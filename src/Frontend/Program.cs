using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayBaseUrl = builder.Configuration["ApiGatewayBaseUrl"];
if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
{
    gatewayBaseUrl = builder.HostEnvironment.BaseAddress;
}

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(gatewayBaseUrl)
});

await builder.Build().RunAsync();
