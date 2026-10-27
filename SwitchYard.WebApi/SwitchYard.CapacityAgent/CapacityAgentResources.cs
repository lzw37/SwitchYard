using System.Diagnostics;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

/// <summary>采样代理进程 的资源使用量，并结合任务配额生成服务端状态快照。</summary>
internal sealed class CapacityAgentResources
{
    /// <summary>
    /// 保护 CPU 采样时间、累计 CPU 时间及使用率缓存的互斥锁，避免并发采样交叉更新。
    /// </summary>
    private readonly object _resourceSampleLock = new();
    /// <summary>
    /// 上一次 CPU 资源采样的 UTC 时刻，初始为采样器创建时刻，用于计算两次采样之间的经过时间。
    /// </summary>
    private DateTimeOffset _lastResourceSampleAt = DateTimeOffset.UtcNow;
    /// <summary>
    /// 上一次采样时代理进程的累计 CPU 时间，初始为零，用于计算 CPU 时间增量。
    /// </summary>
    private TimeSpan _lastResourceCpuTime;
    /// <summary>
    /// 最近一次有效采样的 CPU 使用百分比，按逻辑 CPU 核数归一化并限制在 0 至 100，初始为零。
    /// </summary>
    private double _lastCpuUsagePercent;

    /// <summary>
    /// 汇总代理进程 的 CPU 时间和工作集、任务资源分配及可用槽位，生成当前资源快照。
    /// </summary>
    public CapacityAgentResourceStatus CreateStatus(
        CapacityAgentOptions options, IEnumerable<CapacityTaskResourceLimits> resources)
    {
        var activeJobs = resources.ToList();
        var logicalCpuCores = Math.Max(1, Environment.ProcessorCount);
        using var currentProcess = Process.GetCurrentProcess();
        var totalCpuTime = currentProcess.TotalProcessorTime;
        var now = DateTimeOffset.UtcNow;
        double cpuUsagePercent;
        lock (_resourceSampleLock)
        {
            var elapsed = now - _lastResourceSampleAt;
            var cpuDelta = totalCpuTime - _lastResourceCpuTime;
            if (elapsed.TotalMilliseconds >= 100 && cpuDelta >= TimeSpan.Zero)
            {
                _lastCpuUsagePercent = Math.Clamp(
                    cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds / logicalCpuCores * 100,
                    0,
                    100);
            }

            _lastResourceSampleAt = now;
            _lastResourceCpuTime = totalCpuTime;
            cpuUsagePercent = _lastCpuUsagePercent;
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var totalMemoryBytes = Math.Max(0, gcInfo.TotalAvailableMemoryBytes);
        var memoryLoadBytes = Math.Max(0, gcInfo.MemoryLoadBytes);
        var allocatedCpuCores = activeJobs.Sum(job => job.CpuCoreCount);
        var allocatedMemoryBytes = activeJobs.Sum(job => job.MemoryLimitBytes);
        var processWorkingSetBytes = Math.Max(0, currentProcess.WorkingSet64);
        var agentMemoryCapacity = allocatedMemoryBytes > 0
            ? allocatedMemoryBytes
            : options.MemoryLimitBytesPerJob;
        var memoryUsagePercent = agentMemoryCapacity > 0
            ? Math.Clamp(processWorkingSetBytes * 100d / agentMemoryCapacity, 0, 100)
            : 0;

        return new CapacityAgentResourceStatus
        {
            MaxConcurrentJobs = options.MaxConcurrentJobs,
            ActiveJobCount = activeJobs.Count,
            AvailableJobSlots = Math.Max(0, options.MaxConcurrentJobs - activeJobs.Count),
            LogicalCpuCores = logicalCpuCores,
            CpuCoresPerJob = options.CpuCoresPerJob,
            AllocatedCpuCores = allocatedCpuCores,
            CpuUsagePercent = cpuUsagePercent,
            MemoryLimitBytesPerJob = options.MemoryLimitBytesPerJob,
            AllocatedMemoryBytes = allocatedMemoryBytes,
            TotalMemoryBytes = totalMemoryBytes,
            MemoryLoadBytes = memoryLoadBytes,
            ProcessWorkingSetBytes = processWorkingSetBytes,
            MemoryUsagePercent = memoryUsagePercent,
            CollectedAt = now
        };
    }
}
