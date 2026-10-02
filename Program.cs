using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DevDen;
using DevDen.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Core DevDen Services
builder.Services.AddScoped<IDataStore, LocalStorageDataStore>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<HotkeyService>();

var host = builder.Build();

// Initialize AppState from storage before running
var store = host.Services.GetRequiredService<IDataStore>();
var state = host.Services.GetRequiredService<AppState>();
var snapshot = await store.LoadAllAsync();
state.Initialize(snapshot);

await host.RunAsync();
