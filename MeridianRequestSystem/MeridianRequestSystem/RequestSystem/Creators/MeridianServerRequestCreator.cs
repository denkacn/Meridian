using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Constants;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianRequestSystem.RequestSystem.Utilities;

namespace MeridianRequestSystem.RequestSystem.Creators
{
    public class MeridianServerRequestCreator : IServerRequestCreator
    {
        private readonly Dictionary<int, Type> _requestMap;
        private readonly IRequestDependencyInjector _dependencyInjector;

        public MeridianServerRequestCreator(
            Assembly requestsAssembly,
            IRequestDependencyInjector dependencyInjector = null)
        {
            if (requestsAssembly == null) throw new ArgumentNullException(nameof(requestsAssembly));

            _dependencyInjector = dependencyInjector;
            _requestMap = MapRequests(requestsAssembly);
        }

        public IServerDataNetworkRequest CreateServerDataNetworkRequest(Dictionary<byte, object> package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (!package.TryGetValue(PacketFields.RequestCode, out var value)) return null;

            var requestCode = Convert.ToInt32(value);
            if (!_requestMap.TryGetValue(requestCode, out var requestType)) return null;

            var request = (IServerDataNetworkRequest)Activator.CreateInstance(requestType);
            request.Map(package);

            return _dependencyInjector?.Inject(request) ?? request;
        }

        private static Dictionary<int, Type> MapRequests(Assembly assembly)
        {
            var requestMap = new Dictionary<int, Type>();

            var requestTypes = assembly
                .GetTypes()
                .Where(type =>
                    !type.IsAbstract &&
                    typeof(IServerDataNetworkRequest).IsAssignableFrom(type) &&
                    IsServerRequestType(type));

            foreach (var requestType in requestTypes)
            {
                var requestCode = GetRequestCode(requestType);
                if (requestMap.TryGetValue(requestCode, out var existingType))
                {
                    throw new InvalidOperationException(
                        "Duplicate server request code " + requestCode +
                        " for " + existingType.FullName +
                        " and " + requestType.FullName + ".");
                }

                requestMap.Add(requestCode, requestType);
            }

            return requestMap;
        }

        private static bool IsServerRequestType(Type type)
        {
            return type.GetCustomAttribute<ServerRequestAttribute>() != null ||
                   Attribute.IsDefined(type, typeof(RequestBaseAttribute));
        }

        private static int GetRequestCode(Type serverRequestType)
        {
            var attribute = serverRequestType.GetCustomAttribute<ServerRequestAttribute>();
            var requestName = attribute?.RequestType.Name ?? serverRequestType.Name.Replace("Server", "");

            return GetRealHashCode.FromString(requestName);
        }
    }
}
