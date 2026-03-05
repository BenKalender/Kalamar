using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayBaseUrl = builder.Configuration["ApiGatewayBaseUrl"];
Uri gatewayUri;

if (string.IsNullOrWhiteSpace(gatewayBaseUrl) || gatewayBaseUrl.StartsWith('/'))
{
    gatewayUri = new Uri(builder.HostEnvironment.BaseAddress);
}
else if (Uri.TryCreate(gatewayBaseUrl, UriKind.Absolute, out var configuredUri)
    && (configuredUri.Scheme == Uri.UriSchemeHttp || configuredUri.Scheme == Uri.UriSchemeHttps))
{
    gatewayUri = configuredUri;
}
else
{
    gatewayUri = new Uri(builder.HostEnvironment.BaseAddress);
}

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = gatewayUri
});

await builder.Build().RunAsync();
