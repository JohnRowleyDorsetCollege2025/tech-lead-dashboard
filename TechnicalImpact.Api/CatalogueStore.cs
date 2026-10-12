using System.Text.Json;

namespace TechnicalImpact.Api;

public sealed class CatalogueStore
{
    private readonly Catalogue catalogue;
    public IReadOnlyList<TechnicalAsset> Assets => catalogue.Assets;
    public IReadOnlyList<AssetRelationship> Relationships => catalogue.Relationships;
    public TechnicalAsset? Find(string id) => Assets.FirstOrDefault(a => a.Id == id);
    public CatalogueStore(string path)
    {
        catalogue = JsonSerializer.Deserialize<Catalogue>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Catalogue is empty.");
        if (catalogue.SchemaVersion != 1 || catalogue.Assets is null || catalogue.Relationships is null)
            throw new InvalidDataException("Expected schemaVersion 1, assets and relationships.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in Assets)
        {
            if (string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Name) ||
                string.IsNullOrWhiteSpace(a.AssetType) || !ids.Add(a.Id))
                throw new InvalidDataException("Asset IDs must be nonempty and unique; name and assetType are required.");
            if (a.Deployments is null || a.Technologies is null || a.Evidence is null)
                throw new InvalidDataException($"Asset {a.Id}: deployments, technologies and evidence are required.");
            if (a.Deployments.Any(d => !ValidEnvironment(d.Environment) || d.Endpoints is null) ||
                a.Deployments.Select(d => d.Environment.ToLowerInvariant()).Distinct().Count() != a.Deployments.Count)
                throw new InvalidDataException($"Asset {a.Id}: invalid or duplicate deployment environment.");
            foreach (var e in a.Deployments.SelectMany(d => d.Endpoints))
                if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "https" && uri.Scheme != "http" && uri.Scheme != "sb"))
                    throw new InvalidDataException($"Asset {a.Id}: invalid endpoint URL.");
        }
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Relationships)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || !relationshipIds.Add(r.Id) ||
                !ids.Contains(r.SourceAssetId) || !ids.Contains(r.TargetAssetId) ||
                string.IsNullOrWhiteSpace(r.RelationshipType) || r.Evidence is null ||
                r.Environments is null || r.Environments.Count == 0 || r.Environments.Any(e => !ValidEnvironment(e)))
                throw new InvalidDataException($"Relationship {r.Id}: invalid ID, reference, type, evidence or environments.");
            foreach (var environment in r.Environments)
                foreach (var id in new[] { r.SourceAssetId, r.TargetAssetId })
                    if (!Find(id)!.Deployments.Any(d => Same(d.Environment, environment)))
                        throw new InvalidDataException($"Relationship {r.Id}: {id} has no {environment} deployment.");
        }
    }
    public static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    public static bool ValidEnvironment(string? value) => value is not null &&
        new[] { "Dev", "Uat", "Production" }.Any(e => Same(e, value));
    public IEnumerable<AssetRelationship> Edges(string? environment) => Relationships.Where(r =>
        environment is null || r.Environments.Any(e => Same(e, environment)));

    public List<ImpactAsset> Traverse(string id, string direction, string? environment, int maxDepth)
    {
        var edges = Edges(environment).ToList();
        var visited = new HashSet<string>(StringComparer.Ordinal) { id };
        var queue = new Queue<(string Id, int Depth, List<string> Path)>();
        queue.Enqueue((id, 0, new List<string> { id }));
        var result = new List<ImpactAsset>();
        while (queue.TryDequeue(out var current))
        {
            if (current.Depth >= maxDepth) continue;
            var next = direction == "dependents"
                ? edges.Where(r => r.TargetAssetId == current.Id).Select(r => r.SourceAssetId)
                : edges.Where(r => r.SourceAssetId == current.Id).Select(r => r.TargetAssetId);
            foreach (var nextId in next.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!visited.Add(nextId)) continue;
                var path = new List<string>(current.Path) { nextId };
                result.Add(new ImpactAsset(Find(nextId)!, current.Depth + 1, path));
                queue.Enqueue((nextId, current.Depth + 1, path));
            }
        }
        return result;
    }
}
