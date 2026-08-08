using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SwitchYard.Capacity;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Hubs;

[Authorize(Policy = "CapacityAgent")]
public sealed class CapacityAgentHub : Hub
{
    private readonly CapacityAgentRegistry _agents;
    private readonly CapacitySolveJobService _jobs;
    private readonly ILogger<CapacityAgentHub> _logger;

    public CapacityAgentHub(
        CapacityAgentRegistry agents,
        CapacitySolveJobService jobs,
        ILogger<CapacityAgentHub> logger)
    {
        _agents = agents;
        _jobs = jobs;
        _logger = logger;
    }

    public Task RegisterAgent(
        string agentId,
        string name,
        List<CapacityModelDescriptor> models,
        string? activeJobId = null)
    {
        var normalizedAgentId = agentId?.Trim();
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAgentId) || normalizedAgentId.Length > 100)
        {
            throw new HubException("Agent ID 无效。");
        }

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            throw new HubException("Agent 名称无效。");
        }

        var normalizedModels = (models ?? new List<CapacityModelDescriptor>())
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Take(20)
            .ToList();
        if (normalizedModels.Count == 0)
        {
            throw new HubException("CapacityAgent 必须注册至少一个模型。");
        }

        var username = Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var abandonedJobId = _agents.Register(
            normalizedAgentId,
            normalizedName,
            username,
            Context.ConnectionId,
            normalizedModels,
            activeJobId);
        if (!string.IsNullOrWhiteSpace(abandonedJobId))
        {
            _jobs.FailJob(abandonedJobId, "CapacityAgent 已重启，原求解任务未能完成。");
        }

        _logger.LogInformation(
            "CapacityAgent {AgentId} ({AgentName}) registered by {Username} with {ModelCount} model(s).",
            normalizedAgentId,
            normalizedName,
            username,
            normalizedModels.Count);
        return Task.CompletedTask;
    }

    public Task ReportProgress(CapacitySolveJobProgress progress)
    {
        _jobs.ReportProgress(Context.ConnectionId, progress);
        return Task.CompletedTask;
    }

    public Task CompleteJob(CapacitySolveJobCompletion completion)
    {
        _jobs.Complete(Context.ConnectionId, completion);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _agents.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
