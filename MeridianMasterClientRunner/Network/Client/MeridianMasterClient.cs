using MeridianMasterClientRunner.Services;
using MeridianMasterClientRunnerCommon;
using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.Models.Client;
using MeridianServerLib.Models.Operations;
using Newtonsoft.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MeridianMasterClientRunner.Network.Client
{
	public class MeridianMasterClient : IMeridianMasterClient, IOperationsReceiver
	{
		public const int OC_MCR = 97;

		private readonly ISettingsService _settingsService;
		private readonly IAppControlService _appControlService;

		private string _uid;
		private MeridianClient _meridianClient;

		public MeridianMasterClient(IAppControlService appControlService, ISettingsService settingsService)
		{
			_appControlService = appControlService;
			_settingsService = settingsService;

			ConnectToServer();
		}

		private void ConnectToServer()
		{
			_meridianClient = new MeridianClient(this);
			_meridianClient.ServerStatusChanged += OnServerStatusChanged;

			var serverIp = _settingsService.Configuration.ServerIp;
			var serverPort = _settingsService.Configuration.ServerPort;

			_meridianClient.CreateSession(serverIp, serverPort, this);
		}

		private void OnServerStatusChanged(NetworkClientConnectionStatus status)
		{
			if (status == NetworkClientConnectionStatus.Connected)
			{
				Console.WriteLine("Try Registration MCR");

				var registrationParameters = new Dictionary<byte, object>
				{
					{ 1, McrNetworkCommandType.MCR_REGISTR },
					{ 2, _settingsService.Configuration.MrcId }
				};

				_meridianClient.Send(OC_MCR, registrationParameters);
			}
			else if (status == NetworkClientConnectionStatus.Disconnected)
			{
				_meridianClient.ServerStatusChanged -= OnServerStatusChanged;
				_meridianClient.DiscardSession();
				ConnectToServer();
			}
		}

		public void OnOperationReceived(OperationData operation)
		{
			if (operation.OperationCode != OC_MCR || !operation.Parameters.TryGetValue(1, out var operationParameter)) return;

			var commandType = (McrNetworkCommandType)operationParameter;

			switch (commandType)
			{
				case McrNetworkCommandType.MCR_REGISTR:
					_uid = (string)operation.Parameters[2];
					Console.WriteLine("Registration Complete UID: " + _uid);
					break;

				case McrNetworkCommandType.MCR_CREATE:
					var masterPath = (string)operation.Parameters[2];
					var version = (string)operation.Parameters[3];
					_appControlService.RunMasterClient(masterPath, version, _settingsService.Configuration.IsBatched);
					break;
			}
		}

		public void SendStatus(McrStatusData mcrStatusData)
		{
			var statusBytes = StringCompressor.CompressString(JsonConvert.SerializeObject(mcrStatusData));

			var registrationParameters = new Dictionary<byte, object>
			{
				{ 1, McrNetworkCommandType.MCR_REGISTR },
				{ 2, _uid },
				{ 3, statusBytes }
			};

			_meridianClient.Send(OC_MCR, registrationParameters);
		}

		public void Send(int operationCodes, Dictionary<byte, object> parameters)
		{
			_meridianClient.Send((byte)operationCodes, parameters);
		}
	}
}
