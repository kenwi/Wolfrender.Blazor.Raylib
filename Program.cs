#if CONSOLE_APP
using Game;

var app = new Application();
app.Run();
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