using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DataUpdater.Logging
{
    public class LoggerConfiguration
    {
        public IEnumerable<LogLevel> LogLevels { get; set; } = new LogLevel[] { };
        public string LogDirectory { get; set; } = string.Empty;
        public int HistorySpan { get; set; } = 30;
        public string DatabasePath { get; set; } = string.Empty;
        public string LogTable { get; set; } = string.Empty;
        public LoggerConfiguration(IEnumerable<LogLevel> logLevels, string logDirectory, int historySpan)
        {
            LogLevels = logLevels;
            LogDirectory = logDirectory;
            HistorySpan = historySpan;
        }

        public LoggerConfiguration(IEnumerable<LogLevel> logLevels, string databasePath, string logTable)
        {
            LogLevels = logLevels;
            DatabasePath = databasePath;
            LogTable = logTable;
        }
    }
}
