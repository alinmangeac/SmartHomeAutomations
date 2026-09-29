using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartHomeAutomations.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var hasApiBaseUrl = Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri);
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = hasApiBaseUrl
        ? new Uri(apiUri!.AbsoluteUri.TrimEnd('/') + "/")
        : new Uri(builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
