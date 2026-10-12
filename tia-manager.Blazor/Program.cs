using tia_manager.Blazor.Components;
using tia_manager.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<RepositoryApiClient>((services, client) =>
{
    var baseAddress = services.GetRequiredService<IConfiguration>()["BffApi:BaseUrl"];
    if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException("Configure BffApi:BaseUrl with an absolute URL.");
    }

    client.BaseAddress = uri;
});
builder.Services.AddHttpClient<TechnicalAssetApiClient>((services, client) =>
{
    var baseAddress = services.GetRequiredService<IConfiguration>()["TechnicalImpactApi:BaseUrl"];
    if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException("Configure TechnicalImpactApi:BaseUrl with an absolute URL.");
    }

    client.BaseAddress = uri;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
