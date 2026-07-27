# PhoenixToolkits.TestHost

為 **非 Web 的 .NET Generic Host** 專案打造的整合測試輔助套件——把 `Microsoft.AspNetCore.Mvc.Testing` 中 `WebApplicationFactory<TEntry>` 的核心機制（執行目標專案的進入點、攔截 host 建構過程）抽出來，但**不強制套用 `TestServer`**，讓 Worker、gRPC、背景服務、訊息佇列 consumer（NATS / MassTransit 等）這類沒有 HTTP 端點的 Generic Host 也能享受同樣的整合測試啟動方式。

`WebApplicationFactory<TEntry>` 會要求應用程式是 Web app（需要 `IWebHostBuilder`／`WebApplication`）並塞入 in-memory `TestServer`；如果你的 `Program.cs` 只是一個 `Host.CreateApplicationBuilder(...)` 的 Generic Host，就用不上它。本套件正是為此而生。

## 特色

- 直接回傳 `IHostBuilder`，可自由 `ConfigureServices` 覆寫相依、替換測試替身（test double）
- 用真實的 `Program` 進入點建構 host，測試與正式啟動路徑一致
- 延後 `StartAsync`，由測試決定何時啟動
- 不依賴 ASP.NET Core，只相依 `Microsoft.Extensions.Hosting`
- 多目標支援 **net8.0 / net9.0 / net10.0**

## 安裝

```bash
dotnet add package PhoenixToolkits.TestHost
```

## 快速開始

假設待測專案的進入點是 `Program`：

```csharp
using PhoenixToolkits.TestHost;   // TestHost<TEntry>
using Valhalla.TestHost;          // AsAsyncDisposable() 擴充方法
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 1. 取得 IHostBuilder（環境預設為 Development）
var builder = TestHost<Program>.CreateTestHostBuilder();

// 2. 覆寫相依，換成測試替身
builder.ConfigureServices(services =>
{
    services.AddSingleton<IClock, FakeClock>();
});

// 3. 建構並啟動；用 AsAsyncDisposable() 讓 await using 走非同步釋放
await using var host = builder.Build().AsAsyncDisposable();
await host.StartAsync();

// 4. 從容器解析服務進行驗證
var worker = host.Services.GetRequiredService<IMyWorker>();
// ...assertions...

await host.StopAsync();
```

### 為什麼需要 `AsAsyncDisposable()`

`Build()` 回傳的靜態型別是 `IHost`，而 `IHost` 只實作 `IDisposable`；直接對它 `await using` 不會走到非同步釋放。`AsAsyncDisposable()` 把 host 包成同時實作 `IAsyncDisposable` 的 `HostAsyncDisposable`，`await using` 才能正確以 `DisposeAsync()` 收尾（背景服務、連線池等資源需要）。

> 兩個公開型別的 namespace 不同：`TestHost<TEntry>` 與 `HostAsyncDisposable` 在 `PhoenixToolkits.TestHost`，擴充方法 `AsAsyncDisposable()` 在 `Valhalla.TestHost`。使用時兩個 `using` 都要加。

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
    {
        _host = TestHost<Program>.CreateTestHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IClock, FakeClock>();
            })
            .Build()
            .AsAsyncDisposable();

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        await _host.DisposeAsync();
    }
}
```

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
