using System;

namespace MeridianServer.ControlLayer.Interfaces
{
    public interface IServerControl
    {
        event EventHandler ServerStartCommandEventHandler;
        event EventHandler ServerStopCommandEventHandler;
        event EventHandler ServerRestartCommandEventHandler;
		event EventHandler CloseCommandEventHandler;
    }
}