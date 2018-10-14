using DataUpdater.Logging;
using Microsoft.Extensions.Configuration;
using System;

namespace DataUpdater.Utilities
{
    public class ExceptionHandler
    {
        private FileLogger FileLogger { get; }
        private SqliteDatabaseLogger SqlLogger { get; }
        public ExceptionHandler(IConfiguration config)
        {
        }
        public static void Handle(Exception ex)
        {
        }
    }
}
