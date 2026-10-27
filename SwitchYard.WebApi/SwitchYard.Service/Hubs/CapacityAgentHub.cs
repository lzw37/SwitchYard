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

    public Task RegisterAgent(CapacityAgentRegistration registration)
    {
        registration ??= new CapacityAgentRegistration();
        var normalizedAgentId = registration.AgentId?.Trim();
        var normalizedName = registration.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAgentId) || normalizedAgentId.Length > 100)
        {
            throw new HubException("Agent ID 无效。");
        }

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            throw new HubException("Agent 名称无效。");
        }

        var normalizedModels = (registration.Models ?? new List<CapacityModelDescriptor>())
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Take(20)
            .ToList();
        if (normalizedModels.Count == 0)
        {
            throw new HubException("CapacityAgent 必须注册至少一个模型。");
        }

        var username = Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var isAdministrator = string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(
                                  Context.User?.FindFirstValue(ClaimTypes.Role),
                                  "Admin",
                                  StringComparison.OrdinalIgnoreCase);
        var abandonedJobIds = _agents.Register(
            normalizedAgentId,
            normalizedName,
            username,
            Context.ConnectionId,
            normalizedModels,
            (registration.ActiveJobIds ?? new List<string>()).Take(128).ToList(),
            registration.Resources ?? new CapacityAgentResourceStatus(),
            isAdministrator,
            registration.AccessPolicy ?? new CapacityAgentAccessPolicy());
        foreach (var abandonedJobId in abandonedJobIds)
        {
            _jobs.FailJob(abandonedJobId, "CapacityAgent 已重启，原求解任务未能完成。");
        }

        _logger.LogInformation(
            "CapacityAgent {AgentId} ({AgentName}) registered by {Username} ({Role}) with {ModelCount} model(s), {ActiveJobCount}/{MaxConcurrentJobs} active jobs. Requested allow-all: {AllowAllUsers}; whitelist entries: {AllowedUserCount}.",
            normalizedAgentId,
            normalizedName,
            username,
            isAdministrator ? "Admin" : "User",
            normalizedModels.Count,
            registration.ActiveJobIds?.Count ?? 0,
            registration.Resources?.MaxConcurrentJobs ?? 1,
            registration.AccessPolicy?.AllowAllUsers ?? false,
            registration.AccessPolicy?.AllowedUsers?.Count ?? 0);
        return Task.CompletedTask;
    }

    public Task ReportStatus(CapacityAgentResourceStatus resources)
    {
        var agentId = _agents.GetAgentId(Context.ConnectionId);
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new HubException("CapacityAgent 尚未注册。");
        }

        _agents.UpdateStatus(agentId, Context.ConnectionId, resources ?? new CapacityAgentResourceStatus());
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
