# Deployment Guide

## Overview

Practice Before The Patient runs as three Docker containers managed by Docker Compose:

| Container | Image | Purpose |
|-----------|-------|---------|
| `postgres` | `postgres:17-alpine` | Database |
| `api` | Built from source | ASP.NET Core Web API |
| `web` | Built from source | Blazor Server UI |

The repository includes:

- `compose.yaml` for local development
- `compose.prod.yaml` for VM/server deployments behind a reverse proxy

No cloud accounts or external services are required.

---

## Prerequisites

| Software | Minimum Version | Notes |
|----------|----------------|-------|
| Docker Desktop (or Docker Engine) | 24+ | Includes the Compose plugin |
| Docker Compose | v2 (`docker compose`) | Comes with Docker Desktop |
| Git | Any | To clone the repository |

No .NET SDK is required on the host. The Dockerfiles use multi-stage builds that compile the application inside a container.

---

## Deployment Steps

### 1. Clone the repository

```bash
git clone <repository-url>
cd CS495-PracticeBeforeThePatient
```

### 2. (Optional) Override default configuration

The defaults work for local demos. For any other environment, create a `.env` file in the repository root or export environment variables before running Compose:

```bash
# .env example — copy and edit as needed
POSTGRES_DB=practicebeforethepatient
POSTGRES_USER=practicebeforethepatient
POSTGRES_PASSWORD=change-me          # Change this for anything beyond a local demo
POSTGRES_PORT=5432
API_PORT=5186
WEB_PORT=5009
API_ENVIRONMENT=Production
WEB_ENVIRONMENT=Production

# LLM — required for AI scenario generation
LLM_PROVIDER=gemini
LLM_API_KEY=your-api-key-here
LLM_MODEL=gemini-2.5-flash
```

### 3. Start the stack

For a local demo:

```bash
docker compose up --build
```

For a VM or other public host, use the production override:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up --build -d
```

Compose will:
1. Pull the `postgres:17-alpine` image (first run only).
2. Build the `api` and `web` images from source.
3. Start PostgreSQL and wait for it to be healthy.
4. Start the API, which applies EF Core migrations and seeds starter data.
5. Start the web frontend.

The production override keeps `postgres` and `api` on Docker's internal network and binds the `web` service to `127.0.0.1` so Nginx can proxy it safely.

### 4. Verify the deployment

| Service | Default URL |
|---------|-------------|
| Web UI | `http://localhost:5009` |
| API | `http://localhost:5186` |
| Swagger (Development only) | `http://localhost:5186/swagger` |

The site should load within ~30 seconds on first run while images pull and the database initializes.

When using `compose.prod.yaml`, only the web UI is published on the host, and it is bound to `127.0.0.1` by default.

---

## Configuration Reference

All configuration is passed to the containers as environment variables. The table below lists every variable, its source, and its default value.

| Variable | Used by | Default | Description |
|----------|---------|---------|-------------|
| `POSTGRES_DB` | `postgres`, `api` | `practicebeforethepatient` | Database name |
| `POSTGRES_USER` | `postgres`, `api` | `practicebeforethepatient` | Database username |
| `POSTGRES_PASSWORD` | `postgres`, `api` | `change-me` | Database password |
| `POSTGRES_PORT` | `postgres` | `5432` | Host port mapped to PostgreSQL |
| `API_PORT` | `api` | `5186` | Host port mapped to the API |
| `WEB_PORT` | `web` | `5009` | Host port mapped to the web UI |
| `API_ENVIRONMENT` | `api` | `Production` | ASP.NET Core environment (`Development` enables Swagger) |
| `WEB_ENVIRONMENT` | `web` | `Production` | ASP.NET Core environment for the frontend |
| `LLM_PROVIDER` | `api` | `gemini` | LLM provider for scenario generation |
| `LLM_API_KEY` | `api` | *(none)* | API key for the LLM provider (required for generation) |
| `LLM_MODEL` | `api` | `gemini-2.5-flash` | Model name passed to the LLM provider |

The API's database connection string is assembled inside `compose.yaml` from the Postgres variables above. The web frontend is told where to find the API via the internal Docker network (`http://api:8080/`); browsers do not call the API directly.

---

## Seeded Data

On first startup against an empty database, the API seeds the following demo data:

| Entity | Value |
|--------|-------|
| Admin user | `admin@ua.edu` |
| Instructor | `instructor@ua.edu` |
| Students | `student1@ua.edu`, `student2@ua.edu` |
| Classes | CS 100, CS 101 |
| Scenarios | JSON files in `PracticeBeforeThePatient.Api/Data/scenarios/` |

**Authentication note:** The application currently uses a development identity store (`DevAccessStore`). There is no real SSO or login page. The active user is selected via the UI's user-switcher. This is intentional for the current project scope.

---

## External Resources

Everything the application needs is either built from source or pulled from public, free-tier registries at build/startup time.

| Resource | Source | Cost |
|----------|--------|------|
| `postgres:17-alpine` Docker image | Docker Hub (official) | Free |
| `mcr.microsoft.com/dotnet/sdk:9.0` (build stage) | Microsoft Container Registry | Free |
| `mcr.microsoft.com/dotnet/aspnet:9.0` (runtime) | Microsoft Container Registry | Free |
| NuGet packages (`Npgsql`, `EFCore`, `Swashbuckle`) | nuget.org | Free, open source |

No accounts or network access are required after the initial image pull, except for an LLM API key if AI scenario generation is used.

| Resource | Source | Cost |
|----------|--------|------|
| Google Gemini API | Google AI Studio | Free tier available |

The LLM integration is optional — the application functions fully without it. Scenarios can still be created manually via the PUT endpoint. If `LLM_API_KEY` is not set, the generate endpoint returns a 503 error.

---

## Common Operations

### Stop the stack

```bash
docker compose down
```

Data in the `postgres-data` Docker volume is preserved.

If you started the VM deployment with the production override, stop it the same way:

```bash
docker compose -f compose.yaml -f compose.prod.yaml down
```

### Rebuild after code changes

```bash
docker compose up --build
```

For the VM deployment:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up --build -d
```

### Reset the database (delete all data)

```bash
docker compose down -v
docker compose up --build
```

The `-v` flag removes the `postgres-data` volume. The API will re-run migrations and re-seed on next startup.

### Enable Swagger / API explorer

```bash
API_ENVIRONMENT=Development docker compose up --build
```

Then open `http://localhost:5186/swagger`.

### Run in the background

```bash
docker compose up --build -d
docker compose logs -f   # stream logs
docker compose down      # stop when done
```

For the VM deployment:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up --build -d
docker compose -f compose.yaml -f compose.prod.yaml logs -f
docker compose -f compose.yaml -f compose.prod.yaml down
```

---

## Troubleshooting

**The site loads but shows no data**
Check API logs with `docker compose logs api`. If startup failed during migration, restart:
```bash
docker compose down && docker compose up --build
```

**Port already in use**
Override the conflicting port variable before starting:
```bash
WEB_PORT=8080 docker compose up --build
```

**Database connection refused**
The API waits for PostgreSQL's healthcheck before starting. If it still fails, check that Docker has enough memory allocated (2 GB minimum recommended in Docker Desktop settings).
