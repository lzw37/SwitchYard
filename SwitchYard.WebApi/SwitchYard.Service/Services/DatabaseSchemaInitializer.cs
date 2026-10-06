using System.Text;

namespace SwitchYard.Service.Services
{
    public sealed class DatabaseSchemaInitializer
    {
        private const string MySqlUtf8mb4UnicodeCollation = "utf8mb4_unicode_ci";
        private static readonly string[] CapacityMySqlUnicodeTables =
        [
            "stationroute",
            "stationroutetime"
        ];

        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseSchemaInitializer> _logger;

        public DatabaseSchemaInitializer(
            IConfiguration configuration,
            ILogger<DatabaseSchemaInitializer> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void EnsureSchemaCreated()
        {
            DBConnector.SetConfiguration(_configuration);

            EnsureSchemaCreatedFor(
                DBConnector.HumpDatabaseSectionName,
                "sqlite-schema.sql",
                "mysql-schema.sql");

            if (HasDatabaseConfiguration(DBConnector.CapacityDatabaseSectionName))
            {
                EnsureSchemaCreatedFor(
                    DBConnector.CapacityDatabaseSectionName,
                    "capacity-sqlite-schema.sql",
                    "capacity-mysql-schema.sql");

                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    EnsureMySqlTableCollations(
                        DBConnector.CapacityDatabaseSectionName,
                        CapacityMySqlUnicodeTables);
                }
                OperationPlanSchemaMigration.EnsureSchema(DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName));
                SwitchYard.Service.StationLayout.StationLayoutSchemaMigration.Apply(
                    DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName),
                    DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName));
                OperationPlanSchemaMigration.MigrateData(DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName));
            }
        }

        private void EnsureMySqlTableCollations(
            string databaseSectionName,
            IEnumerable<string> tableNames)
        {
            var dbConnector = DBConnector.GetDBConnector(databaseSectionName);
            foreach (var tableName in tableNames)
            {
                var collations = dbConnector.Query<DatabaseNameRow>(
                        @"SELECT TABLE_COLLATION AS Name
                          FROM INFORMATION_SCHEMA.TABLES
                          WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
                          UNION ALL
                          SELECT COLLATION_NAME AS Name
                          FROM INFORMATION_SCHEMA.COLUMNS
                          WHERE TABLE_SCHEMA = DATABASE()
                            AND TABLE_NAME = @tableName
                            AND COLUMN_NAME NOT REGEXP 'ID$'
                            AND COLLATION_NAME IS NOT NULL",
                        new { tableName })
                    ?? [];
                var existingCollations = collations
                    .Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (existingCollations.Count == 0 ||
                    existingCollations.All(collation => string.Equals(
                        collation,
                        MySqlUtf8mb4UnicodeCollation,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _logger.LogWarning(
                    "Normalizing MySQL collation for {TableName} from {ExistingCollations} to {TargetCollation}.",
                    tableName,
                    string.Join(", ", existingCollations.Distinct(StringComparer.OrdinalIgnoreCase)),
                    MySqlUtf8mb4UnicodeCollation);
                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {QuoteMySqlIdentifier(tableName)}
                       CONVERT TO CHARACTER SET utf8mb4
                       COLLATE {MySqlUtf8mb4UnicodeCollation}");
            }
        }

        private void EnsureSchemaCreatedFor(string databaseSectionName, string sqliteScriptName, string mysqlScriptName)
        {
            var scriptName = DBConnector.IsMySql(databaseSectionName) ? mysqlScriptName : sqliteScriptName;
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Database schema script not found: {scriptPath}");
            }

            var scriptContent = File.ReadAllText(scriptPath, Encoding.UTF8);
            var statements = scriptContent
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(statement => !string.IsNullOrWhiteSpace(statement));

            var dbConnector = DBConnector.GetDBConnector(databaseSectionName);
            foreach (var statement in statements)
            {
                dbConnector.ExecuteNonQuery(statement);
            }

            _logger.LogInformation(
                "Database schema ensured using {ScriptName} for {DatabaseType} in {DatabaseSectionName}.",
                scriptName,
                DBConnector.GetConfiguredDatabaseType(databaseSectionName),
                databaseSectionName);
        }

        private bool HasDatabaseConfiguration(string databaseSectionName)
        {
            var section = _configuration.GetSection(databaseSectionName);
            if (!section.Exists())
            {
                return false;
            }

            if (DBConnector.IsMySql(databaseSectionName))
            {
                return !string.IsNullOrWhiteSpace(_configuration[$"{databaseSectionName}:MysqlConfig:Database"]);
            }

            if (DBConnector.IsSqlite(databaseSectionName))
            {
                return !string.IsNullOrWhiteSpace(_configuration[$"{databaseSectionName}:SqlliteConfig:DatabaseFile"]);
            }

            return false;
        }

        private static string QuoteMySqlIdentifier(string identifier)
        {
            return $"`{identifier.Replace("`", "``")}`";
        }

        private sealed class DatabaseNameRow
        {
            public string? Name { get; set; }
        }
    }
}
