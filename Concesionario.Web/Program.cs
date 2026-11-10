using Concesionario.Web.Components;
using Concesionario.WEB.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Debe ir ANTES de builder.Build()
// El puerto debe coincidir con el del API (http 5100 / https 7000)
builder.Services.AddHttpClient<ConcesionarioApiClient>(c =>
    c.BaseAddress = new Uri("http://localhost:5100/"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
