using System.Threading.Tasks;

namespace MeridianServer.TransportLayer.ApplicationProvider
{
	public interface IApplicationProvider
	{
		bool IsStarted { get; }
		string Id { get; }
		void Start();
		void Stop();
		Task ReloadAsync();
		void Discard();
	}
}
