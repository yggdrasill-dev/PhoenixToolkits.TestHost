using Microsoft.Extensions.Hosting;

namespace Valhalla.TestHost;

public static class Extensions
{
	/// <summary>
	/// 建構測試用 host，並包成同時實作 <see cref="IAsyncDisposable"/> 的型別，
	/// 讓 <c>await using</c> 能正確走非同步釋放。建議取代直接呼叫 <c>Build()</c>。
	/// </summary>
	public static HostAsyncDisposable BuildTestHost(this IHostBuilder builder)
		=> new(builder.Build());

	/// <summary>
	/// 將既有的 <see cref="IHost"/> 包成同時實作 <see cref="IAsyncDisposable"/> 的型別。
	/// </summary>
	public static HostAsyncDisposable AsAsyncDisposable(this IHost host)
		=> new(host);
}
