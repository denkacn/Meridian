
namespace MeridianServer.TransportLayer.ApplicationProvider
{
	public interface IApplicationProvider
	{
		bool IsStarted { get; }
		void Start();
		void Stop();
		void Discard();
	}
}
