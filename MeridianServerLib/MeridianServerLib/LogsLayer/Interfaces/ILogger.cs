using System;

namespace MeridianServerLib.LogsLayer.Interfaces
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message, Exception exception);
    }
}