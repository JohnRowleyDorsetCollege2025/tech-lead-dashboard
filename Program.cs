using System.Net.Http.Headers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tia_manager;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.AddHttpClient<GitHubClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<GitHubOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("tia-manager-bff/1.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
});

var repositories = builder.Configuration
    .GetSection("Repositories")
    .Get<List<RepositoryOptions>>() ?? [];

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var (statusCode, title, detail) = exception switch
    {
        GitHubApiException { StatusCode: StatusCodes.Status503ServiceUnavailable } =>
            (StatusCodes.Status503ServiceUnavailable, "GitHub token is not configured", exception.Message),
        GitHubApiException =>
            (StatusCodes.Status502BadGateway, "GitHub API request failed", exception.Message),
        HttpRequestException =>
            (StatusCodes.Status502BadGateway, "Unable to reach the GitHub API", "The upstream GitHub request failed."),
        _ =>
            (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "The request could not be completed.")
    };

    logger.LogError(exception, "Request to {Path} failed with status {StatusCode}", context.Request.Path, statusCode);
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/repositories", () => repositories.Select(repository => new
{
    repository.Owner,
    repository.Name,
    repository.Label,
    repository.BaseBranch
}));

app.MapGet("/api/repositories/{owner}/{repository}/branches", async (
    string owner,
    string repository,
    GitHubClient github,
    CancellationToken cancellationToken) =>
{
    var configuredRepository = FindRepository(repositories, owner, repository);
    return configuredRepository is null
        ? Results.NotFound()
        : Results.Ok(await github.GetBranchesAsync(configuredRepository, cancellationToken));
});

app.MapGet("/api/repositories/{owner}/{repository}/branch-statistics", async (
    string owner,
    string repository,
    string? baseBranch,
    GitHubClient github,
    CancellationToken cancellationToken) =>
{
    var configuredRepository = FindRepository(repositories, owner, repository);
    if (configuredRepository is null)
    {
        return Results.NotFound();
    }

    var comparisonBase = string.IsNullOrWhiteSpace(baseBranch)
        ? configuredRepository.BaseBranch
        : baseBranch;
    var branches = await github.GetBranchesAsync(configuredRepository, cancellationToken);
    var statistics = await github.GetBranchStatisticsAsync(
        configuredRepository,
        comparisonBase,
        branches,
        cancellationToken);

    return Results.Ok(statistics);
});

app.Run();

static RepositoryOptions? FindRepository(
    IEnumerable<RepositoryOptions> repositories,
    string owner,
    string name) =>
    repositories.FirstOrDefault(repository =>
        string.Equals(repository.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(repository.Name, name, StringComparison.OrdinalIgnoreCase));

public partial class Program;
