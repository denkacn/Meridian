using System.Reflection;
using System.Runtime.Loader;

namespace MeridianServer.ExternalLayer.Models
{
	public class ExternalApplicationLoadContext : AssemblyLoadContext
	{
		private readonly AssemblyDependencyResolver _resolver;

		public ExternalApplicationLoadContext(string mainAssemblyPath) : base(isCollectible: true)
		{
			_resolver = new AssemblyDependencyResolver(mainAssemblyPath);
		}

		protected override Assembly Load(AssemblyName assemblyName)
		{
			if (assemblyName.Name == "MeridianServerLib")
			{
				return Assembly.Load(assemblyName);
			}

			var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
			return assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath);
		}

		protected override nint LoadUnmanagedDll(string unmanagedDllName)
		{
			var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
			return libraryPath == null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
		}
	}
}
