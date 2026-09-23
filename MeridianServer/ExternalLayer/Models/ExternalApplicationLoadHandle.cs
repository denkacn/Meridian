using System;
using MeridianServerLib.Models.Server;

namespace MeridianServer.ExternalLayer.Models
{
	public sealed class ExternalApplicationLoadHandle : IDisposable
	{
		public MeridianApplication Application { get; private set; }
		public ExternalApplicationLoadContext LoadContext { get; }
		public string SourcePath { get; }
		public string ShadowAssemblyPath { get; }
		public string ShadowDirectory { get; }

		public ExternalApplicationLoadHandle(
			MeridianApplication application,
			ExternalApplicationLoadContext loadContext,
			string sourcePath,
			string shadowAssemblyPath,
			string shadowDirectory)
		{
			Application = application;
			LoadContext = loadContext;
			SourcePath = sourcePath;
			ShadowAssemblyPath = shadowAssemblyPath;
			ShadowDirectory = shadowDirectory;
		}

		public void Dispose()
		{
			Application = null;
			LoadContext.Unload();
		}
	}
}
