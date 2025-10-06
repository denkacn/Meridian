using MeridianServerLib.Interfaces.Client;

namespace MeridianMasterClientRunner.Network.Client
{
	public interface IMeridianClient
	{
		void CreateSession(string address, int port, IOperationsReceiver operationsReceiver);
		void DiscardSession();
		void Send(byte code, Dictionary<byte, object> package);
	}
}
