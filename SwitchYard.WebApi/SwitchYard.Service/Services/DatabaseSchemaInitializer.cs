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

            var scriptName = DBConnector.IsMySql() ? "mysql-schema.sql" : "sqlite-schema.sql";
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Database schema script not found: {scriptPath}");
            }

            var scriptContent = File.ReadAllText(scriptPath, Encoding.UTF8);
            var statements = scriptContent
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(statement => !string.IsNullOrWhiteSpace(statement));

            var dbConnector = DBConnector.GetDBConnector();
            foreach (var statement in statements)
            {
                dbConnector.ExecuteNonQuery(statement);
            }

            _logger.LogInformation(
                "Database schema ensured using {ScriptName} for {DatabaseType}.",
                scriptName,
                DBConnector.GetConfiguredDatabaseType());
        }
    }
}
