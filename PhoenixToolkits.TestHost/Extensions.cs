using Microsoft.Extensions.Hosting;
using PhoenixToolkits.TestHost;

namespace Valhalla.TestHost;
public static class Extensions
{
	public static HostAsyncDisposable AsAsyncDisposable(this IHost host)
		=> new(host);
}
