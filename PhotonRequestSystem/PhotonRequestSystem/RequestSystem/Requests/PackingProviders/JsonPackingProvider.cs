using Newtonsoft.Json;

namespace PhotonRequestSystem.RequestSystem.Requests.PackingProviders
{
    public class JsonPackingProvider<T> : IFieldPackingProvider
    {
        public virtual object From(object packData)
        {
            var ret = JsonConvert.DeserializeObject<T>((string) packData);
            return ret;
        }

        public object To<T>(T fieldData)
        {
            var ret = JsonConvert.SerializeObject(fieldData);
            return ret;
        }
    }
}
