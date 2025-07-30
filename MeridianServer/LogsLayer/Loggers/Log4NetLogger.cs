using System;
using System.IO;
using log4net;
using log4net.Config;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServer.LogsLayer.Loggers
{
    public class Log4NetLogger : ILogger
    {
        private readonly ILog _log;
        
        public Log4NetLogger()
        {
            _log = LogManager.GetLogger("MeridianServer");
            var fileInfo = new FileInfo("log4net.config");
            XmlConfigurator.Configure(fileInfo);
        }
        public void Log(string message)
        {
            _log.Info(message);
        }

        public void LogError(string message, Exception exception)
        {
            _log.Error(message, exception);
        }
    }
}