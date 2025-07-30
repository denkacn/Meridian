namespace MeridianServer.TransportLayer.Interfaces
{
    public interface ITransport
    {
        void Start();
        void Stop();
        void Discard();
    }
}