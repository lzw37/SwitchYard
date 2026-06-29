using System.Text;

namespace SwitchYard.Service.Services
{
    public sealed class DatabaseSchemaInitializer
    {
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
    }
}
