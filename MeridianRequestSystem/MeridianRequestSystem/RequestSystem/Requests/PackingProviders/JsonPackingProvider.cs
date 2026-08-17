using Newtonsoft.Json;
using System;

namespace MeridianRequestSystem.RequestSystem.Requests.PackingProviders
{
    public class JsonPackingProvider<T> : IFieldPackingProvider
    {
        public virtual object From(object packData)
        {
            if (packData == null) return default(T);
            if (packData is T value) return value;

            var json = packData as string;
            if (json == null)
            {
                throw new InvalidCastException("Json packing data must be a string.");
            }

            var ret = JsonConvert.DeserializeObject<T>(json);
            return ret;
        }

        public object To<TField>(TField fieldData)
        {
            var ret = JsonConvert.SerializeObject(fieldData);
            return ret;
        }
    }
}
