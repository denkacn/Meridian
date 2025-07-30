using MeridianServer.LogsLayer.Loggers;
using MeridianServerLib.LogsLayer.Interfaces;
using System;

namespace MeridianServer.LogsLayer
{
    public static class LoggerExt
    {
        private static ILogger _logger;

        public static ILogger Logger => _logger;

        public static void Init()
        {
            _logger = new Log4NetLogger();
        }

        public static void Log(string message)
        {
            _logger?.Log(message);
        }

        public static void LogError(string message, Exception exception)
        {
            _logger?.LogError(message, exception);
        }
    }
}