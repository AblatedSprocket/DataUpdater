using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataUpdater.Logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly IConfiguration _config;
        private FileLogger _logger;
        public FileLoggerProvider(IConfiguration config)
        {
            _config = config;
        }
        public ILogger CreateLogger(string categoryName)
        {
            _logger = new FileLogger(categoryName, _config);
            return _logger;
        }

        public void Dispose()
        {
            _logger = null;
        }
    }
}
