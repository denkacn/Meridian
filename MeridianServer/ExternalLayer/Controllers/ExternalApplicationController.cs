using System;
using System.Reflection;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Server;

namespace MeridianServer.ExternalLayer.Controllers
{
    public static class ExternalApplicationController
    {
        public static IMeridianApplication SearchExternalApplication(string pathToExternalApplicationLib)
        {
            var applicationBaseType = typeof(MeridianApplication);
            var assembly = Assembly.LoadFrom(pathToExternalApplicationLib);

            foreach (var definedType in assembly.DefinedTypes)
            {
                if (definedType.BaseType != applicationBaseType) continue;

                var application = (MeridianApplication)Activator.CreateInstance(definedType);

                if (application != null)
                {
                    return application;
                }
            }

            return null;
        }
    }
}