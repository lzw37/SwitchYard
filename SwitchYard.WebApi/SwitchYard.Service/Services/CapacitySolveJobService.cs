using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SwitchYard.Capacity;
using SwitchYard.Service.Hubs;

namespace SwitchYard.Service.Services;

public sealed class CapacitySolveJobService
{
    private readonly CapacityAgentRegistry _agents;
    private readonly IHubContext<CapacityAgentHub> _hubContext;
    private readonly ILogger<CapacitySolveJobService> _logger;
    private readonly ConcurrentDictionary<string, CapacitySolveJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public CapacitySolveJobService(
        CapacityAgentRegistry agents,
        IHubContext<CapacityAgentHub> hubContext,
        ILogger<CapacitySolveJobService> logger)
    {
        _agents = agents;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<(CapacitySolveJob? Job, string? Error)> SubmitAsync(
        CapacitySolveJobRequest request,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        if (!_agents.TryReserve(
                request.AgentId,
                request.ModelId,
                jobId,
                out var connectionId,
                out var agent,
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
            SubmittedAt = submittedAt
        };
        _jobs[jobId] = job;

        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(
                "Solve",
                new CapacitySolveCommand
                {
                    JobId = jobId,
                    ModelId = request.ModelId,
                    Input = request.Input.Clone(),
                    SubmittedAt = submittedAt
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
        }
        _agents.Release(job.AgentId, job.JobId);
    }

    public void FailJob(string jobId, string error)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
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
                Result = source.Result?.Clone()
            };
        }
    }
}
