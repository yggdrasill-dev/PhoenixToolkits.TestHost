using Microsoft.Extensions.Hosting;

namespace PhoenixToolkits.TestHost;

public readonly struct HostAsyncDisposable(IHost host) : IHost, IAsyncDisposable
{
	public async ValueTask DisposeAsync()
	{
		if (host is IAsyncDisposable disposable)
			await disposable.DisposeAsync().ConfigureAwait(false);
		else
			Dispose();
	}

	public Task StartAsync(CancellationToken cancellationToken = default)
		=> host.StartAsync(cancellationToken);

	public Task StopAsync(CancellationToken cancellationToken = default)
		=> host.StopAsync(cancellationToken);

	public IServiceProvider Services => host.Services;

	public void Dispose()
		=> host.Dispose();
}
