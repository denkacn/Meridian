
namespace PhotonRequestSystem.RequestSystem.Requests.PackingProviders
{
    public interface IFieldPackingProvider
    {
        object To<T>(T fieldData);
        object From(object packData);
    }
}
