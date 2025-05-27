using BlazorMultiplayerGame.Shared.Hubs;
using BlazorMultiplayerGame.Shared.Services;
using BlazorMultiplayerGame.Web.Components;
using BlazorMultiplayerGame.Web.Services;
using BlazorMultiplayerGame.Shared;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSharedServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapHub<GameHub>("/gamehub");
app.UseResponseCompression();

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorMultiplayerGame.Shared._Imports).Assembly);

app.Run();
