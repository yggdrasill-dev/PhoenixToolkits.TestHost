# PhoenixToolkits.TestHost

為 **非 Web 的 .NET Generic Host** 專案打造的整合測試輔助套件——把 `Microsoft.AspNetCore.Mvc.Testing` 中 `WebApplicationFactory<TEntry>` 的核心機制（執行目標專案的進入點、攔截 host 建構過程）抽出來，但**不強制套用 `TestServer`**，讓 Worker、gRPC、背景服務、訊息佇列 consumer（NATS / MassTransit 等）這類沒有 HTTP 端點的 Generic Host 也能享受同樣的整合測試啟動方式。

`WebApplicationFactory<TEntry>` 會要求應用程式是 Web app（需要 `IWebHostBuilder`／`WebApplication`）並塞入 in-memory `TestServer`；如果你的 `Program.cs` 只是一個 `Host.CreateApplicationBuilder(...)` 的 Generic Host，就用不上它。本套件正是為此而生。

## 特色

- 用真實的 `Program` 進入點建構 host，測試與正式啟動路徑一致
- 可自由 `ConfigureServices` 覆寫相依、替換測試替身（test double）
- 延後 `StartAsync`，由測試決定何時啟動
- 不依賴 ASP.NET Core，只相依 `Microsoft.Extensions.Hosting`
- 多目標支援 **net8.0 / net9.0 / net10.0**

所有公開型別皆位於 `Valhalla.TestHost` namespace，只要一個 `using` 即可。

## 安裝

```bash
dotnet add package PhoenixToolkits.TestHost
```

## 快速開始

假設待測專案的進入點是 `Program`。最短寫法用一步到位的 `StartAsync`：

```csharp
using Valhalla.TestHost;
using Microsoft.Extensions.DependencyInjection;

await using var host = await TestHost<Program>.StartAsync(
    configureServices: services =>
    {
        services.AddSingleton<IClock, FakeClock>();
    });

var worker = host.Services.GetRequiredService<IMyWorker>();
// ...assertions...
```

`StartAsync` 會建立 builder、套用你的 `configureServices`、建構並啟動 host，回傳可 `await using` 的 host。它還有兩個可選參數：`environment`（預設 `Development`）與 `cancellationToken`。

### 需要更多控制時

若要對 `IHostBuilder` 做 `ConfigureServices` 以外的設定，改用 `CreateTestHostBuilder()` + `BuildTestHost()`：

```csharp
using Valhalla.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = TestHost<Program>.CreateTestHostBuilder(environment: "Staging")
    .ConfigureAppConfiguration(config => config.AddInMemoryCollection(/* ... */))
    .ConfigureServices(services => services.AddSingleton<IClock, FakeClock>())
    .BuildTestHost();          // ← 用這個，不要用 Build()

await using var _ = host;
await host.StartAsync();

var worker = host.Services.GetRequiredService<IMyWorker>();
```

### 為什麼是 `BuildTestHost()` 而不是 `Build()`

`IHostBuilder.Build()` 回傳的靜態型別是 `IHost`，而 `IHost` 只實作 `IDisposable`；直接 `using var host = builder.Build();` 會走**同步**釋放（背景服務、連線池等資源可能沒被 async 收乾淨），而 `await using` 則根本編不過。`BuildTestHost()` 把 host 包成同時實作 `IAsyncDisposable` 的 `HostAsyncDisposable`，`await using` 才能正確以 `DisposeAsync()` 收尾。若你手上已經是一個 `IHost`，也可以用 `host.AsAsyncDisposable()` 達到相同效果。

## `Program` 的存取性

跟 `WebApplicationFactory` 一樣，若待測專案使用 **top-level statements**，編譯器產生的 `Program` 類別是 `internal` 的，測試專案取用不到。請在待測專案補上一行：

```csharp
// 待測專案 Program.cs 結尾
public partial class Program;
```

或改用 `[assembly: InternalsVisibleTo("你的測試組件名稱")]`。

`TestHost<TEntry>` 只用 `typeof(TEntry).Assembly` 取得待測組件，所以 `TEntry` 不一定要是 `Program`——待測組件裡任何 `public` 型別都可以。

## 搭配 xUnit 的 Fixture 範例

```csharp
public sealed class HostFixture : IAsyncLifetime
{
    private HostAsyncDisposable _host;

    public IServiceProvider Services => _host.Services;

    public async Task InitializeAsync()
        => _host = await TestHost<Program>.StartAsync(
            configureServices: services =>
            {
                services.AddSingleton<IClock, FakeClock>();
            });

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        await _host.DisposeAsync();
    }
}
```

## API 一覽

| 成員 | 說明 |
|---|---|
| `TestHost<TEntry>.StartAsync(configureServices?, environment?, cancellationToken?)` | 一步到位：建立、覆寫相依、建構並啟動，回傳 `HostAsyncDisposable` |
| `TestHost<TEntry>.CreateTestHostBuilder(environment?)` | 取得 `IHostBuilder`，可做完整設定 |
| `IHostBuilder.BuildTestHost()` | 建構並包成 `HostAsyncDisposable`（取代 `Build()`） |
| `IHost.AsAsyncDisposable()` | 將既有 `IHost` 包成 `HostAsyncDisposable` |

## 運作原理

`CreateTestHostBuilder()` 內部：

1. 建立一個 `DeferredHostBuilder`——先把所有 `ConfigureServices` / `ConfigureAppConfiguration` 等呼叫錄下來。
2. 透過 `HostFactoryResolver` 在背景執行緒執行待測組件的進入點，並訂閱 `Microsoft.Extensions.Hosting` 的 `DiagnosticListener`。
3. 攔截到 `HostBuilding` 事件時，把步驟 1 錄下的設定「重播」到真正的 host builder 上；攔截到 `HostBuilt` 後取回建好的 `IHost`，並中止進入點繼續往下跑（避免真的執行 `host.Run()`）。
4. 回傳的 host 延後 `StartAsync`，交由測試控制啟動時機。

## 來源與致謝

`DeferredHostBuilder` 與 `HostFactoryResolver` 移植自 [.NET Foundation](https://github.com/dotnet/aspnetcore) 的 `Microsoft.AspNetCore.Mvc.Testing` 與 `dotnet/runtime`（MIT License），檔案內保留原始授權標頭。本套件在其基礎上移除 Web 專屬部分，改為對外提供純 `IHostBuilder`。

## 授權

見 [LICENSE.txt](LICENSE.txt)。
