namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IRequestDependencyInjector
    {
        IServerDataNetworkRequest Inject(IServerDataNetworkRequest request);
    }
}
