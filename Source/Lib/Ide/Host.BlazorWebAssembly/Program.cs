using Clair.Common.RazorLib.Installations.Models;
using Clair.Ide.Wasm;
using Clair.Website.RazorLib;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var hostingInformation = new ClairHostingInformation(
    ClairHostingKind.Wasm,
    ClairPurposeKind.Ide);

builder.Services.AddClairWebsiteServices(hostingInformation);

await builder.Build().RunAsync();
