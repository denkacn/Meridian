using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.Models;
using MeridianServer.TransportLayer.NetCoreServerDomain;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Net;

namespace MeridianServer.TransportLayer.ApplicationProvider
{
	public class BaseApplicationProvider : IApplicationProvider
	{
		public bool IsStarted => _server.IsStarted;

		private readonly string _id;
		private readonly string _path;

		private readonly IServer _server;
		private readonly ILogger _logger;
		private readonly IMeridianApplication _applicationLogic;

		public BaseApplicationProvider(string id, TransportParams transportParams, IMeridianApplication applicationLogic, string path, ILogger logger)
		{
			_id = id;
			_path = path;

			_logger = logger;

			_applicationLogic = applicationLogic;
			_server = new ServerBase(_id, IPAddress.Any, transportParams.Port, _logger).Setup();

			_server.StartedEventHandler += OnServerStarted;
			_server.ConnectedEventHandler += OnServerConnected;
			_server.StoppedEventHandler += OnServerStopped;
		}

		public void Start()
		{
			if (!IsStarted) _server.Start();
		}

		public void Stop()
		{
			_server.Stop();
		}

		public void Discard()
		{
			if (IsStarted)
			{
				_server.Stop();
			}
		}

		private void OnServerStarted(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStarted");

			_applicationLogic.Setup(_id, _path);
		}

		private void OnServerStopped(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStopped");

			_applicationLogic.Discard();
		}

		private void OnServerConnected(object sender, ServerPeerSession peerSession)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerConnected");

			_applicationLogic.InitServerPeer(peerSession);
		}
	}
}
