using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataUpdater.Logging
{
    public class SqliteDatabaseLoggerProvider : ILoggerProvider
    {
        private readonly IConfiguration _config;
        private SqliteDatabaseLogger _logger;
        public SqliteDatabaseLoggerProvider(IConfiguration config)
        {
            _config = config;
        }
        public ILogger CreateLogger(string categoryName)
        {
            _logger = new SqliteDatabaseLogger(categoryName, _config);
            return _logger;
        }

        public void Dispose()
        {
            _logger = null;
        }
    }
}
