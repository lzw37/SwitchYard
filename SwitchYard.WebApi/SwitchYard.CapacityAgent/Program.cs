using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.CapacityAgent;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await RunSelfTestAsync();
    return;
}

Console.WriteLine("SwitchYard CapacityAgent");
Console.WriteLine("========================");

var serverUri = ReadServerUri();
var username = ReadRequired("用户名：");
var password = ReadPassword("密码：");
var agentName = Environment.MachineName;
var agentId = CreateAgentId(agentName, username);
var models = new CapacityModelRegistry(new ICapacityModel[] { new StationCapacityModel() });

Console.WriteLine();
Console.WriteLine($"Agent 名称：{agentName}");
Console.WriteLine($"支持模型：{string.Join(", ", models.Descriptors.Select(model => model.Name))}");

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var client = new CapacityAgentClient(serverUri, username, password, agentId, agentName, models);
await client.RunAsync(cancellationSource.Token);
Console.WriteLine("CapacityAgent 已退出。");

static Uri ReadServerUri()
{
    while (true)
    {
        var value = ReadRequired("Web 服务器 URL（例如 https://localhost:7297）：").Trim();
        if (Uri.TryCreate(value.EndsWith('/') ? value : value + "/", UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        Console.WriteLine("请输入有效的 HTTP 或 HTTPS URL。");
    }
}

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            if (password.Length > 0)
            {
                return password.ToString();
            }
            Console.Write(prompt);
            continue;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}

static string CreateAgentId(string machineName, string username)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{machineName}\n{username}"));
    return $"agent-{Convert.ToHexString(bytes)[..16].ToLowerInvariant()}";
}

static async Task RunSelfTestAsync()
{
    var input = new StationCapacitySolveInput
    {
        Settings = new StationCapacitySolveSettings
        {
            LeftShiftToleranceSeconds = 0,
            RightShiftToleranceSeconds = 600,
            TimeLimitSeconds = 30,
            ThreadCount = 1
        },
        Routes =
        {
            new StationCapacityRouteInput { Id = "R1", CellIds = { "C1" } }
        },
        RouteOccupations =
        {
            new StationCapacityRouteOccupationInput
            {
                RouteId = "R1",
                CellId = "C1",
                StartOccupationShiftSeconds = 0,
                EndOccupationShiftSeconds = 0
            }
        },
        Trains =
        {
            new StationCapacityTrainInput
            {
                Id = "T1",
                Movements =
                {
                    new StationCapacityMovementInput
                    {
                        Id = "M1",
                        CandidateRouteIds = { "R1" },
                        OriginalStartSeconds = 0,
                        OriginalEndSeconds = 60,
                        MinDurationSeconds = 60,
                        MaxDurationSeconds = 60
                    }
                }
            },
            new StationCapacityTrainInput
            {
                Id = "T2",
                Movements =
                {
                    new StationCapacityMovementInput
                    {
                        Id = "M1",
                        CandidateRouteIds = { "R1" },
                        OriginalStartSeconds = 0,
                        OriginalEndSeconds = 60,
                        MinDurationSeconds = 60,
                        MaxDurationSeconds = 60
                    }
                }
            }
        }
    };

    var model = new StationCapacityModel();
    var result = await model.SolveAsync(
        JsonSerializer.SerializeToElement(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        (percent, message) =>
        {
            Console.WriteLine($"{percent,3}% {message}");
            return Task.CompletedTask;
        },
        CancellationToken.None);
    var resultModel = result.Deserialize<StationCapacitySolveResult>(
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
    if (resultModel?.HasSolution != true || resultModel.Trains.Count != 2)
    {
        throw new InvalidOperationException("CapacityAgent 自检失败：求解器未返回预期结果。");
    }

    Console.WriteLine(JsonSerializer.Serialize(resultModel, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    }));
    Console.WriteLine("CapacityAgent 自检通过。");
}
