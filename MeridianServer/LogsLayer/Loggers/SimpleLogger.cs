using System;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServer.LogsLayer.Loggers
{
    public class SimpleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public void LogError(string message, Exception exception)
        {
            Console.WriteLine("[!Error!] " + message + "\n" + exception);
        }
    }
}