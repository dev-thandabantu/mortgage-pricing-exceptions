# Working in this repository

## Purpose and scope

A small mortgage-pricing exception service: ASP.NET Core on .NET 10, EF Core with
file-backed SQLite, a static internal UI, xUnit integration/domain tests and an
optional bounded source-review runner. The mortgage workflow is deterministic;
no model participates in customer pricing decisions.

Keep the service straightforward to run locally. Do not add infrastructure,
frameworks or production features merely to make the exercise appear larger.
Future production requirements belong in the separate production backlog.

## Start a task

1. Check the working tree, branch and actual date before editing or judging status.
2. Read README.md, docs/decisions.md and the relevant existing issue/PR. Check for
   overlapping open work. Use the request's scope; do not ask it again if clear.
3. Read docs/open-questions.md when touching policy. Distinguish a brief requirement,
   an exercise assumption and a future capability. Old draft plans are not the API.
4. Trace the affected route, domain rule, persistence mapping and test before
   claiming behavior is present or absent. An adjacent grep result is not proof.
5. State the intended behavior, important failure case and smallest useful change.
   Update affected docs in the same PR; flag contradictions instead of copying them.

## Code and context map

| Area | Start here |
|---|---|
| Startup, middleware, HTTP routes and policies | src/PricingExceptions.Api/Program.cs |
| State transitions and validation | src/PricingExceptions.Api/Domain.cs |
| Transactions, retries and approved-term reads | src/PricingExceptions.Api/Workflow.cs |
| Keys, indexes and EF relationships | src/PricingExceptions.Api/Database.cs |
| JSON request/response shapes | src/PricingExceptions.Api/Contracts.cs |
| Local identities and claims | src/PricingExceptions.Api/DemoAuthentication.cs |
| Browser state and forms | src/PricingExceptions.Api/wwwroot/ |
| Real HTTP/SQLite test host | tests/PricingExceptions.Tests/ApiFactory.cs |
| Reproducible workflow | examples/workflow.http; scripts/demo.ps1 |
| Decisions, client assumptions and evidence | docs/decisions.md; docs/open-questions.md; docs/verification.md |
| Optional model runner and evaluation limits | scripts/review_agent.py; docs/review-agent.md |

Keep private preparation, other assessments and unrelated business material out
of this repository, PRs and source archives. The submission stands on its own.

## Workflow invariants

- Pending may become Approved or Declined. Terminal requests are immutable; no
  withdrawal, reopening or approved-replacement workflow is currently implemented.
- Only the submitting RM may amend Pending terms. Preserve each prior revision.
- Decisions bind to the exact reviewed revision. Reject a stale expectedVersion
  with 409; never silently update it and retry an approval on the caller's behalf.
- Version changes on every mutation; RevisionNumber changes only with new terms.
- Derive the actor from authenticated claims, never a trusted body field. Enforce
  reviewer role and no self-decision separately, including a dual-role requester.
- Persist request state and revision/decision history in one database transaction.
  An external audit file is not the source of truth. Test rollback, not just success.
- Begin SQLite write transactions before mutable validation reads. Do not replace
  shared-store integrity with a process-local lock. Preserve the EF Version token.
- Enforce one Pending or Approved request per application in the database. Decline
  permits a new request; one terminal decision per request is also a database rule.
- Persist successful creation, initial revision and replay record atomically.
  Scope keys to actor/operation; same key with different parsed fields returns 409.
  Replay the original success body/Location, even after later edits or restart.
  Do not hash raw JSON layout or reconstruct the response from current state.
- Return approved terms from the revision named by the decision, with provenance;
  never expose a pending proposal as an approved discount.
- Discounts use integer basis points and a configurable inclusive demo cap. The
  cap and equal reviewer authority are assumptions, not regulatory requirements.
- Demo identity is Development/Testing only. Keep the Production startup guard.
  Real identity, record access, expiry and application-fact invalidation need policy.
- Browser permission updates must preserve unsaved drafts/comments after failure.
  Obsolete reads must not replace a newer selection; preserve command/read guards.

A requested policy change must identify which invariant changes and update its
implementation, tests, decision rationale and client assumptions together.

## Run and verify

```sh
dotnet restore
dotnet run --project src/PricingExceptions.Api
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
python -m unittest discover -s scripts -p test_review_agent.py -v
```

Default UI: http://localhost:5019. Seed data: /demo. OpenAPI: /openapi/v1.json.
A running Windows Debug preview may lock its output: use Release for checks or
an isolated output, rather than disrupting the preview. Never reset an active
SQLite database casually. Use a fresh, disposable database for destructive or
repeatable scenarios; EnsureCreated is not a migration-management strategy.

| Change | Additional evidence when relevant |
|---|---|
| Rules, auth, API or persistence | Real file-backed SQLite HTTP cases; competing clients/connections for race claims; rollback and retry boundaries |
| Browser behavior | scripts/browser-smoke.cjs and affected failure/delay cases in scripts/browser-review.cjs; inspect rendered output |
| Review runner | Offline Python checks, input/output scope, timeout/error classification; live evaluation only when needed and authorized |
| Docs/instructions only | Check claims against source, examples, links and current review state; do not add copy-mirroring tests |

Use docs/verification.md for browser prerequisites and isolated scenario setup.
Before committing, complete the Release build/test gate above. Run the Python
suite when touching its runner or CI. State checks actually performed and limits.
Do not weaken, skip or rewrite a test to hide a regression. Bug-fix checks should
fail with the defect reintroduced. New tests must protect a concrete outcome,
not implementation choreography, framework behavior or a coverage target.

The normal service and offline runner checks need no model account. A live review
requires Git and a Codex login, sends supplied source to OpenAI and is outside CI.
Honor existing source/provider authorization; do not repeatedly ask for it. Never
publish credentials or raw private traces. Keep fixture expectations/test names
outside model input; errors are not clean reviews and seeded cases are not a
model-accuracy benchmark. The runner must not edit, execute suggestions, post or merge.

## Review and acceptance

1. Work on a branch and open a PR; never push implementation directly to master.
2. Run an adversarial pass against the actual change; keep findings local first.
   Attack failure paths, boundaries, races, unsupported claims and missing context.
3. Use the agreed fresh-chat review with only the brief, PR/issue and exact head.
   Do not prime it with proposed findings, suggested fixes, confidence or expected
   test counts. It should assess code before reading earlier review comments.
4. Compare both assessments, fix what is supported by a concrete case and post one
   combined review. Distinguish independently reported checks from other checks;
   do not inflate the number or independence of approvals. No findings is valid.
5. Obtain final fresh review of the unchanged head and the owner's explicit merge
   authorization. A passing review alone is not authorization; changed heads need
   reassessment. This gate also applies to docs/instructions changes.
6. Merge only the reviewed head with passing CI. Verify the merge tree matches it.
   Close linked issues/milestone only after acceptance; verify the actual counters.
7. Refresh the submission archive from accepted Git source, excluding local state,
   outputs and private prep. Preserve older accepted tags; use a new final tag.
   Verify archive contents and relevant extracted checks before replacing the ZIP.

Keep future-production work separate from submission acceptance. Do not leave
completed milestones closed with open issues or claim 100% without checking.

## Account voice and evidence

- Text posted through the owner's account uses first person for real decisions or
  neutral technical prose. Do not narrate the owner in third person or publish
  chat handover/permission bookkeeping. This applies to PRs, reviews, issues,
  milestone descriptions and submission docs, not just the README.
- Do not turn a supplied review report into a claim of personally performed checks.
  Preserve reviewed heads, findings and verification limits when correcting prose.
- Keep AI-use notes short, factual and supported by actual work. No attribution
  trailers in commits or PR descriptions; do not imply unaided manual authorship.
- Record only measured time and label its scope. Session elapsed is not equivalent
  human effort; identify unmeasured work instead of inventing a complete total.
- Record genuine client questions with owner, current assumption and consequence.
  Do not invent stakeholder engagement or send domain questions to a recruiter.

## Maintain this file

Keep this guide under 200 lines. Add durable, project-specific constraints when
architecture or workflow changes; remove duplication and obsolete rules. Completed
work, temporary review status and long explanations belong in PRs/docs, not here.
