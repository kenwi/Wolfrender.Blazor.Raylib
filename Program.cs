#if CONSOLE_APP
using Game.Core;

try
{
    var app = new Application();
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FATAL] {ex.Message}");
    return 1;
}
#else
using Wolfrender.Blazor.Raylib;
using Wolfrender.Blazor.Raylib.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddRaylibServices();

await builder.Build().RunAsync();
#endif