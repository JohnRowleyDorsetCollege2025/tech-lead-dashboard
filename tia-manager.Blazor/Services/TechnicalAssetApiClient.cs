using System.Net.Http.Json;

namespace tia_manager.Blazor.Services;

public sealed class TechnicalAssetApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<TechnicalAssetSummary>> GetAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/assets", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<TechnicalAssetSummary>>(cancellationToken)
            ?? throw new InvalidDataException("The Technical Impact API returned an empty asset list response.");
    }

    public async Task<TechnicalAssetRelationships> GetAssetRelationshipsAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/assets/{Uri.EscapeDataString(assetId)}/relationships";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TechnicalAssetRelationships>(cancellationToken)
            ?? throw new InvalidDataException("The Technical Impact API returned an empty relationships response.");
    }
}

public sealed record TechnicalAssetSummary(
    string Id,
    string Name,
    string AssetType,
    string Description,
    string OwnerTeam,
    string BusinessCapability,
    string Criticality,
    string LifecycleStatus,
    IReadOnlyList<string> Technologies,
    AssetRepositoryReference? Repository,
    IReadOnlyList<AssetDeploymentSummary> Deployments);

public sealed record AssetRepositoryReference(
    string Provider,
    string Url,
    string DefaultBranch,
    string? Path);

public sealed record AssetDeploymentSummary(
    string Environment,
    string? HostingPlatform,
    string? ResourceName,
    IReadOnlyList<AssetEndpointSummary> Endpoints,
    AssetDatabaseSummary? Database,
    string? Version);

public sealed record AssetEndpointSummary(string Kind, string Url);

public sealed record AssetDatabaseSummary(string Engine, string Server, string DatabaseName);

public sealed record TechnicalAssetRelationships(
    string AssetId,
    IReadOnlyList<AssetRelationshipEntry> Outgoing,
    IReadOnlyList<AssetRelationshipEntry> Incoming);

public sealed record AssetRelationshipEntry(
    AssetRelationshipSummary Relationship,
    TechnicalAssetSummary Asset);

public sealed record AssetRelationshipSummary(
    string Id,
    string SourceAssetId,
    string TargetAssetId,
    string RelationshipType,
    IReadOnlyList<string> Environments,
    string Description,
    AssetEvidenceSummary Evidence);

public sealed record AssetEvidenceSummary(
    string Source,
    DateOnly? LastVerifiedDate,
    string? VerifiedBy,
    string Confidence);
