using DataUpdater.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataUpdater.Logging
{
    public class SqliteDatabaseLogger : ILogger
    {
        private string _name;
        private string _connectionString;
        private string _logTable;
        private int _historySpan;
        private IEnumerable<LogLevel> _logLevels;
        public SqliteDatabaseLogger(string name, IConfiguration config)
        {
            _name = name;
            _connectionString = BuildDatabaseConnectionFromPath(config["Path"]);
            _logTable = config["LogTable"];
            _historySpan = Convert.ToInt32(config["HistorySpan"]);
            _logLevels = config.GetSection("LogLevels").AsEnumerable().Where(kvp => Enum.TryParse(typeof(LogLevel), kvp.Value, out object parsed)).Select(kvp => (LogLevel)Enum.Parse(typeof(LogLevel), kvp.Value));
            DeleteOldEntries();

        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                using (SqliteConnection conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    string sql = $@"
                        INSERT INTO {_logTable}
                        (Application, Source, Message, Trace)
                        VALUES
                        (@APPLICATION, @SOURCE, @MESSAGE, @TRACE)";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@APPLICATION", SqliteType.Text).Value = "HomeBase";
                    cmd.Parameters.Add("@SOURCE", SqliteType.Text).Value = exception.Source;
                    cmd.Parameters.Add("@MESSAGE", SqliteType.Text).Value = exception.Message;
                    cmd.Parameters.Add("@TRACE", SqliteType.Text).Value = exception.StackTrace;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logLevels.Contains(logLevel);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        private string BuildDatabaseConnectionFromPath(string dbFilePath)
        {
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            return builder.ToString();
        }

        private void DeleteOldEntries()
        {
            try
            {
                using (SqliteConnection conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    string sql = $@"
                        DELETE FROM {_logTable}
                        WHERE DateEntered < @CUTOFFDATE";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@CUTOFFDATE", SqliteType.Text).Value = DateTime.Today.AddDays(-_historySpan).ToSqliteStorage();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception)
            {
                throw new Exception("Logger not set up properly.");
            }
        }
    }
}
