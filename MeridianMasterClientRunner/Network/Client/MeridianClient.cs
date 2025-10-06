using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.Models.Client;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;

namespace MeridianMasterClientRunner.Network.Client
{
	public class MeridianClient : IMeridianClient, ILogger
	{
		private IClientPeer _client;
		private IOperationsReceiver _operationsReceiver;

		public event Action<NetworkClientConnectionStatus> ServerStatusChanged;

		public bool IsConnect => _client.IsConnected;

		public MeridianClient(IOperationsReceiver operationsReceiver)
		{
			_operationsReceiver = operationsReceiver ?? throw new ArgumentNullException(nameof(operationsReceiver));
		}

		public void CreateSession(string address, int port, IOperationsReceiver operationsReceiver)
		{
			if (_client != null) throw new InvalidOperationException("Сессия уже создана.");

			_client = new ClientPeer(address, port, this);
			_client.SetOperationsReceiver(operationsReceiver);
			_client.ClientConnectionStatusChanged += OnClientConnectionStatusChanged;
			_client.Connect();
		}

		public void DiscardSession()
		{
			if (_client == null) return;

			_client.Disconnect();
			_client.Discard();

			_client.ClientConnectionStatusChanged -= OnClientConnectionStatusChanged;
			_client = null;
		}

		public void Send(byte code, Dictionary<byte, object> package)
		{
			if (_client == null) return;
			if (!_client.IsConnected) return;

			var operation = new OperationData(code, package);

			_client.Send(operation);
		}

		public void Log(string message) => Console.WriteLine(message);

		public void LogError(string message, Exception exception)
			=> Console.Error.WriteLine($"Error: {message}\n{exception}");

		private void OnClientConnectionStatusChanged(NetworkClientConnectionStatus status)
			=> ServerStatusChanged?.Invoke(status);
	}
}
