using System.Text;
using System.Text.Json;
using Serilog;
using SwitchYard.Capacity;

namespace SwitchYard.CapacitySolver;

/// <summary>Executes a registered solver inside an isolated process hosted by the caller.</summary>
public static class CapacitySolveWorkerHost
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
                    await File.ReadAllTextAsync(requestPath), JsonOptions)
                ?? throw new InvalidOperationException("Worker 求解请求为空或格式不正确。");
            var models = CapacitySolverModels.CreateRegistry();
            if (string.IsNullOrWhiteSpace(request.ModelId) || !models.TryGet(request.ModelId, out var model))
                throw new InvalidOperationException($"Worker 不支持求解模型 {request.ModelId}。");
            var result = await model.SolveAsync(
                request.Input,
                new CapacityModelExecutionContext(request.JobId, request.Resources),
                (percent, message) =>
                {
                    var progress = JsonSerializer.Serialize(new CapacitySolveJobProgress
                    {
                        JobId = request.JobId,
                        Percent = percent,
                        Message = message
                    }, JsonOptions);
                    Console.WriteLine($"{ProgressPrefix}{progress}");
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            response = new CapacitySolveWorkerResponse { Success = true, Result = result };
        }
        catch (Exception exception)
        {
            Log.Error(exception, "CapacitySolver Worker 求解失败");
            response = new CapacitySolveWorkerResponse
            {
                Success = false,
                Error = exception.GetBaseException().Message
            };
        }

        await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return response.Success ? 0 : 1;
    }

    private static string ReadArgument(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
                return Path.GetFullPath(args[index + 1]);
        throw new InvalidOperationException($"Worker 缺少参数 {name}。");
    }
}
