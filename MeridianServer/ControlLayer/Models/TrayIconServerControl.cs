using System;
using MeridianServer.ControlLayer.Interfaces;

namespace MeridianServer.ControlLayer.Models
{
    public class TrayIconServerControl : IServerControl
    {
        public event EventHandler ServerStartCommandEventHandler;
        public event EventHandler ServerStopCommandEventHandler;
        public event EventHandler ServerRestartCommandEventHandler;
        public event EventHandler CloseCommandEventHandler;
        
        public TrayIconServerControl()
        {
        }
    }
}