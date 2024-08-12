using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PhoenixToolkits.TestHost;

public static class TestHost<TEntry>
{
	public static IHostBuilder CreateTestHostBuilder()
	{
		var entryAssembly = typeof(TEntry).Assembly;

		var deferredHostBuilder = new DeferredHostBuilder();
		deferredHostBuilder.UseEnvironment(Environments.Development);

		// There's no helper for UseApplicationName, but we need to
		// set the application name to the target entry point
		// assembly name.
		deferredHostBuilder.ConfigureHostConfiguration(config => config
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ HostDefaults.ApplicationKey, entryAssembly.GetName()?.Name ?? string.Empty }
			}));

		var factory = HostFactoryResolver.ResolveHostFactory(
			entryAssembly,
			stopApplication: false,
			configureHostBuilder: deferredHostBuilder.ConfigureHostBuilder,
			entrypointCompleted: deferredHostBuilder.EntryPointCompleted);

		deferredHostBuilder.SetHostFactory(factory!);

		return deferredHostBuilder;
	}
}
