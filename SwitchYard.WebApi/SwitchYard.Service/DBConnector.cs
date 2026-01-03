using System;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Collections.Generic;
using MySqlConnector;
using System.Data.Common;
using Org.BouncyCastle.Bcpg;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace SwitchYard.Service
{
    public class DBConnector
    {
        // Transaction support fields shared by connectors
        protected DbConnection _transactionConnection;
        protected DbTransaction _transaction;
        protected static IConfiguration _configuration;

        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected virtual DbConnection GetConnection()
        {
            return null;
        }

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
            return 0;
        }

        public static DBConnector GetDBConnector()
        {
            var dbType = _configuration["HumpDatabase:DatabaseType"];
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
                var host = _configuration["HumpDatabase:MysqlConfig:Host"];
                var port = _configuration.GetValue<int>("HumpDatabase:MysqlConfig:Port");
                var db = _configuration["HumpDatabase:MysqlConfig:Database"];
                var user = _configuration["HumpDatabase:MysqlConfig:Username"];
                var pass = _configuration["HumpDatabase:MysqlConfig:Password"];
                var connStr = $"Server={host};Port={port};Database={db};User Id={user};Password={pass};Charset=utf8;";
                return new MySqlConnection(connStr);
            }
        }
    }
}
