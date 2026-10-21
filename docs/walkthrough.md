# Walkthrough

## Start with the business rule

An RM proposes a discount; an authorised second person approves the specific terms before the mortgage process can use them. The interesting failure is approving 25 bps while an RM changes the request to 40 bps.

## Run a scenario

Start from a fresh database and run `scripts/demo.ps1` (or the HTTP file). Show:

1. Submission creates revision 1 and version 1.
2. Retry returns the same id, location and original response; no second proposal.
3. RM changes to 40 bps: revision 2/version 2, preserving the original reason and discount.
4. A reviewer attempting version 1 receives 409; the request is still pending.
5. Reviewer approves version 2: decision references revision 2; request version becomes 3.
6. Mortgage process receives 40 bps with request, revision, decision, actor and timestamp.
7. Inspect the request: history explains how the final terms came about.

For decline, use the other seeded application. A declined request supplies no approved discount; a fresh request may then be submitted. A dual-role user cannot approve their own proposal.

## Code path

`Program.cs` selects/authorises identity and maps the contract. `Workflow.cs` owns transaction boundaries. `Domain.cs` enforces transitions and ownership/four-eyes. `Database.cs` maps uniqueness, relationships and concurrency. `Contracts.cs` keeps HTTP responses separate from EF entities.

In SQLite, a write reservation is acquired before reading for a mutation. Other writers wait, then see the committed state. Versions reject actions based on an old read. The decision row and state update share the transaction, so neither can commit alone. Creation and its replay response likewise share a transaction.

## Questions to be ready for

- Why permit pending changes? It supports corrections while ensuring approval never silently changes. Confirm whether the real need includes renegotiation.
- Why a body version? A single explicit conflict contract is sufficient here. A proper ETag/If-Match contract would use its HTTP precondition semantics.
- What happens after an approval? It remains final. Supersession/invalidation needs the mortgage process owner; this exercise deliberately refuses a second active approval.
- What if the application itself changes? Applications are read-only in this exercise. Production approvals should bind to relevant application facts or an application version; a change may invalidate approval.
- What if the database is busy? A short timeout produces 503 and Retry-After. Retrying creation with the same key is safe; decisions retain the expected version.
- What happens when a decision response is lost? Reload the request to discover the committed decision. Decision POST does not claim transparent response replay.

## What next

First resolve pricing authority, application-change invalidation and approval lifecycle with the client. Then replace demo identity with verified OIDC/workload identities and real record-level access. Choose production database and migration/backup/recovery practices based on deployment and availability needs; SQLite is not an implied production commitment. Add audit retention/access controls and monitoring for rejected transitions, failed writes and lock contention. Agree push versus pull with the mortgage team; add transactional outbox delivery if reliable push is required. No event bus or outbox is needed for the current pull contract.
