using Newtonsoft.Json;
using System.IO;

namespace MeridianServer.ExternalLayer.ResourcesLoader
{
    public static class DataLoader
    {
        public static T Load<T>(string path)
        {
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
