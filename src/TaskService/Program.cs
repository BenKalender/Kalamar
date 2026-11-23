using Microsoft.Extensions.Http.Resilience;
using TaskService.Interfaces;
using TaskService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services
    .AddHttpClient<IPersistenceTaskClient, PersistenceTaskClient>((sp, client) =>
        client.BaseAddress = GetRequiredUri(sp, "PersistenceService:BaseUrl"))
    .AddStandardResilienceHandler(ConfigureResilience);

// Kullanıcı doğrulaması, kullanıcının beklediği bir isteğin içinde çalışır; bu yüzden
// varsayılandan (3 retry, 30 sn) daha kısa sürede pes edip 503 döneriz.
builder.Services
    .AddHttpClient<IUserServiceClient, UserServiceClient>((sp, client) =>
        client.BaseAddress = GetRequiredUri(sp, "UserService:BaseUrl"))
    .AddStandardResilienceHandler(options =>
    {
        ConfigureResilience(options);
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");

await app.RunAsync();

static Uri GetRequiredUri(IServiceProvider serviceProvider, string key)
{
    var value = serviceProvider.GetRequiredService<IConfiguration>()[key];
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"{key} is missing in configuration.")
        : new Uri(value);
}

// Retry, timeout ve circuit breaker. POST/PUT/DELETE tekrar denenmez; aksi halde bir isteğin
// cevabı kaybolduğunda aynı görev iki kez oluşturulabilir.
static void ConfigureResilience(HttpStandardResilienceOptions options) =>
    options.Retry.DisableForUnsafeHttpMethods();

public partial class Program;
