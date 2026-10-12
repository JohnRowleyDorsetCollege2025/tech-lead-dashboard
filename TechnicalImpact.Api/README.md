# Technical Impact API

A read-only ASP.NET Core (.NET 10) prototype, ready for a Blazor client. No database or external NuGet packages are required. All asset metadata is read from `Data/assets.json` into an immutable startup snapshot. Edit the JSON and restart to reload it. The SDK is required to build/run.

## Run

From this folder:

```bash
dotnet build
dotnet run --launch-profile http
```

Then open http://localhost:5080/api/assets or use the included `requests.http` in Rider/VS Code. Deployment/publish copies the JSON with the application. Override its path with configuration key `Catalogue:Path` (environment variable `Catalogue__Path`). Relative paths resolve against the application content root.

## Endpoints

| GET route | Purpose |
|---|---|
| `/health` | Readiness after startup validation |
| `/api/assets` | Full asset list; optional `search`, `assetType`, `ownerTeam`, `environment` filters |
| `/api/assets/{id}` | One asset, including repository and deployment details |
| `/api/relationships?environment=Production` | All edges, optionally scoped to an environment |
| `/api/assets/{id}/relationships` | Direct outgoing dependencies and incoming consumers, each with related asset details |
| `/api/graph?environment=Production` | Nodes and edges for a Blazor graph view |
| `/api/assets/{id}/impact` | Transitive consumers; optional `direction`, `environment`, `maxDepth` |

IDs are case-sensitive. Filters, direction and environments are case-insensitive. Supported environments: Dev, Uat, Production. Missing assets return 404; invalid environment/direction/depth returns 400 Problem Details. Default direction is `dependents`, default depth 10, allowed depth 1–50. Asset-list responses include the complete asset record; the environment filter selects assets, rather than stripping other deployments from those records.

## Data model

- **Asset:** a logical application, API, database, worker, messaging topic, library or external system. Repository is optional; `path` can identify one service within a monorepo. Multiple assets can refer to the same repository.
- **Deployment:** per-environment resource, version and zero or more named endpoints. Database deployments have engine/server/database name metadata. These are metadata, not connection strings or credentials.
- **Relationship:** source depends on target; `environments` identifies where this edge exists. For example `claims-worker → policy-updated (ConsumesFrom)` and `policy-api → policy-updated (PublishesTo)`. A topic change therefore finds both publisher and consumer. A publisher change does not automatically traverse the topic to its consumers; query the changed topic/contract as well when a published event changes.
- **Evidence:** source, last verification date/person and confidence on both assets and edges. All sample evidence and URLs are fictional.

Databases are first-class assets, not embedded copies inside API records. `policy-api → policy-db` establishes the relationship; database hosting details belong to the database's deployments. Separate ReadsFrom and WritesTo edges are supported.

The catalogue uses `schemaVersion: 1`. Startup validates IDs, required collections, endpoints, dangling references and deployment environment coverage. Asset/relationship type strings are intentionally extensible for experimenting with the taxonomy. Evidence does not currently affect traversal.

## Impact semantics

`GET /api/assets/static-db/impact?environment=Production` returns four potential dependents: Occupation API and Pricing API (depth 1), Quote API (depth 2), Broker Portal (depth 3).

`GET /api/assets/policy-api/impact?direction=dependencies` follows outgoing edges instead, showing the systems Policy API depends on. Incoming traversal identifies consumers potentially affected by a change; outgoing traversal explores dependencies and integration/testing scope.

Breadth-first traversal deduplicates assets, prevents cycles and returns one shortest path per asset. The changed asset is excluded from counts. Counts represent logical assets, not deployments. A path runs from the selected asset outward through the chosen traversal direction; for dependents it runs opposite to stored edge arrows. The depth limit may omit further assets. `/api/graph` retains every edge, including multiple edges between the same assets.

## Blazor starting point

Bind `/api/assets` to a searchable catalogue table. Selecting an asset loads its record and `/relationships`; show repository, environment endpoints, database metadata, consumers and dependencies in tabs. Use `/graph` for a node/edge visualisation and `/impact` for an affected-assets table with depth and path. The API does not include a UI.

## Scope and validation

This is a local structural prototype without authentication, write endpoints, paging or automated discovery. Run locally until enterprise authentication and access controls are implemented. A server-side Blazor client can call it directly; a separately hosted browser client needs a configured, explicit CORS origin.

The generation environment had no .NET SDK, so the C# project could not be compiled or executed there. Mock JSON references, environment coverage and the expected StaticDB transitive result were validated independently. Run the supplied smoke check after starting the API to exercise actual HTTP behaviour:

```bash
python3 smoke_check.py
```
