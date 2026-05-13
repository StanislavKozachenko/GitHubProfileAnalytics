# GitHub Profile Analytics

REST API for querying GitHub profiles and computing activity metrics. Users register, authenticate with JWT, then fetch profile data and analytics for any GitHub username.

[![.NET build & test](https://github.com/StanislavKozachenko/GitHubProfileAnalytics/actions/workflows/ci.yml/badge.svg)](https://github.com/StanislavKozachenko/GitHubProfileAnalytics/actions/workflows/ci.yml)

## Tech stack

- **ASP.NET Core 10** — Web API
- **PostgreSQL 18** + **EF Core** (Npgsql)
- **JWT** access tokens + refresh tokens
- **Octokit.NET** — GitHub API client

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker](https://www.docker.com/)

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Set secrets
cd src/GitHubProfileAnalytics
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=github_analytics;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:Key" "<random-256-bit-key>"
dotnet user-secrets set "Jwt:Issuer" "GitHubProfileAnalytics"
dotnet user-secrets set "Jwt:Audience" "GitHubProfileAnalytics"
dotnet user-secrets set "GitHub:Token" "<your-github-pat>"

# 3. Run
dotnet run
```

The API is available at `https://localhost:7038` (or `http://localhost:5270`).
Interactive API docs (Scalar) are available at `/scalar/v1` in development mode.

## Configuration

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:Key` | JWT signing key (min. 32 characters) |
| `Jwt:Issuer` | JWT issuer claim |
| `Jwt:Audience` | JWT audience claim |
| `GitHub:Token` | GitHub personal access token |

In production set these as environment variables using `__` as separator (e.g. `ConnectionStrings__DefaultConnection`).

## Tests

```bash
dotnet test
```

Integration tests use [Testcontainers](https://testcontainers.com/) — Docker must be running.
