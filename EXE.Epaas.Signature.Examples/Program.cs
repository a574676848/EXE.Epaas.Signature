using EXE.Epaas.Signature;

var builder = WebApplication.CreateBuilder(args);

// 1. 从 appsettings.json 加载配置
var apiOptionsSection = builder.Configuration.GetSection("EpaasApi");
builder.Services.Configure<ApiClientOptions>(apiOptionsSection);

// 2. 注册 ApiClient 和 HttpClient
builder.Services.AddHttpClient<ApiClient>();

// 添加 Swagger/OpenAPI (可选, 但推荐用于 API 测试)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 配置 Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Redirect("/swagger"));

// --- Web API 示例端点 ---
app.MapPost("/api/test", async (ApiClient apiClient) =>
{
    try
    {
        var requestUri = "业务api";
        var requestBody = @"{}";

        var customHeaders = new Dictionary<string, string>
        {
            { "x-tenant-id", "your tenant id" },
        };

        string response = await apiClient.PostAsync(requestUri, requestBody, headers: customHeaders);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("TestUserPagination")
.WithOpenApi();

// --- 控制台应用程序入口点 ---
if (args.Contains("--run-console-demo"))
{
    Console.WriteLine("\n--- 运行控制台 Demo ---");
    var apiClient = app.Services.GetRequiredService<ApiClient>();
    try
    {
        var requestUri = "业务api"; 
        var requestBody = @"{}";

        var customHeaders = new Dictionary<string, string> { { "x-tenant-id", "your tenant id" } };

        Console.WriteLine("正在调用 API...");
        string response = await apiClient.PostAsync(requestUri, requestBody, headers: customHeaders);
        Console.WriteLine("API 响应:");
        Console.WriteLine(response);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"发生错误: {ex.Message}");
        Console.ResetColor();
    }
    return; // 运行完控制台模式后直接退出
}

// 启动 Web API
app.Run();
