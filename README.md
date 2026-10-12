# tia-manager GitHub BFF

This solution contains an ASP.NET Core API and a server-interactive Blazor Web App. The API reads a list of GitHub repositories from `appsettings.json` and uses the GitHub REST API to list branches and compare each branch with its configured base branch.

## Projects

- `tia-manager.csproj` — GitHub BFF API.
- `tia-manager.Blazor/tia-manager.Blazor.csproj` — default server-interactive Blazor Web App.

Build both projects with `dotnet build tia-manager.sln`. Run either project with `dotnet run --project tia-manager.csproj` or `dotnet run --project tia-manager.Blazor/tia-manager.Blazor.csproj`.

The Blazor home page loads configured repositories and branch statistics from the BFF. Each repository has a branches link to a detail page with ahead/behind counts and sortable latest-committer-date, ahead, and behind columns. The base branch row is labeled and summarizes how many other branches are ahead, rather than showing its self-comparison as up to date. The home page shows a warning when any branch is ahead of its repository's base branch; unavailable comparisons are shown as such rather than treated as clean. Configure its server-side API URL with `BffApi:BaseUrl` in `tia-manager.Blazor/appsettings.json` or override it with the `BffApi__BaseUrl` environment variable.

The Blazor navigation also includes a searchable technical asset catalogue at `/assets`. Selecting an asset opens `/assets/{id}` and loads its direct dependencies and consumers from `/api/assets/{id}/relationships`. Mock configuration forms at `/assets/new` and `/relationships/new` show the TechnicalAsset and relationship fields; the relationship form loads existing assets for source/target selection. Neither form saves data. The catalogue reads asset metadata from the Technical Impact API. Configure its URL with `TechnicalImpactApi:BaseUrl` in `tia-manager.Blazor/appsettings.json` or override it with `TechnicalImpactApi__BaseUrl`; the local API default is `http://localhost:5080/`.

## Configuration

Add repositories to the `Repositories` array in `appsettings.json`:

```json
{
  "Owner": "your-github-owner",
  "Name": "your-repository",
  "Label": "Friendly repository name",
  "BaseBranch": "master"
}
```

`Label` is the friendly name shown in the Blazor dashboard. The current sample labels match each repository's `Name`.

Set `GitHub:Token` in `appsettings.Development.json` to a GitHub personal access token before making GitHub-backed requests. The committed value is only a placeholder; do not replace it with a real token in source control. For deployment, provide the token through the `GitHub__Token` environment variable or a secret manager.

## Endpoints

- `GET /api/repositories` — list configured repositories.
- `GET /api/repositories/{owner}/{repository}/branches` — list branches, their latest commit SHA, and the latest commit's committer date. The BFF fetches commit details for each branch to populate this date.
- `GET /api/repositories/{owner}/{repository}/branch-statistics` — compare all branches against the configured base branch.
- `GET /api/repositories/{owner}/{repository}/branch-statistics?baseBranch=main` — compare against a different base branch.

For branch statistics, `BaseAheadBy` is the number of commits the selected base branch has that the branch does not, and `BranchAheadBy` is the number of commits the branch has that the selected base branch does not. The selected base branch defaults to `master`.

Only repositories listed in configuration are accessible through the API.
