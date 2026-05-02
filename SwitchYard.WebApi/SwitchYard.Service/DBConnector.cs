using System;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using MySqlConnector;
using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace SwitchYard.Service
{
    public abstract class DBConnector
    {
        public const string HumpDatabaseSectionName = "HumpDatabase";
        public const string CapacityDatabaseSectionName = "CapacityDatabase";

        // Transaction support fields shared by connectors
        protected DbConnection? _transactionConnection;
        protected DbTransaction? _transaction;
        protected static IConfiguration? _configuration;
        private readonly string _databaseSectionName;

        protected DBConnector(string databaseSectionName = HumpDatabaseSectionName)
        {
            _databaseSectionName = NormalizeDatabaseSectionName(databaseSectionName);
        }

        protected string DatabaseSectionName => _databaseSectionName;

        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static IConfiguration GetConfiguration()
        {
            return _configuration
                ?? throw new InvalidOperationException("Database configuration has not been initialized.");
        }

        public static string GetConfiguredDatabaseType(string databaseSectionName = HumpDatabaseSectionName)
        {
            var configuration = GetConfiguration();
            return configuration[GetConfigPath(databaseSectionName, "DatabaseType")] ?? "Sqllite";
        }

        public static bool IsSqlite(string databaseSectionName = HumpDatabaseSectionName)
        {
            var dbType = GetConfiguredDatabaseType(databaseSectionName);
            return dbType == "SQLite" || dbType == "Sqllite";
        }

        public static bool IsMySql(string databaseSectionName = HumpDatabaseSectionName)
        {
            var dbType = GetConfiguredDatabaseType(databaseSectionName);
            return dbType == "MySQL" || dbType == "Mysql";
        }

        protected abstract DbConnection GetConnection();

        public virtual List<T>? Query<T>(string querySqlString, object? parameters = null)
        {
            if (_transactionConnection != null && _transaction != null)
            {
                try
                {
                    var list = _transactionConnection.Query<T>(querySqlString, parameters, transaction: _transaction).AsList();
                    return list;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return null;
                }
            }
            else
            {
                var conn = GetConnection();
                try
                {
                    var list = conn.Query<T>(querySqlString, parameters).AsList();
                    return list;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return null;
                }
                finally
                {
                    conn.Close();
                }
            }
        }

        public virtual int ExecuteNonQuery(string sqlString, object? parameters = null)
        {
            if (_transactionConnection != null && _transaction != null)
            {
                try
                {
                    var impactRowCount = _transactionConnection.Execute(sqlString, parameters, transaction: _transaction);
                    return impactRowCount;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return 0;
                }
            }
            else
            {
                var conn = GetConnection();
                try
                {
                    var impactRowCount = conn.Execute(sqlString, parameters);
                    return impactRowCount;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return 0;
                }
                finally
                {
                    conn.Close();
                }
            }
        }

        public static DBConnector GetDBConnector(string databaseSectionName = HumpDatabaseSectionName)
        {
            var normalizedSectionName = NormalizeDatabaseSectionName(databaseSectionName);
            var dbType = GetConfiguredDatabaseType(normalizedSectionName);
            if (dbType == "SQLite" || dbType == "Sqllite")
            {
                return new SqlLiteConnector(normalizedSectionName);
            }
            else if (dbType == "MySQL" || dbType == "Mysql")
            {
                return new MysqlConnector(normalizedSectionName);
            }
            else
            {
                throw new NotSupportedException(
                    $"Database type '{dbType}' is not supported for configuration section '{normalizedSectionName}'.");
            }
        }

        public virtual void BeginTransaction()
        {
            if (_transaction != null)
                throw new InvalidOperationException("A transaction is already in progress.");

            _transactionConnection = GetConnection();
            if (_transactionConnection == null)
                throw new InvalidOperationException("GetConnection returned null.");

            _transactionConnection.Open();
            _transaction = _transactionConnection.BeginTransaction();
        }

        public virtual void Commit()
        {
            if (_transaction == null)
                throw new InvalidOperationException("No active transaction to commit.");

            try
            {
                _transaction.Commit();
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
                _transactionConnection.Close();
                _transactionConnection.Dispose();
                _transactionConnection = null;
            }
        }

        public virtual void Rollback()
        {
            if (_transaction == null)
                return;

            try
            {
                _transaction.Rollback();
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
                _transactionConnection.Close();
                _transactionConnection.Dispose();
                _transactionConnection = null;
            }
        }

        private static string NormalizeDatabaseSectionName(string? databaseSectionName)
        {
            return string.IsNullOrWhiteSpace(databaseSectionName)
                ? HumpDatabaseSectionName
                : databaseSectionName;
        }

        private static string GetConfigPath(string databaseSectionName, string configPath)
        {
            return $"{NormalizeDatabaseSectionName(databaseSectionName)}:{configPath}";
        }

        private class SqlLiteConnector : DBConnector
        {
            public SqlLiteConnector(string databaseSectionName)
                : base(databaseSectionName)
            {
            }

            protected override DbConnection GetConnection()
            {
                var configuration = GetConfiguration();
                var path = configuration[GetConfigPath(DatabaseSectionName, "SqlliteConfig:DatabaseFile")];
                var conn = new SqliteConnection($"Data Source={path}");
                return conn;
            }
        }

        private class MysqlConnector : DBConnector
        {
            public MysqlConnector(string databaseSectionName)
                : base(databaseSectionName)
            {
            }

            protected override DbConnection GetConnection()
            {
                var configuration = GetConfiguration();
                var connectionStringBuilder = new MySqlConnectionStringBuilder
                {
                    Server = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:Host")],
                    Port = (uint)configuration.GetValue(GetConfigPath(DatabaseSectionName, "MysqlConfig:Port"), 3306),
                    Database = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:Database")],
                    UserID = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:Username")],
                    Password = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:Password")],
                    CharacterSet = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:CharSet")] ?? "utf8mb4",
                    ConnectionTimeout = (uint)configuration.GetValue(GetConfigPath(DatabaseSectionName, "MysqlConfig:ConnectionTimeout"), 15)
                };

                var sslMode = configuration[GetConfigPath(DatabaseSectionName, "MysqlConfig:SslMode")];
                if (!string.IsNullOrWhiteSpace(sslMode) &&
                    Enum.TryParse<MySqlSslMode>(sslMode, ignoreCase: true, out var parsedSslMode))
                {
                    connectionStringBuilder.SslMode = parsedSslMode;
                }

                if (configuration.GetValue<bool?>(GetConfigPath(DatabaseSectionName, "MysqlConfig:AllowPublicKeyRetrieval")) is bool allowPublicKeyRetrieval)
                {
                    connectionStringBuilder.AllowPublicKeyRetrieval = allowPublicKeyRetrieval;
                }

                return new MySqlConnection(connectionStringBuilder.ConnectionString);
            }
        }
    }
}
