using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Serilog;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal static class CapacitySolveWorkerHost
{
    public const string WorkerSwitch = "--solve-worker";
    public const string ProgressPrefix = "__SWITCHYARD_CAPACITY_PROGRESS__";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsWorker(IReadOnlyCollection<string> args) =>
        args.Contains(WorkerSwitch, StringComparer.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args)
    {
        var requestPath = ReadArgument(args, "--request");
        var responsePath = ReadArgument(args, "--response");
        CapacitySolveWorkerResponse response;

        try
        {
            var request = JsonSerializer.Deserialize<CapacitySolveWorkerRequest>(
                    await File.ReadAllTextAsync(requestPath),
                    JsonOptions)
                ?? throw new InvalidOperationException("Worker 求解请求为空或格式不正确。");
            var model = new StationCapacityModel(runInProcess: true);
            var result = await model.SolveAsync(
                request.Input,
                new CapacityModelExecutionContext(request.JobId, request.Resources),
                (percent, message) =>
                {
                    var progress = JsonSerializer.Serialize(
                        new CapacitySolveJobProgress
                        {
                            JobId = request.JobId,
                            Percent = percent,
                            Message = message
                        },
                        JsonOptions);
                    Console.WriteLine($"{ProgressPrefix}{progress}");
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            response = new CapacitySolveWorkerResponse { Success = true, Result = result };
        }
        catch (Exception exception)
        {
            Log.Error(exception, "CapacityAgent Worker 求解失败");
            response = new CapacitySolveWorkerResponse
            {
                Success = false,
                Error = exception.GetBaseException().Message
            };
        }

        await File.WriteAllTextAsync(
            responsePath,
            JsonSerializer.Serialize(response, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return response.Success ? 0 : 1;
    }

    private static string ReadArgument(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        throw new InvalidOperationException($"Worker 缺少参数 {name}。");
    }
}

internal static class CapacitySolveWorkerProcess
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "CapacitySolveWorker");

    public static async Task<JsonElement> SolveAsync(
        JsonElement input,
        CapacityModelExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"switchyard-capacity-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var requestPath = Path.Combine(tempDirectory, "request.json");
        var responsePath = Path.Combine(tempDirectory, "response.json");

        try
        {
            var request = new CapacitySolveWorkerRequest
            {
                JobId = executionContext.JobId,
                Input = input.Clone(),
                Resources = executionContext.Resources
            };
            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(request, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            using var process = StartWorker(requestPath, responsePath);
            CapacityWorkerProcessRegistry.Register(executionContext.JobId, process);
            var stdoutTask = PumpOutputAsync(
                process.StandardOutput,
                executionContext.JobId,
                reportProgress,
                cancellationToken);
            var stderrTask = PumpErrorAsync(process.StandardError, executionContext.JobId, cancellationToken);
            Exception? executionError = null;
            var memoryExceeded = false;
            using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));

            try
            {
                var exitTask = process.WaitForExitAsync(CancellationToken.None);
                while (!exitTask.IsCompleted)
                {
                    await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!process.HasExited &&
                        executionContext.Resources.MemoryLimitBytes > 0 &&
                        SafeWorkingSet(process) > executionContext.Resources.MemoryLimitBytes)
                    {
                        memoryExceeded = true;
                        TryKill(process);
                        break;
                    }
                }

                await exitTask;
            }
            catch (Exception exception)
            {
                executionError = exception;
                TryKill(process);
            }
            finally
            {
                try
                {
                    await process.WaitForExitAsync(CancellationToken.None);
                    await Task.WhenAll(stdoutTask, stderrTask);
                }
                catch (Exception exception) when (executionError is not null)
                {
                    Logger.Debug(exception, "Worker {JobId} 退出清理时发生异常", executionContext.JobId);
                }

                CapacityWorkerProcessRegistry.Unregister(executionContext.JobId, process);
            }

            if (memoryExceeded)
            {
                throw new InvalidOperationException(
                    $"求解任务超过配置的内存上限 {FormatMegabytes(executionContext.Resources.MemoryLimitBytes)} MB，Worker 已终止。");
            }

            if (executionError is not null)
            {
                throw executionError;
            }

            if (!File.Exists(responsePath))
            {
                throw new InvalidOperationException($"求解 Worker 异常退出（退出码 {process.ExitCode}），未生成结果。");
            }

            var response = JsonSerializer.Deserialize<CapacitySolveWorkerResponse>(
                    await File.ReadAllTextAsync(responsePath, cancellationToken),
                    JsonOptions)
                ?? throw new InvalidOperationException("求解 Worker 返回了无效结果。");
            if (!response.Success || !response.Result.HasValue)
            {
                throw new InvalidOperationException(response.Error ?? $"求解 Worker 失败（退出码 {process.ExitCode}）。");
            }

            return response.Result.Value.Clone();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static Process StartWorker(string requestPath, string responsePath)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定 CapacityAgent 可执行程序路径。");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssembly))
            {
                throw new InvalidOperationException("无法确定 CapacityAgent 程序集路径。");
            }

            startInfo.ArgumentList.Add(entryAssembly);
        }

        startInfo.ArgumentList.Add(CapacitySolveWorkerHost.WorkerSwitch);
        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(requestPath);
        startInfo.ArgumentList.Add("--response");
        startInfo.ArgumentList.Add(responsePath);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 CapacityAgent 求解 Worker。");
    }

    private static async Task PumpOutputAsync(
        StreamReader reader,
        string jobId,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            var line = rawLine.TrimStart('\uFEFF');
            if (line.StartsWith(CapacitySolveWorkerHost.ProgressPrefix, StringComparison.Ordinal))
            {
                try
                {
                    var progress = JsonSerializer.Deserialize<CapacitySolveJobProgress>(
                        line[CapacitySolveWorkerHost.ProgressPrefix.Length..],
                        JsonOptions);
                    if (progress is not null)
                    {
                        await reportProgress(progress.Percent, progress.Message);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warning(exception, "无法解析 Worker {JobId} 的进度消息", jobId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                Logger.Information("Worker {JobId}: {WorkerOutput}", jobId, line);
            }
        }
    }

    private static async Task PumpErrorAsync(
        StreamReader reader,
        string jobId,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                Logger.Warning("Worker {JobId} stderr: {WorkerOutput}", jobId, line);
            }
        }
    }

    private static long SafeWorkingSet(Process process)
    {
        try
        {
            process.Refresh();
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private static long FormatMegabytes(long bytes) => bytes / (1024L * 1024L);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have exited between the checks.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Logger.Debug(exception, "删除 Worker 临时目录失败：{WorkerDirectory}", path);
        }
    }
}

internal static class CapacityWorkerProcessRegistry
{
    private static readonly ConcurrentDictionary<string, Process> Processes = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string jobId, Process process) => Processes[jobId] = process;

    public static void Unregister(string jobId, Process process)
    {
        if (Processes.TryGetValue(jobId, out var current) && ReferenceEquals(current, process))
        {
            Processes.TryRemove(jobId, out _);
        }
    }

    public static (long WorkingSetBytes, TimeSpan CpuTime) GetUsage()
    {
        long workingSet = 0;
        var cpuTicks = 0L;
        foreach (var process in Processes.Values)
        {
            try
            {
                process.Refresh();
                if (!process.HasExited)
                {
                    workingSet += Math.Max(0, process.WorkingSet64);
                    cpuTicks += Math.Max(0, process.TotalProcessorTime.Ticks);
                }
            }
            catch
            {
                // A Worker may exit while its resource values are being sampled.
            }
        }

        return (workingSet, TimeSpan.FromTicks(cpuTicks));
    }
}

internal sealed class CapacitySolveWorkerRequest
{
    public string JobId { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
    public CapacityTaskResourceLimits Resources { get; set; } = new();
}

internal sealed class CapacitySolveWorkerResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JsonElement? Result { get; set; }
}
