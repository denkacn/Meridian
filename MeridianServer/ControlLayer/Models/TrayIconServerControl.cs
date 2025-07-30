using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MeridianServer.ControlLayer.Interfaces;

namespace MeridianServer.ControlLayer.Models
{
    public class TrayIconServerControl : IServerControl
    {
        public event EventHandler ServerStartCommandEventHandler;
        public event EventHandler ServerStopCommandEventHandler;
        public event EventHandler ServerRestartCommandEventHandler;
        public event EventHandler CloseCommandEventHandler;

        private NotifyIcon _notificationIcon; 
        
        public TrayIconServerControl()
        {
            CreateTrayIcon();
        }
        
        private void CreateTrayIcon()
        {
            var notifyThread = new Thread(
                delegate()
                {
                    _notificationIcon = new NotifyIcon();
                    _notificationIcon.Text = "MeridianServer";
                    _notificationIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

                    _notificationIcon.ContextMenuStrip = new ContextMenuStrip();
                    
                    _notificationIcon.ContextMenuStrip.Items.Add("Start Server", null, (sender, args) =>
                    {
                        ServerStartCommandEventHandler?.Invoke(this, args);
                    });

                    _notificationIcon.ContextMenuStrip.Items.Add("Stop Server", null, (sender, args) =>
                    {
                        ServerStopCommandEventHandler?.Invoke(this, args);
                    });

                    _notificationIcon.ContextMenuStrip.Items.Add("Restart Server", null, (sender, args) =>
                    {
	                    ServerRestartCommandEventHandler?.Invoke(this, args);
                    });

					_notificationIcon.ContextMenuStrip.Items.Add("Close", null, (sender, args) =>
                    {
                        CloseCommandEventHandler?.Invoke(this, args);

                        _notificationIcon.Dispose();

                        Application.Exit();
                    });

                    _notificationIcon.Visible = true;
                    
                    Application.Run();
                }
            );
            
            notifyThread.Start();
        }
    }
}