using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Server;

namespace MeridianServer.ExternalLayer.Controllers
{
    public static class ExternalApplicationController
    {
        public static IMeridianApplication SearchExternalApplication(string pathToExternalApplicationLib)
        {
            if (string.IsNullOrWhiteSpace(pathToExternalApplicationLib))
            {
                throw new MeridianExternalLogicException("External application DLL path is empty.");
            }

            if (!File.Exists(pathToExternalApplicationLib))
            {
                throw new MeridianExternalLogicException($"External application DLL was not found: {pathToExternalApplicationLib}");
            }

            var applicationBaseType = typeof(MeridianApplication);
            var assembly = LoadAssembly(pathToExternalApplicationLib);
            var definedTypes = GetDefinedTypes(assembly, pathToExternalApplicationLib);

            foreach (var definedType in definedTypes)
            {
                if (!applicationBaseType.IsAssignableFrom(definedType.AsType())) continue;
                if (definedType.IsAbstract) continue;

                return CreateApplication(definedType, pathToExternalApplicationLib);
            }

            throw new MeridianExternalLogicException(
                $"External application DLL does not contain a non-abstract {nameof(MeridianApplication)} implementation: {pathToExternalApplicationLib}");
        }

        private static Assembly LoadAssembly(string pathToExternalApplicationLib)
        {
            try
            {
                return Assembly.LoadFrom(pathToExternalApplicationLib);
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException || ex is FileNotFoundException)
            {
                throw new MeridianExternalLogicException($"External application DLL could not be loaded: {pathToExternalApplicationLib}", ex);
            }
        }

        private static TypeInfo[] GetDefinedTypes(Assembly assembly, string pathToExternalApplicationLib)
        {
            try
            {
                return assembly.DefinedTypes.ToArray();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaderErrors = string.Join(Environment.NewLine, ex.LoaderExceptions.Select(e => e.Message));
                throw new MeridianExternalLogicException(
                    $"External application DLL types could not be loaded: {pathToExternalApplicationLib}{Environment.NewLine}{loaderErrors}", ex);
            }
        }

        private static MeridianApplication CreateApplication(TypeInfo definedType, string pathToExternalApplicationLib)
        {
            try
            {
                return (MeridianApplication)Activator.CreateInstance(definedType.AsType());
            }
            catch (Exception ex)
            {
                throw new MeridianExternalLogicException(
                    $"External application type could not be created: {definedType.FullName} from {pathToExternalApplicationLib}", ex);
            }
        }
    }
}
