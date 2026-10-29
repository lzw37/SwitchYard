using System.Collections.Concurrent;
using System.Globalization;
using SwitchYard.Service.Utils;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SwitchYard.Capacity;
using SwitchYard.Service.Hubs;

namespace SwitchYard.Service.Services;

public sealed class CapacitySolveJobService
{
    private readonly CapacityAgentRegistry _agents;
    private readonly SnowflakeIdGenerator _ids;
    private readonly IHubContext<CapacityAgentHub> _hubContext;
    private readonly SaturatedOperationPlanService _saturatedPlans;
    private readonly CapacitySolveJobStore _store;
    private readonly ILogger<CapacitySolveJobService> _logger;
    private readonly ConcurrentDictionary<string, StoredCapacitySolveJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public CapacitySolveJobService(
        CapacityAgentRegistry agents,
        IHubContext<CapacityAgentHub> hubContext,
        SaturatedOperationPlanService saturatedPlans,
        CapacitySolveJobStore store,
        ILogger<CapacitySolveJobService> logger,
        SnowflakeIdGenerator ids)
    {
        _agents = agents;
        _ids = ids;
        _hubContext = hubContext;
        _saturatedPlans = saturatedPlans;
        _store = store;
        _logger = logger;
        foreach (var record in store.Load())
        {
            record.Job.CreatedAt = record.Job.CreatedAt == default ? record.Job.SubmittedAt : record.Job.CreatedAt;
            if (string.IsNullOrWhiteSpace(record.Job.Name)) record.Job.Name = DefaultName(record.Job.CreatedAt == default ? DateTimeOffset.UtcNow : record.Job.CreatedAt);
            record.Job.Settings = CapacitySolvePresetService.NormalizeSettings(record.Job.Settings ?? ReadSettings(record.Request.Input));
            if (record.Job.Status == "draft") record.Request.Input = ApplySettings(record.Request.Input, record.Job.Settings);
            record.Request.Name = record.Job.Name;
            record.Job.InstanceId ??= record.SaturatedPlanContext?.InstanceId ?? record.Request.InstanceId ?? InputField(record.Request.Input, "instanceId");
            record.Job.InstanceId = record.Job.InstanceId?.Trim();
            record.Request.InstanceId = record.Job.InstanceId;
            _jobs[record.Job.JobId] = record;
        }
    }

    private static string DefaultName(DateTimeOffset createdAt) => "求解任务" + createdAt.ToOffset(TimeSpan.FromHours(8)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    public static bool IsActive(string status) => status is "queued" or "running" or "cancelling";

    public static bool IsTerminal(string status) => status is "completed" or "failed" or "cancelled";

    public (CapacitySolveJob? Job, string? Error) CreateDraft(CapacitySolveDraftRequest request, string requestedBy)
    {
        var instanceId = request.InstanceId?.Trim() ?? string.Empty;
        if (instanceId.Length == 0 || string.IsNullOrWhiteSpace(requestedBy)) return (null, "实例和任务所有者不能为空。");
        if (request.Name?.Trim().Length > 100) return (null, "任务名称最长为 100 个字符。");
        var input = request.Input?.Clone() ?? JsonSerializer.SerializeToElement<object?>(null);
        var inputError = ValidateDraftInput(instanceId, request.StationSchemeId, request.OperationPlanId, input);
        if (inputError != null) return (null, inputError);
        var settings = CapacitySolvePresetService.NormalizeSettings(request.Settings ?? ReadSettings(input));
        input = ApplySettings(input, settings);
        var now = DateTimeOffset.UtcNow;
        var job = new CapacitySolveJob
        {
            JobId = _ids.NextIdString(), Name = string.IsNullOrWhiteSpace(request.Name) ? DefaultName(now) : request.Name.Trim(),
            InstanceId = instanceId, RequestedBy = requestedBy, Status = "draft", ProgressMessage = "草稿已保存，尚未开始求解",
            CreatedAt = now, SubmittedAt = now, Revision = 1, Settings = settings,
            AgentId = request.AgentId?.Trim() ?? string.Empty, ModelId = request.ModelId?.Trim() ?? string.Empty,
            StationSchemeId = request.StationSchemeId?.Trim(), SourceOperationPlanId = request.OperationPlanId?.Trim()
        };
        job.AgentName = _agents.GetAgents(requestedBy).FirstOrDefault(agent => agent.AgentId == job.AgentId)?.Name ?? job.AgentId;
        var record = new StoredCapacitySolveJob { Job = job, Request = new()
        {
            Name = job.Name, InstanceId = instanceId, AgentId = job.AgentId, ModelId = job.ModelId, Input = input
        } };
        _store.Save(record);
        _jobs[job.JobId] = record;
        return (Clone(job), null);
    }

    public (CapacitySolveJob? Job, string? Error) UpdateDraft(string jobId, CapacitySolveDraftRequest request)
    {
        if (!_jobs.TryGetValue(jobId, out var record)) return (null, "任务不存在。");
        lock (record)
        {
            if (!_jobs.ContainsKey(jobId)) return (null, "任务不存在。");
            if (record.Job.Status != "draft") return (null, "只有尚未开始求解的草稿可以编辑。");
            if (record.Job.Revision != request.Revision) return (null, "草稿已被修改，请重新打开任务后再编辑。");
            if (!string.Equals(record.Job.InstanceId, request.InstanceId?.Trim(), StringComparison.Ordinal)) return (null, "不能更改任务所属实例。");
            if (request.Name?.Trim().Length > 100) return (null, "任务名称最长为 100 个字符。");
            var input = request.Input?.Clone() ?? JsonSerializer.SerializeToElement<object?>(null);
            var inputError = ValidateDraftInput(record.Job.InstanceId!, request.StationSchemeId, request.OperationPlanId, input);
            if (inputError != null) return (null, inputError);
            var job = Clone(record.Job);
            job.Name = string.IsNullOrWhiteSpace(request.Name) ? job.Name : request.Name.Trim();
            job.AgentId = request.AgentId?.Trim() ?? string.Empty;
            job.AgentName = _agents.GetAgents(job.RequestedBy).FirstOrDefault(agent => agent.AgentId == job.AgentId)?.Name ?? job.AgentId;
            job.ModelId = request.ModelId?.Trim() ?? string.Empty;
            job.Settings = CapacitySolvePresetService.NormalizeSettings(request.Settings ?? ReadSettings(input) ?? record.Job.Settings);
            input = ApplySettings(input, job.Settings);
            job.StationSchemeId = request.StationSchemeId?.Trim(); job.SourceOperationPlanId = request.OperationPlanId?.Trim();
            job.Revision++;
            var candidate = new StoredCapacitySolveJob { Job = job, Request = new()
            {
                Name = job.Name, InstanceId = job.InstanceId, AgentId = job.AgentId, ModelId = job.ModelId, Input = input
            } };
            _store.Save(candidate);
            record.Job = job; record.Request = candidate.Request;
            return (Clone(job), null);
        }
    }

    private static readonly JsonSerializerOptions SettingsJsonOptions = new(JsonSerializerDefaults.Web);

    private static StationCapacitySolveSettings? ReadSettings(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) return null;
        var settings = input.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, "settings", StringComparison.OrdinalIgnoreCase)).Value;
        if (settings.ValueKind != JsonValueKind.Object) return null;
        try { return settings.Deserialize<StationCapacitySolveSettings>(SettingsJsonOptions); }
        catch (JsonException) { return null; }
    }

    // Preserve solver-specific input fields while keeping the form's settings authoritative.
    private static JsonElement ApplySettings(JsonElement input, StationCapacitySolveSettings settings)
    {
        if (input.ValueKind != JsonValueKind.Object) return input.Clone();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in input.EnumerateObject())
                if (!string.Equals(property.Name, "settings", StringComparison.OrdinalIgnoreCase)) property.WriteTo(writer);
            writer.WritePropertyName("settings");
            JsonSerializer.Serialize(writer, settings, SettingsJsonOptions);
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static string? ValidateDraftInput(string instanceId, string? schemeId, string? planId, JsonElement input)
    {
        var inputInstance = InputField(input, "instanceId");
        if (!string.IsNullOrWhiteSpace(inputInstance) && !string.Equals(instanceId, inputInstance.Trim(), StringComparison.Ordinal))
            return "求解输入的实例与当前任务所属实例不一致。";
        foreach (var (field, selected) in new[] { ("stationSchemeId", schemeId), ("operationPlanId", planId) })
        {
            var actual = InputField(input, field);
            if (!string.IsNullOrWhiteSpace(actual) && !string.IsNullOrWhiteSpace(selected) && !string.Equals(actual.Trim(), selected.Trim(), StringComparison.Ordinal))
                return "请求 JSON 的站场方案或作业计划与任务配置不一致，请重新生成请求数据。";
        }
        return null;
    }

    public async Task<(CapacitySolveJob? Job, string? Error)> StartAsync(string jobId, long revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_jobs.TryGetValue(jobId, out var record)) return (null, "任务不存在。");
        string connectionId;
        CapacityTaskResourceLimits resources;
        lock (record)
        {
            if (!_jobs.ContainsKey(jobId)) return (null, "任务不存在。");
            if (record.Job.Status != "draft") return (null, "任务已经开始或已经结束，不能重复启动。");
            if (record.Job.Revision != revision) return (null, "草稿已被修改，请先保存最新配置再开始求解。");
            if (string.IsNullOrWhiteSpace(record.Request.AgentId) || string.IsNullOrWhiteSpace(record.Request.ModelId) || record.Request.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return (null, "请选择计算代理、求解器并生成请求数据。");
            if (!_agents.TryReserve(record.Request.AgentId, record.Request.ModelId, jobId, record.Job.RequestedBy,
                out connectionId, out var agent, out resources, out var error, RequiredModelVersion(record.Request))) return (null, error);
            var job = Clone(record.Job);
            job.Status = "queued"; job.AgentName = agent?.Name ?? job.AgentId;
            job.ProgressMessage = "任务已提交，等待 CapacityAgent 接收";
            job.SubmittedAt = DateTimeOffset.UtcNow; job.Revision++;
            try { _store.Save(new StoredCapacitySolveJob { Job = job, Request = record.Request }); }
            catch { _agents.Release(job.AgentId, jobId); throw; }
            record.Job = job;
        }
        return await DispatchAsync(record, connectionId, resources);
    }

    public async Task<(CapacitySolveJob? Job, string? Error)> SubmitAsync(
        CapacitySolveJobRequest request,
        string requestedBy,
        CancellationToken cancellationToken,
        SaturatedPlanJobContext? saturatedPlanContext = null,
        string? retryOfJobId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputInstanceId = InputField(request.Input, "instanceId")?.Trim();
        var instanceId = (saturatedPlanContext?.InstanceId ??
            (string.IsNullOrWhiteSpace(request.InstanceId) ? inputInstanceId : request.InstanceId))?.Trim();
        if (string.IsNullOrWhiteSpace(instanceId)) return (null, "求解任务必须指定所属实例。");
        if (!string.IsNullOrWhiteSpace(inputInstanceId) && !string.Equals(inputInstanceId, instanceId, StringComparison.Ordinal))
            return (null, "求解输入的实例与当前选择的实例不一致，请重新生成输入。");
        if (request.Name?.Trim().Length > 100) return (null, "任务名称最长为 100 个字符。");
        var jobId = _ids.NextIdString();
        var createdAt = DateTimeOffset.UtcNow;
        if (!_agents.TryReserve(request.AgentId, request.ModelId, jobId, requestedBy,
                out var connectionId, out var agent, out var resources, out var error, RequiredModelVersion(request)))
            return (null, error);

        var job = new CapacitySolveJob
        {
            JobId = jobId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? DefaultName(createdAt) : request.Name.Trim(),
            CreatedAt = createdAt,
            Revision = 1,
            Settings = CapacitySolvePresetService.NormalizeSettings(ReadSettings(request.Input)),
            AgentId = request.AgentId,
            AgentName = agent?.Name ?? request.AgentId,
            ModelId = request.ModelId,
            RequestedBy = requestedBy,
            Status = "queued",
            ProgressMessage = "任务已提交，等待 CapacityAgent 接收",
            SubmittedAt = DateTimeOffset.UtcNow,
            PresetId = saturatedPlanContext?.PresetId,
            PresetName = saturatedPlanContext?.PresetName,
            SourceOperationPlanId = saturatedPlanContext?.SourceOperationPlanId ?? InputField(request.Input, "operationPlanId"),
            InstanceId = instanceId,
            StationSchemeId = saturatedPlanContext?.StationSchemeId ?? InputField(request.Input, "stationSchemeId"),
            RetryOfJobId = retryOfJobId
        };
        var record = new StoredCapacitySolveJob
        {
            Job = job,
            Request = new CapacitySolveJobRequest { Name = job.Name, InstanceId = instanceId, AgentId = request.AgentId, ModelId = request.ModelId, Input = request.Input.Clone() },
            SaturatedPlanContext = saturatedPlanContext
        };
        try
        {
            _store.Save(record);
            _jobs[jobId] = record;
        }
        catch
        {
            _agents.Release(job.AgentId, jobId);
            throw;
        }

        return await DispatchAsync(record, connectionId, resources);
    }

    private async Task<(CapacitySolveJob? Job, string? Error)> DispatchAsync(StoredCapacitySolveJob record, string connectionId, CapacityTaskResourceLimits resources)
    {
        var job = record.Job;
        // After durable acceptance, closing the HTTP request must not cancel dispatch.
        try
        {
            using var dispatchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _hubContext.Clients.Client(connectionId).SendAsync("Solve", new CapacitySolveCommand
            {
                JobId = job.JobId,
                ModelId = job.ModelId,
                Input = record.Request.Input.Clone(),
                SubmittedAt = job.SubmittedAt,
                Resources = resources
            }, dispatchTimeout.Token);
            return (Get(job.JobId), null);
        }
        catch (Exception exception)
        {
            // Delivery may have happened before the transport failed. Keep the reservation
            // until cancellation acknowledgement or agent reconciliation, never run twice.
            lock (record)
            {
                if (!IsTerminal(job.Status))
                {
                    job.Status = "cancelling";
                    job.ProgressMessage = "任务发送未确认，等待计算代理确认停止";
                    job.Error = $"向 CapacityAgent 发送任务失败：{exception.Message}";
                    _store.Save(record);
                }
            }
            await SendCancellationAsync(job);
            _logger.LogError(exception, "Failed to dispatch capacity solve job {JobId}.", job.JobId);
            return (Get(job.JobId), job.Error);
        }
    }

    private static string? InputField(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object
            ? input.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value is var value
                && value.ValueKind == JsonValueKind.String ? value.GetString() : null
            : null;

    private static string? RequiredModelVersion(CapacitySolveJobRequest request)
    {
        if (!string.Equals(request.ModelId, CapacityAgentProtocol.StationCapacityModelId, StringComparison.OrdinalIgnoreCase) ||
            request.Input.ValueKind != JsonValueKind.Object) return null;
        string? requestedMinimum = null;
        var containsProcess = false;
        var containsExactPrecedence = false;
        var containsParameterSets = false;
        var containsProcessReference = false;
        var containsLocationObjects = false;
        foreach (var property in request.Input.EnumerateObject())
        {
            if (string.Equals(property.Name, "minimumModelVersion", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                requestedMinimum = property.Value.GetString();
            if ((string.Equals(property.Name, "trains", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(property.Name, "routeOccupations", StringComparison.OrdinalIgnoreCase)) && property.Value.ValueKind == JsonValueKind.Array)
                containsParameterSets |= property.Value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object &&
                    item.EnumerateObject().Any(field => string.Equals(field.Name, "calculationParameterSetId", StringComparison.OrdinalIgnoreCase) &&
                        field.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.Value.GetString())));
            if (string.Equals(property.Name, "trains", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array)
                foreach (var train in property.Value.EnumerateArray().Where(train => train.ValueKind == JsonValueKind.Object))
                {
                    containsProcessReference |= train.EnumerateObject().Any(field =>
                        string.Equals(field.Name, "operationProcessId", StringComparison.OrdinalIgnoreCase) &&
                        field.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.Value.GetString()));
                    containsLocationObjects |= train.EnumerateObject().Any(field =>
                        string.Equals(field.Name, "movements", StringComparison.OrdinalIgnoreCase) && field.Value.ValueKind == JsonValueKind.Array &&
                        field.Value.EnumerateArray().Any(movement => movement.ValueKind == JsonValueKind.Object &&
                            movement.EnumerateObject().Any(locations =>
                                (string.Equals(locations.Name, "startLocationsByRoute", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(locations.Name, "endLocationsByRoute", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(locations.Name, "startLocationIdsByRoute", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(locations.Name, "endLocationIdsByRoute", StringComparison.OrdinalIgnoreCase)) &&
                                locations.Value.ValueKind == JsonValueKind.Object &&
                                locations.Value.EnumerateObject().Any(route => route.Value.ValueKind == JsonValueKind.Array &&
                                    route.Value.EnumerateArray().Any(location => location.ValueKind == JsonValueKind.Object)))));
                    foreach (var field in train.EnumerateObject().Where(field => string.Equals(field.Name, "processConstraints", StringComparison.OrdinalIgnoreCase)))
                    {
                        containsProcess |= field.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
                        if (field.Value.ValueKind != JsonValueKind.Object) continue;
                        containsExactPrecedence |= field.Value.EnumerateObject().Any(value =>
                            string.Equals(value.Name, "precedences", StringComparison.OrdinalIgnoreCase) && value.Value.ValueKind == JsonValueKind.Array &&
                            value.Value.EnumerateArray().Any(precedence => precedence.ValueKind == JsonValueKind.Object &&
                                precedence.EnumerateObject().Any(flag => string.Equals(flag.Name, "isExact", StringComparison.OrdinalIgnoreCase) && flag.Value.ValueKind == JsonValueKind.True)));
                    }
                }
        }
        var required = containsProcessReference || containsLocationObjects ? new Version(2, 5, 0) : containsParameterSets ? new Version(2, 4, 0) : containsExactPrecedence ? new Version(2, 3, 0) : new Version(2, 2, 0);
        if ((containsProcess || containsParameterSets || containsProcessReference || containsLocationObjects) && (!Version.TryParse(requestedMinimum, out var version) || version < required)) return required.ToString();
        return requestedMinimum;
    }

    public void ReportProgress(string connectionId, CapacitySolveJobProgress progress)
    {
        var record = GetAgentRecord(connectionId, progress.JobId);
        lock (record)
        {
            var job = record.Job;
            if (!IsActive(job.Status) || job.Status == "cancelling") return;
            job.Status = "running";
            job.StartedAt ??= DateTimeOffset.UtcNow;
            job.Progress = Math.Clamp(progress.Percent, 0, 100);
            job.ProgressMessage = progress.Message?.Trim() ?? string.Empty;
            _store.Save(record);
        }
        _agents.Touch(record.Job.AgentId);
    }

    public void Complete(string connectionId, CapacitySolveJobCompletion completion)
    {
        // A terminal task may have been deleted after persistence but before the
        // agent received its acknowledgement. Acknowledge that replay as well.
        if (!_jobs.ContainsKey(completion.JobId))
        {
            var agentId = _agents.GetAgentId(connectionId) ?? throw new HubException("CapacityAgent 尚未注册。");
            _agents.Release(agentId, completion.JobId);
            return;
        }
        var record = GetAgentRecord(connectionId, completion.JobId);
        lock (record)
        {
            var job = record.Job;
            if (job.Status == "draft") return;
            if (IsTerminal(job.Status))
            {
                if (_jobs.ContainsKey(job.JobId)) _store.Save(record);
                _agents.Release(job.AgentId, job.JobId);
                return;
            }
            job.StartedAt ??= DateTimeOffset.UtcNow;
            job.Result = completion.Result?.Clone();
            if (job.Status == "cancelling" || completion.Cancelled)
            {
                job.Status = "cancelled";
                job.ProgressMessage = "任务已取消";
                job.Error = null;
                job.Result = null;
            }
            else
            {
                job.Status = completion.Success ? "completed" : "failed";
                job.Progress = completion.Success ? 100 : job.Progress;
                job.ProgressMessage = completion.Success ? "求解完成" : "求解失败";
                job.Error = completion.Success ? null : completion.Error ?? "CapacityAgent 求解失败。";
                if (completion.Success && record.SaturatedPlanContext != null)
                {
                    try
                    {
                        if (!completion.Result.HasValue) throw new InvalidOperationException("CapacityAgent 未返回可保存的求解结果。");
                        var savedPlan = _saturatedPlans.Save(record.SaturatedPlanContext, completion.Result.Value, completion.JobId);
                        job.ResultOperationPlanId = savedPlan.OperationPlanID;
                        job.ResultOperationPlanName = savedPlan.Name;
                    }
                    catch (Exception exception)
                    {
                        job.Status = "failed";
                        job.Error = $"求解完成，但保存饱和作业计划失败：{exception.Message}";
                        job.ProgressMessage = "保存饱和作业计划失败";
                        _logger.LogError(exception, "Failed to save saturated operation plan for job {JobId}.", completion.JobId);
                    }
                }
            }
            job.CompletedAt = DateTimeOffset.UtcNow;
            _store.Save(record);
            _agents.Release(job.AgentId, job.JobId);
        }
    }

    public void FailJob(string jobId, string error)
    {
        if (!_jobs.TryGetValue(jobId, out var record)) return;
        lock (record)
        {
            var job = record.Job;
            if (!IsActive(job.Status)) return;
            job.Status = job.Status == "cancelling" ? "cancelled" : "failed";
            job.Error = job.Status == "cancelled" ? null : error;
            job.ProgressMessage = job.Status == "cancelled" ? "任务已取消" : "求解失败";
            job.CompletedAt = DateTimeOffset.UtcNow;
            _store.Save(record);
            _agents.Release(job.AgentId, job.JobId);
        }
    }

    public async Task<CapacitySolveJob?> CancelAsync(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var record)) return null;
        lock (record)
        {
            if (!IsActive(record.Job.Status)) return Clone(record.Job);
            record.Job.Status = "cancelling";
            record.Job.ProgressMessage = "已请求取消，等待计算代理停止（离线时将在重连后执行）";
            _store.Save(record);
        }
        await SendCancellationAsync(record.Job);
        return Get(jobId);
    }

    private async Task SendCancellationAsync(CapacitySolveJob job)
    {
        var connectionId = _agents.GetConnectionId(job.AgentId);
        if (connectionId == null) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _hubContext.Clients.Client(connectionId).SendAsync("CancelJob", job.JobId, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cancellation for {JobId} will be retried after agent reconnects.", job.JobId);
        }
    }

    public async Task DeliverCancellationsAsync(string agentId)
    {
        foreach (var record in _jobs.Values.Where(r => string.Equals(r.Job.AgentId, agentId, StringComparison.OrdinalIgnoreCase)))
        {
            bool cancel;
            lock (record) cancel = record.Job.Status == "cancelling";
            if (cancel) await SendCancellationAsync(record.Job);
        }
    }

    // Also reconciles durable records after API restart, when the registry has no prior state.
    public async Task ReconcileAgentAsync(string agentId, IReadOnlyCollection<string> activeJobIds, DateTimeOffset? registrationStartedAt = null)
    {
        var active = activeJobIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var record in _jobs.Values.Where(r => string.Equals(r.Job.AgentId, agentId, StringComparison.OrdinalIgnoreCase)))
        {
            bool cancel;
            lock (record)
            {
                // New requests may reserve a slot as soon as Register makes the agent
                // available. They were not part of the registration's active-job snapshot.
                if (record.Job.Status == "draft") continue;
                if (registrationStartedAt.HasValue && record.Job.SubmittedAt > registrationStartedAt.Value) continue;
                if (IsTerminal(record.Job.Status))
                {
                    _agents.Release(agentId, record.Job.JobId);
                    continue;
                }
                if (!active.Contains(record.Job.JobId))
                {
                    FailJob(record.Job.JobId, "CapacityAgent 已重启或任务已丢失，原求解任务未能完成。可使用原输入重试。");
                    continue;
                }
                cancel = record.Job.Status == "cancelling";
            }
            if (cancel) await SendCancellationAsync(record.Job);
        }
    }

    public Task<(CapacitySolveJob? Job, string? Error)> RetryAsync(string jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var record))
            return Task.FromResult<(CapacitySolveJob?, string?)>((null, "任务不存在。"));
        lock (record)
        {
            if (!IsTerminal(record.Job.Status))
                return Task.FromResult<(CapacitySolveJob?, string?)>((null, "任务尚未结束，无法重试。"));
            return SubmitAsync(record.Request, record.Job.RequestedBy, cancellationToken, record.SaturatedPlanContext, jobId);
        }
    }

    public bool Delete(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var record)) return false;
        lock (record)
        {
            if (record.Job.Status != "draft" && !IsTerminal(record.Job.Status)) return false;
            _store.Delete(jobId);
            return _jobs.TryRemove(jobId, out _);
        }
    }

    public CapacitySolveJob? Get(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var record)) return null;
        lock (record) return Clone(record.Job);
    }

    public JsonElement? GetInput(string jobId) =>
        _jobs.TryGetValue(jobId, out var record) ? record.Request.Input.Clone() : null;

    public IReadOnlyList<CapacitySolveJob> GetRecent(string requestedBy, bool includeAll, int limit = 50, string? instanceId = null) =>
        GetPage(requestedBy, includeAll, limit, 0, null, instanceId).Items;

    public (IReadOnlyList<CapacitySolveJob> Items, int Total) GetPage(string requestedBy, bool includeAll, int limit, int offset, string? status, string? instanceId = null)
    {
        var normalizedInstanceId = instanceId?.Trim();
        if (string.IsNullOrEmpty(normalizedInstanceId)) return (Array.Empty<CapacitySolveJob>(), 0);
        var jobs = _jobs.Values.Where(r => includeAll || string.Equals(r.Job.RequestedBy, requestedBy, StringComparison.Ordinal))
            .Select(r => { lock (r) return Clone(r.Job, includeResult: false); })
            .Where(job => string.Equals(job.InstanceId, normalizedInstanceId, StringComparison.Ordinal))
            .Where(job => string.IsNullOrEmpty(status) || (status == "active" ? IsActive(job.Status) : job.Status == status))
            .OrderByDescending(job => !IsTerminal(job.Status)).ThenByDescending(job => job.SubmittedAt).ThenBy(job => job.JobId)
            .ToList();
        return (jobs.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 200)).ToList(), jobs.Count);
    }

    private StoredCapacitySolveJob GetAgentRecord(string connectionId, string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var record) || !_agents.IsCurrentConnection(record.Job.AgentId, connectionId))
            throw new HubException("任务不存在或不属于当前 CapacityAgent。");
        return record;
    }

    private static CapacitySolveJob Clone(CapacitySolveJob source, bool includeResult = true) => new()
    {
        JobId = source.JobId, Name = source.Name, CreatedAt = source.CreatedAt, Revision = source.Revision, AgentId = source.AgentId, AgentName = source.AgentName, ModelId = source.ModelId,
        Settings = CapacitySolvePresetService.NormalizeSettings(source.Settings),
        RequestedBy = source.RequestedBy, Status = source.Status, Progress = source.Progress,
        ProgressMessage = source.ProgressMessage, SubmittedAt = source.SubmittedAt, StartedAt = source.StartedAt,
        CompletedAt = source.CompletedAt, Error = source.Error, Result = includeResult ? source.Result?.Clone() : null,
        PresetId = source.PresetId, PresetName = source.PresetName, SourceOperationPlanId = source.SourceOperationPlanId,
        ResultOperationPlanId = source.ResultOperationPlanId, ResultOperationPlanName = source.ResultOperationPlanName,
        InstanceId = source.InstanceId, StationSchemeId = source.StationSchemeId, RetryOfJobId = source.RetryOfJobId
    };
}
