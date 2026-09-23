using System.Threading.Tasks;

namespace MeridianServer.TransportLayer.Interfaces
{
	public interface ITransport
	{
		void Start();
		void Stop();
		Task ReloadApplicationAsync(string id);
		void Discard();
	}
}
