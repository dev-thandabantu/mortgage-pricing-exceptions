# Mortgage pricing exceptions

A small internal service for requesting and approving discounts on mortgage interest rates. An RM submits a proposal; a different authorised reviewer decides it. The mortgage process can retrieve only an approved discount, with the decision and revision that authorised it.

## Run

Requires the **.NET 10 SDK**. No database server, credentials or model access needed.

```sh
dotnet restore
dotnet run --project src/PricingExceptions.Api
```

The API runs at **http://localhost:5019**. Seed applications and demo users: `/demo`. OpenAPI: `/openapi/v1.json`. Requests: [examples/workflow.http](examples/workflow.http).

```sh
dotnet test
```

SQLite is created in `src/PricingExceptions.Api/state/pricing.db` on first run. To reset the exercise, stop the service and remove that `state` directory. Startup uses `EnsureCreated`, intentionally without migration management; use a fresh database when switching between schema-changing commits.

## Explore a workflow

On Windows, with the API running and a fresh database:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/demo.ps1
```

The script submits 25 bps, retries the same call, amends to 40 bps, demonstrates rejection of a stale approval, approves the current revision, and checks the downstream discount. It stops on unexpected results. It intentionally leaves the approval/history available for inspection. Reset before repeating, or pass `-ApplicationId app-1002`.

One basis point is 0.01 percentage points; 25 bps is a 0.25 percentage-point discount. The service returns the discount, not a calculated interest rate.

## Contract

All workflow endpoints require `X-User-Id`. This is a **local exercise identity selector**, accepted only in Development/Testing. Users are synthetic: `rm-alex`, `rm-sam`, `reviewer-jules`, `reviewer-robin`, `rm-reviewer` (both roles), and `mortgage-process`.

| Operation | Endpoint | Caller |
|---|---|---|
| Create | `POST /pricing-exceptions` | RM; `Idempotency-Key` required |
| List | `GET /pricing-exceptions` | Known internal user; latest 100 |
| Inspect, including full history | `GET /pricing-exceptions/{id}` | Known internal user |
| Amend pending terms | `PATCH /pricing-exceptions/{id}` | Submitting RM; `expectedVersion` required |
| Decide | `POST /pricing-exceptions/{id}/decisions` | Reviewer other than requester; `expectedVersion` required |
| Read approved discount | `GET /applications/{id}/approved-discount` | Mortgage process or internal RM/reviewer |

Errors use Problem Details: 400 for invalid input, 401 for missing/unknown identity, 403 for forbidden actions, 404 for absent resources/approval, 409 for state/version/key conflicts. Database lock timeout returns 503 with `Retry-After: 1`. Successful creation/replay returns 201 and the original `Location`/body; `Idempotency-Replayed` identifies a replay.

## Deliberate decisions

- **Pending requests can change; decisions cannot.** Each amendment retains both discount and reason. A decision names the reviewed revision. Two callers cannot silently overwrite each other's work.
- One pending or approved request per application. Decline permits resubmission. Replacing an approval and post-approval negotiation are outside scope.
- Integer basis points, positive up to a configurable **100 bps inclusive** demo cap (`Pricing__MaxDiscountBps`). This is an assumed exercise policy, not a regulatory limit.
- Creation idempotency is scoped to actor and operation. The fingerprint uses parsed fields, ignoring JSON layout/property order but preserving field values. Same key/different fields returns 409. Only successful creations consume keys; retries replay the original response even after amendment/decision/restart. Keys have no expiry in this exercise.
- SQLite serialises short write transactions before validation reads. Creation/replay data and request/history changes commit atomically. A concurrency token changes on every mutation. No process-local lock is relied on.
- History is append-only through this API, not tamper-proof against a database administrator. There is one terminal decision per request; no reopening/re-decision workflow is implied by the history collection.

The decision rationale and production priorities are in [docs/decisions.md](docs/decisions.md); provisional client assumptions are in [docs/open-questions.md](docs/open-questions.md).

## Evidence and handover

Integration tests use isolated, **file-backed SQLite** databases with the actual HTTP pipeline. They cover approval/decline provenance, restart-safe retries, concurrent duplicates and decisions, amendment/decision races, four-eyes with a dual-role user, ownership, database uniqueness and rollback after an injected history-write failure. Focused domain tests cover stale amendments and rate boundaries. These demonstrate exercise behaviour, not a production load benchmark.

[Interview scenario and next steps](docs/walkthrough.md) ? [AI assistance](docs/ai-usage.md) ? [Time spent](docs/time-spent.md) ? [Work items](docs/work-items.md)
