# EXE.Epaas.Signature - 职行力开放平台 C# SDK

---

## 概述

`EXE.Epaas.Signature` 是职行力开放平台设计的 .NET SDK，旨在简化与平台 API 的集成。它封装了复杂的签名认证、AccessToken 管理和 HTTP 请求逻辑，使开发者可以专注于业务功能的实现。

该 SDK 被设计为现代、健壮且易于使用，特别适合在 ASP.NET Core 等支持依赖注入的环境中使用。

## 功能特性

- **完整的认证封装**: 自动处理 V3 版本的签名算法和 AccessToken 的获取与刷新。
- **依赖注入友好**: 可以轻松集成到 ASP.NET Core 的服务容器中，推荐使用 `IHttpClientFactory` 进行管理。
- **可插拔的 AccessToken 缓存**: 内置了基于内存的 AccessToken 缓存，同时定义了 `IAccessTokenCache` 接口，允许开发者实现自己的分布式缓存策略（如 Redis、Memcached）。
- **线程安全**: 内置的 AccessToken 管理机制是线程安全的，可以安全地在多线程环境（如 Web 应用）中作为单例使用。
- **配置化**: 所有关键参数（如 BaseUri, AccessId, SecretKey）都可通过 `ApiClientOptions` 类进行配置，并能与 `appsettings.json` 无缝集成。
- **超时控制与取消令牌**: 支持配置全局 HTTP 超时，并在 API 调用层面支持 `CancellationToken`。
- **清晰的异常处理**: 在 HTTP 请求失败时会抛出明确的异常。

## 快速入门

### 1. 安装 NuGet 包

在您的项目目录中，通过 .NET CLI 安装 SDK：

```bash
dotnet add package EXE.Epaas.Signature
```

或者，在 Visual Studio 的 NuGet 包管理器中搜索 `EXE.Epaas.Signature` 并安装。

### 2. 配置

在您的 `appsettings.json` 文件中，添加如下配置节：

```json
{
  "EpaasApi": {
    "BaseUri": "https://your-api-endpoint.com",
    "AccessId": "YOUR_ACCESS_ID",
    "SecretKey": "YOUR_SECRET_KEY",
    "TimeoutSeconds": 30
  }
}
```

### 3. 在 ASP.NET Core 中注册服务

在您的 `Program.cs` (或 `Startup.cs`) 文件中，注册 `ApiClient`。

```csharp
using EXE.Epaas.Signature;

var builder = WebApplication.CreateBuilder(args);

// 1. 从 appsettings.json 加载配置
var apiOptionsSection = builder.Configuration.GetSection("EpaasApi");
builder.Services.Configure<ApiClientOptions>(apiOptionsSection);

// 2. 注册 ApiClient 和 HttpClient
// 这会利用 IHttpClientFactory 的优势，如连接池和生命周期管理
// SDK 内置的 InMemoryAccessTokenCache 已实现为单例，无需额外注册即可在 ApiClient 中共享缓存。
builder.Services.AddHttpClient<ApiClient>();

// 3. (可选) 如果您实现了自定义的分布式缓存，在这里注册为单例
// builder.Services.AddSingleton<IAccessTokenCache, MyCustomDistributedCache>();

var app = builder.Build();
```

### 4. 使用 ApiClient

通过依赖注入获取 `ApiClient` 实例并发起请求。

```csharp
[ApiController]
[Route("[controller]")]
public class MyController : ControllerBase
{
    private readonly ApiClient _apiClient;

    public MyController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpPost("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var requestUri = "your api endpoint";
            var requestBody = @"{}";
            var customHeaders = new Dictionary<string, string>
            {
                { "x-tenant-id", "your tenant id" }
            };

            string response = await _apiClient.PostAsync(requestUri, requestBody, customHeaders);
            
            // 假设响应是 JSON，直接返回
            return Content(response, "application/json");
        }
        catch (Exception ex)
        {
            return Problem(ex.Message);
        }
    }
}
```

## 高级用法

### 自定义 AccessToken 缓存

SDK 默认使用一个线程安全的内存缓存 (`InMemoryAccessTokenCache`)，该缓存被实现为单例模式，足以应对单个应用实例内的所有请求。如果您需要跨多个服务器实例共享 Token（例如在负载均衡环境中），或者需要将 Token 持久化，可以实现自己的分布式缓存。

**1. 实现 `IAccessTokenCache` 接口**

您可以创建一个类来实现自己的缓存逻辑，例如使用 Redis。

```csharp
// MyRedisCache.cs
using StackExchange.Redis;
using System.Threading.Tasks;

public class MyRedisCache : IAccessTokenCache
{
    private readonly IDatabase _database;

    public MyRedisCache(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.StringGetAsync(key);
    }

    public async Task SetAsync(string key, string token, int expiresInSeconds, CancellationToken cancellationToken = default)
    {
        await _database.StringSetAsync(key, token, TimeSpan.FromSeconds(expiresInSeconds));
    }
}
```

**2. 注册自定义缓存**

在 `Program.cs` 中，将您的实现注册为 `IAccessTokenCache` 的单例服务。这会自动替换掉默认的内存缓存。

```csharp
// 假设您已经配置了 Redis 连接
// builder.Services.AddSingleton<IConnectionMultiplexer>(...);

// 注册您的自定义缓存为单例
builder.Services.AddSingleton<IAccessTokenCache, MyRedisCache>();

// 注册 ApiClient 时，它会自动通过依赖注入找到并使用 MyRedisCache
builder.Services.AddHttpClient<ApiClient>();
```

### 在非依赖注入环境中使用

如果您在控制台应用等没有依赖注入容器的环境中使用，可以直接实例化 `ApiClient`。

```csharp
using EXE.Epaas.Signature;

// 1. 手动创建配置
var options = new ApiClientOptions
{
    BaseUri = "https://...",
    AccessId = "...",
    SecretKey = "...",
    TimeoutSeconds = 30
};

// 2. 实例化 ApiClient
// 它会默认使用内置的单例内存缓存。它也会内部管理一个 HttpClient。
var apiClient = new ApiClient(options);

// 如果需要，也可以传入自定义的缓存实例
// var myCache = new MyCustomCache();
// var apiClientWithCustomCache = new ApiClient(options, myCache);

// 3. 使用客户端
try
{
    string response = await apiClient.PostAsync(...);
    Console.WriteLine(response);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// 4. 释放资源
apiClient.Dispose();
```

## 示例项目

本项目包含一个开箱即用的示例项目 `EXE.Epaas.Signature.Examples`，它演示了如何在 ASP.NET Core Minimal API 中使用本 SDK。

### 如何运行示例

1.  **配置凭证**: 打开 `EXE.Epaas.Signature.Examples/appsettings.json`，将占位符替换为您的真实 `BaseUri`, `AccessId` 和 `SecretKey`。
2.  **设置启动项目**: 在 Visual Studio 中，将 `EXE.Epaas.Signature.Examples` 设置为启动项目。
3.  **运行**: 直接按 F5 或执行 `dotnet run`。应用启动后，访问 `/swagger` 即可看到交互式 API 文档并进行测试。

---
