using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace tia_manager;

public sealed class GitHubClient(
    HttpClient httpClient,
    IOptions<GitHubOptions> options,
    ILogger<GitHubClient> logger)
{
    private const int PageSize = 100;
    private const int MaxConcurrentComparisons = 4;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(
        RepositoryOptions repository,
        CancellationToken cancellationToken)
    {
        var branches = new List<BranchInfo>();
        var page = 1;

        while (true)
        {
            var path = $"repos/{Escape(repository.Owner)}/{Escape(repository.Name)}/branches?per_page={PageSize}&page={page}";
            var pageBranches = await GetAsync<List<GitHubBranch>>(path, cancellationToken);
            using var concurrencyLimit = new SemaphoreSlim(MaxConcurrentComparisons);
            var branchDetails = pageBranches.Select(async branch =>
            {
                await concurrencyLimit.WaitAsync(cancellationToken);
                try
                {
                    var commitPath =
                        $"repos/{Escape(repository.Owner)}/{Escape(repository.Name)}/commits/{Escape(branch.Commit.Sha)}";
                    var commit = await GetAsync<GitHubCommitDetails>(commitPath, cancellationToken);
                    var committerDate = commit.Commit.Committer?.Date;
                    if (committerDate is { } date)
                    {
                        logger.LogInformation(
                            "Resolved latest committer date for {Owner}/{Repository} branch {Branch} at commit {CommitSha}: {CommitterDate:O}",
                            repository.Owner,
                            repository.Name,
                            branch.Name,
                            branch.Commit.Sha,
                            date);
                    }
                    else
                    {
                        logger.LogWarning(
                            "GitHub commit response contained no committer date for {Owner}/{Repository} branch {Branch} at commit {CommitSha}",
                            repository.Owner,
                            repository.Name,
                            branch.Name,
                            branch.Commit.Sha);
                    }

                    return new BranchInfo(
                        branch.Name,
                        branch.Commit.Sha,
                        branch.Protected,
                        committerDate);
                }
                finally
                {
                    concurrencyLimit.Release();
                }
            });

            branches.AddRange(await Task.WhenAll(branchDetails));

            if (pageBranches.Count < PageSize)
            {
                return branches;
            }

            page++;
        }
    }

    public async Task<IReadOnlyList<BranchStatistics>> GetBranchStatisticsAsync(
        RepositoryOptions repository,
        string baseBranch,
        IReadOnlyList<BranchInfo> branches,
        CancellationToken cancellationToken)
    {
        using var concurrencyLimit = new SemaphoreSlim(MaxConcurrentComparisons);
        var tasks = branches.Select(async branch =>
        {
            if (string.Equals(branch.Name, baseBranch, StringComparison.Ordinal))
            {
                return new BranchStatistics(baseBranch, branch.Name, "identical", 0, 0);
            }

            await concurrencyLimit.WaitAsync(cancellationToken);
            try
            {
                var comparison = await CompareAsync(repository, baseBranch, branch.Name, cancellationToken);
                return new BranchStatistics(
                    baseBranch,
                    branch.Name,
                    comparison.Status,
                    comparison.BehindBy,
                    comparison.AheadBy);
            }
            finally
            {
                concurrencyLimit.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<GitHubComparison> CompareAsync(
        RepositoryOptions repository,
        string baseBranch,
        string branch,
        CancellationToken cancellationToken)
    {
        var path = $"repos/{Escape(repository.Owner)}/{Escape(repository.Name)}/compare/{Escape(baseBranch)}...{Escape(branch)}";
        return await GetAsync<GitHubComparison>(path, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var token = options.Value.Token;
        if (string.IsNullOrWhiteSpace(token) ||
            string.Equals(token, "YOUR_GITHUB_TOKEN_HERE", StringComparison.Ordinal))
        {
            throw new GitHubApiException(
                "Set GitHub:Token in appsettings.Development.json before calling GitHub-backed endpoints.",
                StatusCodes.Status503ServiceUnavailable);
        }

        var requestUrl = new Uri(httpClient.BaseAddress!, path);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Sending GitHub API request {Method} {Url}", request.Method, requestUrl);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "GitHub API request {Method} {Url} failed after {ElapsedMilliseconds} ms",
                request.Method,
                requestUrl,
                stopwatch.ElapsedMilliseconds);
            throw;
        }

        using (response)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "GitHub API response {Method} {Url} returned {StatusCode} in {ElapsedMilliseconds} ms",
                request.Method,
                requestUrl,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubApiException(
                    $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                    ?? throw new GitHubApiException("GitHub returned an empty response.", (int)HttpStatusCode.BadGateway);
            }
            catch (JsonException exception)
            {
                throw new GitHubApiException("GitHub returned an invalid response.", (int)HttpStatusCode.BadGateway, exception);
            }
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}

public sealed record BranchInfo(string Name, string Sha, bool Protected, DateTimeOffset? LastCommitterDate);

public sealed record BranchStatistics(
    string BaseBranch,
    string Branch,
    string Status,
    int BaseAheadBy,
    int BranchAheadBy);

internal sealed record GitHubBranch(
    string Name,
    GitHubCommit Commit,
    bool Protected);

internal sealed record GitHubCommit(string Sha);

internal sealed record GitHubCommitDetails(GitHubCommitMetadata Commit);

internal sealed record GitHubCommitMetadata(GitHubCommitSignature? Committer);

internal sealed record GitHubCommitSignature(DateTimeOffset? Date);

internal sealed record GitHubComparison(
    string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("ahead_by")] int AheadBy,
    [property: System.Text.Json.Serialization.JsonPropertyName("behind_by")] int BehindBy);
