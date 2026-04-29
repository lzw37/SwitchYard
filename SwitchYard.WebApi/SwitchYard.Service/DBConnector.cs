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
        // Transaction support fields shared by connectors
        protected DbConnection? _transactionConnection;
        protected DbTransaction? _transaction;
        protected static IConfiguration? _configuration;

        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static string GetConfiguredDatabaseType()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Database configuration has not been initialized.");
            }

            return _configuration["HumpDatabase:DatabaseType"] ?? "Sqllite";
        }

        public static bool IsSqlite()
        {
            var dbType = GetConfiguredDatabaseType();
            return dbType == "SQLite" || dbType == "Sqllite";
        }

        public static bool IsMySql()
        {
            var dbType = GetConfiguredDatabaseType();
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

        public static DBConnector GetDBConnector()
        {
            var dbType = GetConfiguredDatabaseType();
            if (dbType == "SQLite" || dbType == "Sqllite")
            {
                return new SqlLiteConnector();
            }
            else if (dbType == "MySQL" || dbType == "Mysql")
            {
                return new MysqlConnector();
            }
            else
            {
                throw new NotSupportedException($"Database type '{dbType}' is not supported.");
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
        private class SqlLiteConnector : DBConnector
        {
            protected override DbConnection GetConnection()
            {
                var path = _configuration["HumpDatabase:SqlliteConfig:DatabaseFile"];
                var conn = new SqliteConnection($"Data Source={path}");
                return conn;
            }
        }

        private class MysqlConnector : DBConnector
        {
            protected override DbConnection GetConnection()
            {
                var connectionStringBuilder = new MySqlConnectionStringBuilder
                {
                    Server = _configuration["HumpDatabase:MysqlConfig:Host"],
                    Port = (uint)_configuration.GetValue("HumpDatabase:MysqlConfig:Port", 3306),
                    Database = _configuration["HumpDatabase:MysqlConfig:Database"],
                    UserID = _configuration["HumpDatabase:MysqlConfig:Username"],
                    Password = _configuration["HumpDatabase:MysqlConfig:Password"],
                    CharacterSet = _configuration["HumpDatabase:MysqlConfig:CharSet"] ?? "utf8mb4",
                    ConnectionTimeout = (uint)_configuration.GetValue("HumpDatabase:MysqlConfig:ConnectionTimeout", 15)
                };

                var sslMode = _configuration["HumpDatabase:MysqlConfig:SslMode"];
                if (!string.IsNullOrWhiteSpace(sslMode) &&
                    Enum.TryParse<MySqlSslMode>(sslMode, ignoreCase: true, out var parsedSslMode))
                {
                    connectionStringBuilder.SslMode = parsedSslMode;
                }

                if (_configuration.GetValue<bool?>("HumpDatabase:MysqlConfig:AllowPublicKeyRetrieval") is bool allowPublicKeyRetrieval)
                {
                    connectionStringBuilder.AllowPublicKeyRetrieval = allowPublicKeyRetrieval;
                }

                return new MySqlConnection(connectionStringBuilder.ConnectionString);
            }
        }
    }
}
