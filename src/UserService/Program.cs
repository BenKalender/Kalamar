using Microsoft.Extensions.Http.Resilience;
using UserService.Interfaces;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services
    .AddHttpClient<IPersistenceUserClient, PersistenceUserClient>((sp, client) =>
        client.BaseAddress = GetRequiredUri(sp, "PersistenceService:BaseUrl"))
    .AddStandardResilienceHandler(ConfigureResilience);

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

// Retry, timeout ve circuit breaker. POST/PUT/DELETE tekrar denenmez (mükerrer kayıt riskine karşı).
static void ConfigureResilience(HttpStandardResilienceOptions options) =>
    options.Retry.DisableForUnsafeHttpMethods();
