using System.Net.Http.Json;

namespace tia_manager.Blazor.Services;

public sealed class RepositoryApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<RepositorySummary>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/repositories", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RepositorySummary>>(cancellationToken)
            ?? throw new InvalidDataException("The BFF returned an empty repository list response.");
    }

    public async Task<IReadOnlyList<BranchStatisticsSummary>> GetBranchStatisticsAsync(
        RepositorySummary repository,
        CancellationToken cancellationToken = default)
    {
        var owner = Uri.EscapeDataString(repository.Owner);
        var name = Uri.EscapeDataString(repository.Name);
        var baseBranch = Uri.EscapeDataString(repository.BaseBranch);
        var path = $"api/repositories/{owner}/{name}/branch-statistics?baseBranch={baseBranch}";

        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<BranchStatisticsSummary>>(cancellationToken)
            ?? throw new InvalidDataException("The BFF returned an empty branch statistics response.");
    }

    public async Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(
        RepositorySummary repository,
        CancellationToken cancellationToken = default)
    {
        var owner = Uri.EscapeDataString(repository.Owner);
        var name = Uri.EscapeDataString(repository.Name);
        var path = $"api/repositories/{owner}/{name}/branches";

        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<BranchSummary>>(cancellationToken)
            ?? throw new InvalidDataException("The BFF returned an empty branch list response.");
    }
}

public sealed record RepositorySummary(string Owner, string Name, string Label, string BaseBranch);

public sealed record BranchStatisticsSummary(
    string BaseBranch,
    string Branch,
    string Status,
    int BaseAheadBy,
    int BranchAheadBy);

public sealed record BranchSummary(string Name, string Sha, bool Protected, DateTimeOffset? LastCommitterDate);

public sealed record RepositoryBranchRow(BranchSummary Branch, BranchStatisticsSummary Statistics);

public sealed record RepositoryBranchStatus(
    RepositorySummary Repository,
    bool? IsBaseBehindAnyBranch,
    string? Error);
