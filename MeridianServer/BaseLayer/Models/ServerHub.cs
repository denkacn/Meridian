using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MeridianServer.BaseLayer.Interfaces;
using MeridianServer.ControlLayer.Interfaces;
using MeridianServer.ControlLayer.Models;
using MeridianServer.ExternalLayer.Controllers;
using MeridianServer.ExternalLayer.ResourcesLoader;
using MeridianServer.LogsLayer;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.Models;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Models.Server;

namespace MeridianServer.BaseLayer.Models
{
    public class ServerHub : IServerController
    {
        private const string SETTING_FILE_PATH = "server_setting.json";

        private IServerControl _serverControl;
        private ITransport _transport;
        private ServerSettings _serverSettings;
        private List<MeridianApplicationConfiguration> _applicationConfigurations;

        private string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;


		public void Init()
        {
            LoggerExt.Log("[ServerHub] Server Init Start");

            InitSettingsLayer();

            InitExternalLayers();
            
            InitServerControl();
            
            InitTransportLayer();
            
            LoggerExt.Log("[ServerHub] Server Init End");

            _ = TryAutoStartAsync();
        }

		private async Task TryAutoStartAsync()
		{
			if (_serverSettings.IsAutoStart)
			{
				await Task.Delay(2000).ConfigureAwait(true);

				StartServer();
			}
		}

		private void Discard()
        {
            LoggerExt.Log("[ServerHub] Server Discard Start");

            DiscardServerControl();
            
            DiscardTransportLayer();

            DiscardExternalLayer();

            LoggerExt.Log("[ServerHub] Server Discard End");
        }

        private async Task Restart()
        {
	        LoggerExt.Log("[ServerHub] Restart");

	        LoggerExt.Log("[ServerHub] Discard");
			Discard();

	        await Task.Delay(1000);

	        LoggerExt.Log("[ServerHub] Init");
			Init();

	        await Task.Delay(1000);

	        LoggerExt.Log("[ServerHub] StartServer");
			//StartServer();
        }

        #region Setting Layer

        private void InitSettingsLayer()
        {
	        _serverSettings = DataLoader.Load<ServerSettings>(Path.Combine(BaseDirectory, SETTING_FILE_PATH));
        }

        #endregion

        #region External Layer 

        private void InitExternalLayers()
        {
	        _applicationConfigurations = new List<MeridianApplicationConfiguration>();

			foreach (var layer in _serverSettings.Layers)
			{
                var path = Path.Combine(BaseDirectory, layer.PathToExternalApplicationLib);
				MeridianApplication meridianApplication;

                try
                {
                    meridianApplication = (MeridianApplication)ExternalApplicationController.SearchExternalApplication(path);
                }
                catch (MeridianExternalLogicException ex)
                {
                    throw new MeridianExternalLogicException(
                        $"Failed to load Meridian layer '{layer.LayerName}' from '{path}'.", ex);
                }

				meridianApplication.MeridianApplicationCommand += OnMeridianApplicationCommand;

                Console.WriteLine("[ServerHub] Load layer: " + layer.LayerName + " from " + layer.PathToExternalApplicationLib);

				_applicationConfigurations.Add(new MeridianApplicationConfiguration(layer.LayerName, meridianApplication, layer.Port, path));
			}
        }

        private void DiscardExternalLayer()
        {
	        foreach (var application in _applicationConfigurations)
	        {
		        application.MeridianApplication.MeridianApplicationCommand -= OnMeridianApplicationCommand;
			}

	        _applicationConfigurations = null;

        }

		#endregion

		#region Control Layer  

		private void InitServerControl()
        {
            _serverControl = new TrayIconServerControl();
            _serverControl.ServerStartCommandEventHandler += OnServerControlServerStartCommand;
            _serverControl.ServerStopCommandEventHandler += OnServerControlServerStopCommand;
            _serverControl.ServerRestartCommandEventHandler += OnServerControlServerRestartCommand;
			_serverControl.CloseCommandEventHandler += OnServerControlCloseCommand;
        }

		private void DiscardServerControl()
        {
            _serverControl.ServerStartCommandEventHandler -= OnServerControlServerStartCommand;
            _serverControl.ServerStopCommandEventHandler -= OnServerControlServerStopCommand;
            _serverControl.ServerRestartCommandEventHandler -= OnServerControlServerRestartCommand;
			_serverControl.CloseCommandEventHandler -= OnServerControlCloseCommand;
        }

        #endregion

        #region Transport Layer

        private void InitTransportLayer()
        {
            _transport = new Transport(_applicationConfigurations.ToArray(),
	            _serverSettings.IsDebugEnable ? LoggerExt.Logger : null);
        }

        private void DiscardTransportLayer()
        {
            _transport.Discard();
        }

        private void StartServer()
        {
            _transport.Start();
        }

        private void StopServer()
        {
            _transport.Stop();
        }

        #endregion

        private void OnServerControlServerStartCommand(object sender, EventArgs e)
        {
	        StartServer();
        }
        
        private void OnServerControlServerStopCommand(object sender, EventArgs e)
        {
	        StopServer();
        }

        private void OnServerControlServerRestartCommand(object sender, EventArgs e)
        {
			_ = Restart();
        }

		private void OnServerControlCloseCommand(object sender, EventArgs e)
        {
            Discard();
        }

        private void OnMeridianApplicationCommand(OutsideCommandType command)
        {
	        LoggerExt.Log("[ServerHub] OnMeridianApplicationCommand command: " + command);

			if (command == OutsideCommandType.Restart)
	        {
				_ = Restart();
	        }
        }
	}
}
