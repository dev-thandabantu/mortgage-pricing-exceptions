# Decisions

## Workflow and amendment policy

Pending -> Approved or Declined. Approval and decline are final. A submitting RM may amend while pending; previous revisions are retained and a decision names the version reviewed. This permits corrections without changing an approval retrospectively. Post-approval renegotiation needs an explicit supersession workflow, outside this exercise. No withdrawal endpoint is planned.

At most one Pending or Approved request exists per application, enforced by a filtered unique index. Decline permits a new submission; replacing an approval is not supported.

## Exercise boundaries

Applications are seeded read-only records. Users are seeded identities selected through X-User-Id in Development/Testing only. Discounts use integer basis points. A configurable demonstration cap is an assumption, not a lender policy. Authorisation and four-eyes are separate checks.

SQLite keeps local setup simple. State transitions, revisions and decisions share one transaction. Creation idempotency also commits in that transaction; caller and operation scope its key. Same key/different parsed fields returns 409. Stored successful responses are replayed, not reconstructed from current request state.

The service will use explicit expectedVersion values and 409 for conflicting mutations. SQLite write transactions are short and serialised; application-managed concurrency tokens additionally protect updates. This is an exercise-scale concurrency trade-off.
