![HackboxConsole](readmemedia/splashscreen.png)

# HackboxConsole — MicroHack Event Platform

**HackboxConsole** is a hackathon and technical challenge management platform. It delivers a guided, step-by-step experience where participant teams work through a series of challenges under the supervision of a coach, while event organizers maintain oversight across all teams from a central dashboard.

The platform is purpose-built for running **Azure Migration & Modernization MicroHacks** — hands-on workshops that walk teams through discovery, assessment, and application modernization using Azure services and GitHub Copilot.

## What it does

- **Participants** log in, see their current challenge (rendered from Markdown), view lab credentials, and wait for coach approval to advance.
- **Coaches** review solutions, approve/revert/reset team progress, and monitor timing — all in real time via SignalR.
- **Event Organizers** get a dashboard showing every team's status with bulk controls to manage the event at scale.

Each team's data (progress, credentials, timers) is fully isolated. Multiple teams share one application instance.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Frontend** | Next.js 16, React 19, TypeScript, Material UI (MUI), Tailwind CSS |
| **Backend** | .NET 10 Minimal API, SignalR (real-time), Swagger |
| **Data** | PostgreSQL (via EF Core) or file-based JSON (auto-detected) |
| **Orchestration** | .NET Aspire (local dev) |
| **Deployment** | Azure Container Apps via Azure Developer CLI (`azd`) |
| **Infra** | Bicep (in `infra/`) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (required — Aspire uses it for PostgreSQL)

## Running Locally

The easiest way to run everything is with the included PowerShell script, which launches .NET Aspire to orchestrate the API, the Next.js frontend, and a PostgreSQL database together:

```powershell
./run-local.ps1
```

The Aspire dashboard opens automatically in your browser and shows all services with logs, traces, and endpoints.

To do a clean build first (removes caches and reinstalls dependencies):

```powershell
./run-local.ps1 -CleanBuild
```

### Running services individually

If you prefer to run services separately (uses file-based storage instead of PostgreSQL):

```bash
# API (.NET backend)
cd src/api
dotnet run

# Web (Next.js frontend) — in a separate terminal
cd src/web
npm install
npm run dev
```

The API runs on `http://localhost:5000` and the web frontend on `http://localhost:3000`.

## Deploy to Azure

```bash
azd auth login
azd up
```

This provisions Azure Container Apps for the API and web frontend, a Container Registry, and supporting infrastructure defined in `infra/`.

## Project Structure

```
├── hackcontent/
│   ├── challenges/          # Challenge Markdown files (challenge-001.md, ...)
│   └── solutions/           # Solution Markdown files + media
├── src/
│   ├── api/                 # .NET 10 Minimal API
│   │   ├── Endpoints/       # Route handlers (auth, challenges, timer, dashboard, ...)
│   │   ├── Hubs/            # SignalR hub for real-time updates
│   │   ├── Services/        # Business logic
│   │   ├── Models/          # Data models
│   │   ├── Data/            # Repository pattern (File + EF Core implementations)
│   │   └── config-data/     # User accounts, credentials, progress (JSON files)
│   ├── web/                 # Next.js 16 frontend (App Router, MUI, Tailwind)
│   ├── AppHost/             # .NET Aspire orchestrator
│   └── ServiceDefaults/     # Shared service configuration
├── infra/                   # Azure infrastructure (Bicep)
├── specs/                   # Product specifications (PRD, FRDs, Gherkin)
├── tests/                   # BDD / integration tests
├── e2e/                     # Playwright end-to-end tests
├── azure.yaml               # Azure Developer CLI configuration
├── run-local.ps1            # One-command local dev launcher
└── run-tests.ps1            # Test runner script
```

## Challenge Content

Challenges and solutions are authored in Markdown and placed in `hackcontent/`:

- `hackcontent/challenges/challenge-001.md`, `challenge-002.md`, etc.
- `hackcontent/solutions/solution-001.md`, `solution-002.md`, etc.

Files are numbered sequentially and loaded in order at startup. The frontend renders them with full Markdown support including syntax-highlighted code blocks, images, tables, and lists.

## License

ISC
