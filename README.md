# Mortgage pricing exceptions

An internal HTTP service for requesting and reviewing discounts on mortgage interest rates.
Built with ASP.NET Core (.NET 10), EF Core and SQLite. Implementation is in progress; no runnable workflow is claimed in this initial planning commit.

## Delivery checkpoints

1. Complete service: create, decide, retrieve, history, durable retries, pending amendments, tests and run instructions.
2. Bounded agent-review demonstration with checked findings.
3. Simple UI using the same API.

Each checkpoint will be verified before the next optional addition. No external infrastructure or model access will be required to run the service.

## Development

Requires the .NET 10 SDK. `dotnet restore`, `dotnet build`, `dotnet test`.
Run and scenario instructions will be added alongside the working service.

Decisions: [docs/decisions.md](docs/decisions.md). Client questions: [docs/open-questions.md](docs/open-questions.md).
