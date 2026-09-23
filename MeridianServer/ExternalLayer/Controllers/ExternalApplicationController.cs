using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MeridianServer.ExternalLayer.Models;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Server;

namespace MeridianServer.ExternalLayer.Controllers
{
    public static class ExternalApplicationController
    {
        public static IMeridianApplication SearchExternalApplication(string pathToExternalApplicationLib)
        {
            var loadHandle = LoadExternalApplication(
                pathToExternalApplicationLib,
                "legacy",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-layers"));

            return loadHandle.Application;
        }

        public static ExternalApplicationLoadHandle LoadExternalApplication(
            string pathToExternalApplicationLib,
            string layerName,
            string shadowRootDirectory)
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
            var shadowDirectory = CreateShadowDirectory(layerName, shadowRootDirectory);
            string shadowAssemblyPath = null;
            ExternalApplicationLoadContext loadContext = null;

            try
            {
                shadowAssemblyPath = CopyApplicationDirectory(pathToExternalApplicationLib, shadowDirectory);
                loadContext = new ExternalApplicationLoadContext(shadowAssemblyPath);

                var assembly = LoadAssembly(loadContext, shadowAssemblyPath, pathToExternalApplicationLib);
                var definedTypes = GetDefinedTypes(assembly, pathToExternalApplicationLib);

                foreach (var definedType in definedTypes)
                {
                    if (!applicationBaseType.IsAssignableFrom(definedType.AsType())) continue;
                    if (definedType.IsAbstract) continue;

                    var application = CreateApplication(definedType, pathToExternalApplicationLib);
                    return new ExternalApplicationLoadHandle(
                        application,
                        loadContext,
                        pathToExternalApplicationLib,
                        shadowAssemblyPath,
                        shadowDirectory);
                }

                throw new MeridianExternalLogicException(
                    $"External application DLL does not contain a non-abstract {nameof(MeridianApplication)} implementation: {pathToExternalApplicationLib}");
            }
            catch
            {
                loadContext?.Unload();
                TryDeleteDirectory(shadowDirectory);
                throw;
            }
        }

        private static string CreateShadowDirectory(string layerName, string shadowRootDirectory)
        {
            var safeLayerName = string.Join("_", layerName.Split(Path.GetInvalidFileNameChars()));
            var shadowDirectory = Path.Combine(
                shadowRootDirectory,
                safeLayerName,
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(shadowDirectory);
            return shadowDirectory;
        }

        private static string CopyApplicationDirectory(string pathToExternalApplicationLib, string shadowDirectory)
        {
            var sourceDirectory = Path.GetDirectoryName(pathToExternalApplicationLib);
            var fileName = Path.GetFileName(pathToExternalApplicationLib);

            if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(fileName))
            {
                throw new MeridianExternalLogicException($"External application DLL path is invalid: {pathToExternalApplicationLib}");
            }

            try
            {
                foreach (var sourceFile in Directory.GetFiles(sourceDirectory))
                {
                    var targetFile = Path.Combine(shadowDirectory, Path.GetFileName(sourceFile));
                    File.Copy(sourceFile, targetFile, overwrite: true);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new MeridianExternalLogicException(
                    $"External application DLL directory could not be copied to shadow directory: {sourceDirectory} -> {shadowDirectory}", ex);
            }

            return Path.Combine(shadowDirectory, fileName);
        }

        private static Assembly LoadAssembly(
            ExternalApplicationLoadContext loadContext,
            string shadowAssemblyPath,
            string pathToExternalApplicationLib)
        {
            try
            {
                return loadContext.LoadFromAssemblyPath(shadowAssemblyPath);
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

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }

                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }
}
