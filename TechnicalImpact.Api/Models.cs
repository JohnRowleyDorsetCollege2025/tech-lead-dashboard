namespace TechnicalImpact.Api;

public sealed record Catalogue(int SchemaVersion, List<TechnicalAsset> Assets, List<AssetRelationship> Relationships);
public sealed record TechnicalAsset(
    string Id, string Name, string AssetType, string Description, string OwnerTeam,
    string BusinessCapability, string Criticality, string LifecycleStatus,
    List<string> Technologies, RepositoryReference? Repository,
    List<AssetDeployment> Deployments, Evidence Evidence);
public sealed record RepositoryReference(string Provider, string Url, string DefaultBranch, string? Path);
public sealed record AssetDeployment(
    string Environment, string? HostingPlatform, string? ResourceName,
    List<AssetEndpoint> Endpoints, DatabaseDetails? Database, string? Version);
public sealed record AssetEndpoint(string Kind, string Url);
public sealed record DatabaseDetails(string Engine, string Server, string DatabaseName);
public sealed record Evidence(string Source, DateOnly? LastVerifiedDate, string? VerifiedBy, string Confidence);
// Source depends on Target. This applies consistently to Calls, ReadsFrom,
// WritesTo, PublishesTo, ConsumesFrom and DependsOn.
public sealed record AssetRelationship(
    string Id, string SourceAssetId, string TargetAssetId, string RelationshipType,
    List<string> Environments, string Description, Evidence Evidence);
public sealed record ImpactAsset(TechnicalAsset Asset, int Depth, List<string> PathFromChangedAsset);
