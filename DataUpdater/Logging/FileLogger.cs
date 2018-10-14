using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DataUpdater.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _name;
        private readonly string _file = string.Concat(DateTime.Today, ".txt");
        private string _saveDirectory;
        private string _saveFile;
        private int _historySpan;
        private IEnumerable<LogLevel> _logLevels = new LogLevel[] { };
        public FileLogger(string name, IConfiguration config)
        {
            _name = name;
            _historySpan = Convert.ToInt32(config["HistorySpan"]);
            SetLogFile(config["LogDirectory"]);
            _logLevels = config.GetSection("LogLevels").AsEnumerable().Where(kvp => Enum.TryParse(typeof(LogLevel), kvp.Value, out object parsed)).Select(kvp => (LogLevel)Enum.Parse(typeof(LogLevel), kvp.Value));
            DeleteExpiredLogs();
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                try
                {
                    using (StreamWriter writer = File.AppendText(_saveFile))
                    {
                        await writer.WriteLineAsync($"{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")} {logLevel}: {formatter(state, exception)}");
                    }
                }
                catch (IOException ex)
                {
                    if (IsFileLocked(ex))
                    {
                        Log(logLevel, eventId, state, exception, formatter);
                    }
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

        private void DeleteExpiredLogs()
        {
            foreach (string file in Directory.GetFiles(_saveDirectory))
            {
                if (File.GetCreationTime(file) < DateTime.Now.AddDays(-_historySpan))
                {
                    File.Delete(file);
                }
            }
        }
        private bool IsFileLocked(Exception exception)
        {
            const int ERROR_SHARING_VIOLATION = 32;
            const int ERROR_LOCK_VIOLATION = 33;
            int errorCode = Marshal.GetHRForException(exception) & ((1 << 16) - 1);
            return errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION;
        }
        private void SetLogFile(string path)
        {
            _saveDirectory = path;
            _saveFile = Path.Combine(_saveDirectory, string.Concat(DateTime.Today.ToString("yyyy-MM-dd"), ".txt"));
            Directory.CreateDirectory(_saveDirectory);
        }
    }
}
