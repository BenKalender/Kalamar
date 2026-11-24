// Tarayıcı istekleri ingress → frontend nginx → gateway yoluyla aynı origin'den geldiği için CORS gerekmez.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");
app.MapReverseProxy();

await app.RunAsync();
