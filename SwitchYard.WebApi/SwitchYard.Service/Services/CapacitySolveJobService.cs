using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SwitchYard.Capacity;
using SwitchYard.Service.Hubs;

namespace SwitchYard.Service.Services;

public sealed class CapacitySolveJobService
{
    private readonly CapacityAgentRegistry _agents;
    private readonly IHubContext<CapacityAgentHub> _hubContext;
    private readonly SaturatedOperationPlanService _saturatedPlans;
    private readonly ILogger<CapacitySolveJobService> _logger;
    private readonly ConcurrentDictionary<string, CapacitySolveJob> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SaturatedPlanJobContext> _saturatedPlanContexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _completingJobs = new(StringComparer.OrdinalIgnoreCase);

    public CapacitySolveJobService(
        CapacityAgentRegistry agents,
        IHubContext<CapacityAgentHub> hubContext,
        SaturatedOperationPlanService saturatedPlans,
        ILogger<CapacitySolveJobService> logger)
    {
        _agents = agents;
        _hubContext = hubContext;
        _saturatedPlans = saturatedPlans;
        _logger = logger;
    }

    public async Task<(CapacitySolveJob? Job, string? Error)> SubmitAsync(
        CapacitySolveJobRequest request,
        string requestedBy,
        CancellationToken cancellationToken,
        SaturatedPlanJobContext? saturatedPlanContext = null)
    {
        var jobId = Guid.NewGuid().ToString("N");
        if (!_agents.TryReserve(
                request.AgentId,
                request.ModelId,
                jobId,
                requestedBy,
                out var connectionId,
                out var agent,
                out var resourceLimits,
                out var reservationError))
        {
            return (null, reservationError);
        }

        var submittedAt = DateTimeOffset.UtcNow;
        var job = new CapacitySolveJob
        {
            JobId = jobId,
            AgentId = request.AgentId,
            AgentName = agent?.Name ?? request.AgentId,
            ModelId = request.ModelId,
            RequestedBy = requestedBy,
            Status = "queued",
            ProgressMessage = "任务已提交，等待 CapacityAgent 接收",
            SubmittedAt = submittedAt,
            PresetId = saturatedPlanContext?.PresetId,
            PresetName = saturatedPlanContext?.PresetName,
            SourceOperationPlanId = saturatedPlanContext?.SourceOperationPlanId
        };
        _jobs[jobId] = job;
        if (saturatedPlanContext != null)
        {
            _saturatedPlanContexts[jobId] = saturatedPlanContext;
        }

        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(
                "Solve",
                new CapacitySolveCommand
                {
                    JobId = jobId,
                    ModelId = request.ModelId,
                    Input = request.Input.Clone(),
                    SubmittedAt = submittedAt,
                    Resources = resourceLimits
                },
                cancellationToken);
            return (Clone(job), null);
        }
        catch (Exception exception)
        {
            FailJob(jobId, $"向 CapacityAgent 发送任务失败：{exception.Message}");
            _logger.LogError(exception, "Failed to dispatch capacity solve job {JobId}.", jobId);
            return (Clone(job), job.Error);
        }
    }

    public void ReportProgress(string connectionId, CapacitySolveJobProgress progress)
    {
        if (!_jobs.TryGetValue(progress.JobId, out var job) ||
            !_agents.IsCurrentConnection(job.AgentId, connectionId))
        {
            throw new HubException("任务不存在或不属于当前 CapacityAgent。 ");
        }

        lock (job)
        {
            if (job.Status is "completed" or "failed")
            {
                return;
            }

            job.Status = "running";
            job.StartedAt ??= DateTimeOffset.UtcNow;
            job.Progress = Math.Clamp(progress.Percent, 0, 100);
            job.ProgressMessage = progress.Message?.Trim() ?? string.Empty;
        }
        _agents.Touch(job.AgentId);
    }

    public void Complete(string connectionId, CapacitySolveJobCompletion completion)
    {
        if (!_jobs.TryGetValue(completion.JobId, out var job) ||
            !_agents.IsCurrentConnection(job.AgentId, connectionId))
        {
            throw new HubException("任务不存在或不属于当前 CapacityAgent。 ");
        }

        if (!_completingJobs.TryAdd(completion.JobId, 0))
        {
            return;
        }

        try
        {
            OperationPlanRow? savedPlan = null;
            if (completion.Success && _saturatedPlanContexts.TryRemove(completion.JobId, out var saturatedContext))
            {
                try
                {
                    if (!completion.Result.HasValue)
                    {
                        throw new InvalidOperationException("CapacityAgent 未返回可保存的求解结果。");
                    }

                    lock (job)
                    {
                        job.Status = "running";
                        job.StartedAt ??= DateTimeOffset.UtcNow;
                        job.Progress = Math.Max(job.Progress, 99);
                        job.ProgressMessage = "求解完成，正在保存新的饱和作业计划";
                    }
                    savedPlan = _saturatedPlans.Save(saturatedContext, completion.Result.Value, completion.JobId);
                }
                catch (Exception exception)
                {
                    lock (job)
                    {
                        job.Status = "failed";
                        job.Error = $"求解完成，但保存饱和作业计划失败：{exception.Message}";
                        job.ProgressMessage = "保存饱和作业计划失败";
                        job.CompletedAt = DateTimeOffset.UtcNow;
                    }
                    _agents.Release(job.AgentId, job.JobId);
                    _logger.LogError(exception, "Failed to save saturated operation plan for job {JobId}.", completion.JobId);
                    return;
                }
            }

            lock (job)
            {
                if (job.Status is "completed" or "failed")
                {
                    return;
                }

                job.StartedAt ??= DateTimeOffset.UtcNow;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = completion.Success ? "completed" : "failed";
                job.Progress = completion.Success ? 100 : job.Progress;
                job.ProgressMessage = completion.Success ? "求解完成" : "求解失败";
                job.Error = completion.Success ? null : completion.Error ?? "CapacityAgent 求解失败。";
                job.Result = completion.Result?.Clone();
                job.ResultOperationPlanId = savedPlan?.OperationPlanID;
                job.ResultOperationPlanName = savedPlan?.Name;
            }
            _saturatedPlanContexts.TryRemove(completion.JobId, out _);
            _agents.Release(job.AgentId, job.JobId);
        }
        finally
        {
            _completingJobs.TryRemove(completion.JobId, out _);
        }
    }

    public void FailJob(string jobId, string error)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return;
        }
        if (_completingJobs.ContainsKey(jobId))
        {
            return;
        }

        lock (job)
        {
            if (job.Status is "completed" or "failed")
            {
                return;
            }

            job.Status = "failed";
            job.Error = error;
            job.ProgressMessage = "求解失败";
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
        _saturatedPlanContexts.TryRemove(jobId, out _);
        _agents.Release(job.AgentId, job.JobId);
    }

    public CapacitySolveJob? Get(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? Clone(job) : null;
    }

    public IReadOnlyList<CapacitySolveJob> GetRecent(string requestedBy, bool includeAll, int limit = 50)
    {
        return _jobs.Values
            .Where(job => includeAll || string.Equals(job.RequestedBy, requestedBy, StringComparison.Ordinal))
            .OrderByDescending(job => job.SubmittedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(Clone)
            .ToList();
    }

    private static CapacitySolveJob Clone(CapacitySolveJob source)
    {
        lock (source)
        {
            return new CapacitySolveJob
            {
                JobId = source.JobId,
                AgentId = source.AgentId,
                AgentName = source.AgentName,
                ModelId = source.ModelId,
                RequestedBy = source.RequestedBy,
                Status = source.Status,
                Progress = source.Progress,
                ProgressMessage = source.ProgressMessage,
                SubmittedAt = source.SubmittedAt,
                StartedAt = source.StartedAt,
                CompletedAt = source.CompletedAt,
                Error = source.Error,
                Result = source.Result?.Clone(),
                PresetId = source.PresetId,
                PresetName = source.PresetName,
                SourceOperationPlanId = source.SourceOperationPlanId,
                ResultOperationPlanId = source.ResultOperationPlanId,
                ResultOperationPlanName = source.ResultOperationPlanName
            };
        }
    }
}
