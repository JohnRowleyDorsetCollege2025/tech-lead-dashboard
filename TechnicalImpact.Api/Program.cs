using TechnicalImpact.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
// Startup snapshot: edit Data/assets.json then restart to reload.
var configuredPath = builder.Configuration["Catalogue:Path"] ?? "Data/assets.json";
var store = new CatalogueStore(Path.IsPathRooted(configuredPath)
    ? configuredPath : Path.Combine(builder.Environment.ContentRootPath, configuredPath));
builder.Services.AddSingleton(store);
var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", assetCount = store.Assets.Count }));
var api = app.MapGroup("/api");
api.MapGet("/assets", (string? search, string? assetType, string? ownerTeam, string? environment) =>
{
    if (environment is not null && !CatalogueStore.ValidEnvironment(environment)) return BadEnvironment();
    var assets = store.Assets.Where(a =>
        (search is null || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || a.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
        (assetType is null || CatalogueStore.Same(a.AssetType, assetType)) &&
        (ownerTeam is null || CatalogueStore.Same(a.OwnerTeam, ownerTeam)) &&
        (environment is null || a.Deployments.Any(d => CatalogueStore.Same(d.Environment, environment))));
    return Results.Ok(assets);
});
api.MapGet("/assets/{id}", (string id) => store.Find(id) is { } asset ? Results.Ok(asset) : Results.NotFound());
api.MapGet("/relationships", (string? environment) =>
    environment is not null && !CatalogueStore.ValidEnvironment(environment)
        ? BadEnvironment() : Results.Ok(store.Edges(environment)));
api.MapGet("/assets/{id}/relationships", (string id, string? environment) =>
{
    if (store.Find(id) is null) return Results.NotFound();
    if (environment is not null && !CatalogueStore.ValidEnvironment(environment)) return BadEnvironment();
    var edges = store.Edges(environment).ToList();
    return Results.Ok(new {
        assetId = id,
        outgoing = edges.Where(r => r.SourceAssetId == id).Select(r => new { relationship = r, asset = store.Find(r.TargetAssetId) }),
        incoming = edges.Where(r => r.TargetAssetId == id).Select(r => new { relationship = r, asset = store.Find(r.SourceAssetId) })
    });
});
api.MapGet("/graph", (string? environment) =>
{
    if (environment is not null && !CatalogueStore.ValidEnvironment(environment)) return BadEnvironment();
    return Results.Ok(new {
        nodes = store.Assets.Where(a => environment is null || a.Deployments.Any(d => CatalogueStore.Same(d.Environment, environment))),
        edges = store.Edges(environment)
    });
});
api.MapGet("/assets/{id}/impact", (string id, string? direction, string? environment, int? maxDepth) =>
{
    if (store.Find(id) is null) return Results.NotFound();
    if (environment is not null && !CatalogueStore.ValidEnvironment(environment)) return BadEnvironment();
    direction = direction?.ToLowerInvariant() ?? "dependents";
    if (direction != "dependents" && direction != "dependencies")
        return Results.Problem(statusCode: 400, detail: "direction must be dependents or dependencies.");
    var depth = maxDepth ?? 10;
    if (depth < 1 || depth > 50) return Results.Problem(statusCode: 400, detail: "maxDepth must be 1..50.");
    var affected = store.Traverse(id, direction, environment, depth);
    return Results.Ok(new {
        changedAsset = store.Find(id), direction, environment, maxDepth = depth,
        potentialAssetCount = affected.Count,
        countsByType = affected.GroupBy(a => a.Asset.AssetType).ToDictionary(g => g.Key, g => g.Count()),
        assets = affected,
        note = "Potential impact inferred from declared dependencies; this is not a confirmed risk assessment. Paths show one shortest route, not every route. Results are limited to maxDepth."
    });
});
app.Run();

static IResult BadEnvironment() => Results.Problem(statusCode: 400, detail: "environment must be Dev, Uat or Production.");
