using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Valhalla.TestHost;

public static class TestHost<TEntry>
{
	public static IHostBuilder CreateTestHostBuilder(string? environment = null)
	{
		var entryAssembly = typeof(TEntry).Assembly;

		var deferredHostBuilder = new DeferredHostBuilder();
		deferredHostBuilder.UseEnvironment(environment ?? Environments.Development);

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

	/// <summary>
	/// 一步到位：建立 host builder、（選擇性）覆寫相依、建構並啟動，
	/// 回傳可 <c>await using</c> 的 host。
	/// </summary>
	public static async Task<HostAsyncDisposable> StartAsync(
		Action<IServiceCollection>? configureServices = null,
		string? environment = null,
		CancellationToken cancellationToken = default)
	{
		var builder = CreateTestHostBuilder(environment);

		if (configureServices is not null)
			builder.ConfigureServices(configureServices);

		var host = builder.BuildTestHost();
		await host.StartAsync(cancellationToken).ConfigureAwait(false);

		return host;
	}
}
