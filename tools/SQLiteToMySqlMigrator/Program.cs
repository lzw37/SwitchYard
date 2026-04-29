using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MySqlConnector;

var exitCode = await MigratorProgram.RunAsync(args);
return exitCode;

internal static class MigratorProgram
{
    private static readonly string[] TableOrder =
    [
        "user",
        "humpinstance",
        "slopeline",
        "position",
        "positionsegment",
        "switch",
        "retarder",
        "wagonconcept",
        "operationcondition",
        "humpscheme",
        "vposition",
        "vpositionsegment",
        "humpcalculation",
        "humpcalculationdata",
        "retarderstatus",
        "headwaycheckscheme",
        "headwaycheckwagon",
        "headwaycheckdata",
        "headwaycheckresult",
        "refreshtoken"
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        MigrationReport? report = null;

        try
        {
            var options = MigrationOptions.Parse(args);
            options.LoadDefaultsFromConfig();
            options.Validate();

            report = new MigrationReport
            {
                StartedAt = DateTime.Now,
                SqliteFile = options.SqlitePath!,
                MySqlHost = options.MySqlHost!,
                MySqlPort = options.MySqlPort,
                MySqlDatabase = options.MySqlDatabase!
            };

            WriteStep("Migration plan");
            WriteInfo($"SQLite source: {options.SqlitePath}");
            WriteInfo($"MySQL target: {options.MySqlHost}:{options.MySqlPort} / {options.MySqlDatabase}");
            WriteInfo($"Schema initialization: {!options.SkipSchemaInitialization}");
            WriteInfo($"SQLite precheck: {!options.SkipPrecheck}");
            WriteInfo($"Clear target tables: {!options.SkipClearTarget}");
            WriteInfo($"Post-migration validation: {!options.SkipValidation}");
            WriteInfo($"Table count: {TableOrder.Length}");

            if (options.DryRun)
            {
                WriteStep("DryRun enabled. Exiting before database changes.");
                return 0;
            }

            WriteStep("Ensuring target MySQL database exists");
            await EnsureMySqlDatabaseExistsAsync(options);

            await using var sqliteConnection = new SqliteConnection($"Data Source={options.SqlitePath}");
            await using var mySqlConnection = new MySqlConnection(BuildMySqlConnectionString(options, includeDatabase: true));

            WriteStep("Opening database connections");
            await sqliteConnection.OpenAsync();
            await mySqlConnection.OpenAsync();

            if (!options.SkipPrecheck)
            {
                WriteStep("Running SQLite precheck");
                report.Precheck = await RunPrecheckAsync(sqliteConnection, options.PrecheckPath!);
                foreach (var item in report.Precheck.Where(item => item.RowCount > 0))
                {
                    WriteWarn($"Precheck found {item.RowCount} row(s) for statement: {item.Sql}");
                }
            }

            if (!options.SkipSchemaInitialization)
            {
                WriteStep("Initializing MySQL schema");
                var schemaScript = await File.ReadAllTextAsync(options.SchemaPath!, Encoding.UTF8);
                await ExecuteSqlScriptAsync(mySqlConnection, schemaScript);
            }

            if (!options.SkipClearTarget)
            {
                WriteStep("Clearing target tables");
                foreach (var tableName in TableOrder.Reverse())
                {
                    await ExecuteNonQueryAsync(mySqlConnection, $"DELETE FROM {QuoteMySqlIdentifier(tableName)}");
                }
            }

            WriteStep("Migrating table data");
            await using (var transaction = await mySqlConnection.BeginTransactionAsync())
            {
                foreach (var tableName in TableOrder)
                {
                    var sourceRows = await GetTableCountAsync(sqliteConnection, tableName, isSqlite: true);
                    WriteInfo($"Migrating {tableName} ({sourceRows} row(s))");

                    var insertedRows = await MigrateTableAsync(sqliteConnection, mySqlConnection, transaction, tableName);
                    report.TableResults.Add(new TableMigrationResult
                    {
                        Table = tableName,
                        SourceRows = sourceRows,
                        InsertedRows = insertedRows
                    });
                }

                await transaction.CommitAsync();
            }

            if (!options.SkipValidation)
            {
                WriteStep("Validating row counts");
                foreach (var tableName in TableOrder)
                {
                    var sourceRows = await GetTableCountAsync(sqliteConnection, tableName, isSqlite: true);
                    var targetRows = await GetTableCountAsync(mySqlConnection, tableName, isSqlite: false);
                    var isMatch = sourceRows == targetRows;

                    report.Validation.Add(new TableValidationResult
                    {
                        Table = tableName,
                        SourceRows = sourceRows,
                        TargetRows = targetRows,
                        IsMatch = isMatch
                    });

                    if (!isMatch)
                    {
                        throw new InvalidOperationException(
                            $"Validation failed for table {tableName}. SQLite={sourceRows}, MySQL={targetRows}");
                    }
                }
            }

            report.CompletedAt = DateTime.Now;
            report.Status = "Success";

            var reportPath = SaveReport(report, failed: false);
            WriteStep("Migration completed successfully");
            WriteInfo($"Report saved to: {reportPath}");
            return 0;
        }
        catch (Exception ex)
        {
            if (report != null)
            {
                report.CompletedAt = DateTime.Now;
                report.Status = "Failed";
                report.Error = ex.Message;
                var reportPath = SaveReport(report, failed: true);
                WriteInfo($"Failure report saved to: {reportPath}");
            }

            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task EnsureMySqlDatabaseExistsAsync(MigrationOptions options)
    {
        await using var connection = new MySqlConnection(BuildMySqlConnectionString(options, includeDatabase: false));
        await connection.OpenAsync();

        var sql =
            $"CREATE DATABASE IF NOT EXISTS {QuoteMySqlIdentifier(options.MySqlDatabase!)} CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
        await ExecuteNonQueryAsync(connection, sql);
    }

    private static string BuildMySqlConnectionString(MigrationOptions options, bool includeDatabase)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = options.MySqlHost,
            Port = (uint)options.MySqlPort,
            UserID = options.MySqlUsername,
            Password = options.MySqlPassword ?? string.Empty,
            CharacterSet = "utf8mb4",
            AllowPublicKeyRetrieval = true
        };

        if (includeDatabase)
        {
            builder.Database = options.MySqlDatabase;
        }

        return builder.ConnectionString;
    }

    private static async Task ExecuteSqlScriptAsync(MySqlConnection connection, string scriptContent)
    {
        foreach (var statement in SplitSqlStatements(scriptContent))
        {
            await ExecuteNonQueryAsync(connection, statement);
        }
    }

    private static async Task<List<PrecheckResult>> RunPrecheckAsync(SqliteConnection connection, string precheckPath)
    {
        var results = new List<PrecheckResult>();
        var script = await File.ReadAllTextAsync(precheckPath, Encoding.UTF8);

        foreach (var statement in SplitSqlStatements(script))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;

            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = ConvertFromDbValue(reader.GetValue(i));
                }

                rows.Add(row);
            }

            results.Add(new PrecheckResult
            {
                Sql = statement,
                RowCount = rows.Count,
                Rows = rows
            });
        }

        return results;
    }

    private static async Task<long> GetTableCountAsync(DbConnection connection, string tableName, bool isSqlite)
    {
        var sql = isSqlite
            ? $"SELECT COUNT(*) FROM {QuoteSqliteIdentifier(tableName)}"
            : $"SELECT COUNT(*) FROM {QuoteMySqlIdentifier(tableName)}";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private static async Task<int> MigrateTableAsync(
        SqliteConnection sqliteConnection,
        MySqlConnection mySqlConnection,
        MySqlTransaction transaction,
        string tableName)
    {
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = $"SELECT * FROM {QuoteSqliteIdentifier(tableName)}";

        await using var reader = await sqliteCommand.ExecuteReaderAsync();
        var fieldCount = reader.FieldCount;
        if (fieldCount == 0)
        {
            return 0;
        }

        var columns = Enumerable.Range(0, fieldCount)
            .Select(reader.GetName)
            .ToArray();

        var columnSql = string.Join(", ", columns.Select(QuoteMySqlIdentifier));
        var parameterSql = string.Join(", ", Enumerable.Range(0, fieldCount).Select(i => $"@p{i}"));
        var insertSql = $"INSERT INTO {QuoteMySqlIdentifier(tableName)} ({columnSql}) VALUES ({parameterSql})";

        await using var insertCommand = new MySqlCommand(insertSql, mySqlConnection, transaction);
        for (var i = 0; i < fieldCount; i++)
        {
            insertCommand.Parameters.Add(new MySqlParameter($"@p{i}", DBNull.Value));
        }

        var insertedRows = 0;
        while (await reader.ReadAsync())
        {
            for (var i = 0; i < fieldCount; i++)
            {
                insertCommand.Parameters[i].Value = ConvertToDbValue(reader.GetValue(i));
            }

            await insertCommand.ExecuteNonQueryAsync();
            insertedRows++;
        }

        return insertedRows;
    }

    private static object ConvertToDbValue(object value)
    {
        if (value == DBNull.Value)
        {
            return DBNull.Value;
        }

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            _ => value
        };
    }

    private static object? ConvertFromDbValue(object value)
    {
        return value == DBNull.Value ? null : value;
    }

    private static async Task ExecuteNonQueryAsync(MySqlConnection connection, string sql)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteMySqlIdentifier(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private static string QuoteSqliteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static List<string> SplitSqlStatements(string scriptContent)
    {
        var statements = new List<string>();
        var builder = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;

        foreach (var ch in scriptContent)
        {
            switch (ch)
            {
                case '\'':
                    if (!inDoubleQuote)
                    {
                        inSingleQuote = !inSingleQuote;
                    }
                    builder.Append(ch);
                    break;
                case '"':
                    if (!inSingleQuote)
                    {
                        inDoubleQuote = !inDoubleQuote;
                    }
                    builder.Append(ch);
                    break;
                case ';' when !inSingleQuote && !inDoubleQuote:
                    var statement = builder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        statements.Add(statement);
                    }
                    builder.Clear();
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        var tail = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
        {
            statements.Add(tail);
        }

        return statements;
    }

    private static string SaveReport(MigrationReport report, bool failed)
    {
        var reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), "migration-reports");
        Directory.CreateDirectory(reportDirectory);

        var fileName = failed
            ? $"sqlite-to-mysql-{DateTime.Now:yyyyMMdd-HHmmss}-failed.json"
            : $"sqlite-to-mysql-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var reportPath = Path.Combine(reportDirectory, fileName);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(reportPath, json, Encoding.UTF8);
        return reportPath;
    }

    private static void WriteStep(string message) => Console.WriteLine($"[STEP] {message}");
    private static void WriteInfo(string message) => Console.WriteLine($"[INFO] {message}");
    private static void WriteWarn(string message) => Console.WriteLine($"[WARN] {message}");
}

internal sealed class MigrationOptions
{
    public string? ConfigPath { get; private set; }
    public string? SchemaPath { get; private set; }
    public string? PrecheckPath { get; private set; }
    public string? SqlitePath { get; private set; }
    public string? MySqlHost { get; private set; }
    public int MySqlPort { get; private set; } = 3306;
    public string? MySqlDatabase { get; private set; }
    public string? MySqlUsername { get; private set; }
    public string? MySqlPassword { get; private set; }
    public bool SkipSchemaInitialization { get; private set; }
    public bool SkipPrecheck { get; private set; }
    public bool SkipClearTarget { get; private set; }
    public bool SkipValidation { get; private set; }
    public bool DryRun { get; private set; }

    public static MigrationOptions Parse(string[] args)
    {
        var options = new MigrationOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--config-path":
                    options.ConfigPath = RequireValue(args, ref i, arg);
                    break;
                case "--schema-path":
                    options.SchemaPath = RequireValue(args, ref i, arg);
                    break;
                case "--precheck-path":
                    options.PrecheckPath = RequireValue(args, ref i, arg);
                    break;
                case "--sqlite-path":
                    options.SqlitePath = RequireValue(args, ref i, arg);
                    break;
                case "--mysql-host":
                    options.MySqlHost = RequireValue(args, ref i, arg);
                    break;
                case "--mysql-port":
                    options.MySqlPort = int.Parse(RequireValue(args, ref i, arg));
                    break;
                case "--mysql-database":
                    options.MySqlDatabase = RequireValue(args, ref i, arg);
                    break;
                case "--mysql-username":
                    options.MySqlUsername = RequireValue(args, ref i, arg);
                    break;
                case "--mysql-password":
                    options.MySqlPassword = RequireValue(args, ref i, arg);
                    break;
                case "--skip-schema-initialization":
                    options.SkipSchemaInitialization = true;
                    break;
                case "--skip-precheck":
                    options.SkipPrecheck = true;
                    break;
                case "--skip-clear-target":
                    options.SkipClearTarget = true;
                    break;
                case "--skip-validation":
                    options.SkipValidation = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return options;
    }

    public void LoadDefaultsFromConfig()
    {
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            throw new ArgumentException("Config path is required.");
        }

        ConfigPath = Path.GetFullPath(ConfigPath);
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException($"Config file not found: {ConfigPath}");
        }

        if (!string.IsNullOrWhiteSpace(SchemaPath))
        {
            SchemaPath = Path.GetFullPath(SchemaPath);
        }

        if (!string.IsNullOrWhiteSpace(PrecheckPath))
        {
            PrecheckPath = Path.GetFullPath(PrecheckPath);
        }

        var configRoot = JsonNode.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8));
        SqlitePath ??= ReadConfigValue(configRoot, "HumpDatabase", "SqlliteConfig", "DatabaseFile");
        MySqlHost ??= ReadConfigValue(configRoot, "HumpDatabase", "MysqlConfig", "Host");
        MySqlDatabase ??= ReadConfigValue(configRoot, "HumpDatabase", "MysqlConfig", "Database");
        MySqlUsername ??= ReadConfigValue(configRoot, "HumpDatabase", "MysqlConfig", "Username");
        MySqlPassword ??= ReadConfigValue(configRoot, "HumpDatabase", "MysqlConfig", "Password");

        if (MySqlPort == 3306)
        {
            var portText = ReadConfigValue(configRoot, "HumpDatabase", "MysqlConfig", "Port");
            if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsedPort))
            {
                MySqlPort = parsedPort;
            }
        }

        if (!string.IsNullOrWhiteSpace(SqlitePath))
        {
            SqlitePath = Path.GetFullPath(SqlitePath);
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaPath) || !File.Exists(SchemaPath))
        {
            throw new FileNotFoundException($"MySQL schema file not found: {SchemaPath}");
        }

        if (!SkipPrecheck && (string.IsNullOrWhiteSpace(PrecheckPath) || !File.Exists(PrecheckPath)))
        {
            throw new FileNotFoundException($"SQLite precheck file not found: {PrecheckPath}");
        }

        if (string.IsNullOrWhiteSpace(SqlitePath) || !File.Exists(SqlitePath))
        {
            throw new FileNotFoundException($"SQLite database file not found: {SqlitePath}");
        }

        if (string.IsNullOrWhiteSpace(MySqlHost))
        {
            throw new ArgumentException("MySQL host is required.");
        }

        if (string.IsNullOrWhiteSpace(MySqlDatabase))
        {
            throw new ArgumentException("MySQL database is required.");
        }

        if (string.IsNullOrWhiteSpace(MySqlUsername))
        {
            throw new ArgumentException("MySQL username is required.");
        }
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}");
        }

        index++;
        return args[index];
    }

    private static string? ReadConfigValue(JsonNode? root, params string[] path)
    {
        var current = root;
        foreach (var part in path)
        {
            current = current?[part];
        }

        return current?.ToString();
    }
}

internal sealed class MigrationReport
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string? Error { get; set; }
    public string SqliteFile { get; set; } = string.Empty;
    public string MySqlHost { get; set; } = string.Empty;
    public int MySqlPort { get; set; }
    public string MySqlDatabase { get; set; } = string.Empty;
    public List<TableMigrationResult> TableResults { get; set; } = [];
    public List<PrecheckResult> Precheck { get; set; } = [];
    public List<TableValidationResult> Validation { get; set; } = [];
}

internal sealed class TableMigrationResult
{
    public string Table { get; set; } = string.Empty;
    public long SourceRows { get; set; }
    public int InsertedRows { get; set; }
}

internal sealed class TableValidationResult
{
    public string Table { get; set; } = string.Empty;
    public long SourceRows { get; set; }
    public long TargetRows { get; set; }
    public bool IsMatch { get; set; }
}

internal sealed class PrecheckResult
{
    public string Sql { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}
