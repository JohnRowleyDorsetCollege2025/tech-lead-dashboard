namespace tia_manager;

public sealed class GitHubOptions
{
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";
    public string? Token { get; init; }
}

public sealed class RepositoryOptions
{
    public string Owner { get; init; } = "";
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string BaseBranch { get; init; } = "master";
}
