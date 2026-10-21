# Working rules

Small mortgage-pricing exception service. Read README.md and docs/decisions.md first.

- Approval binds to the exact revision reviewed. Terminal requests cannot be edited.
- Derive actors from authenticated identity. Only the submitting RM can amend; no self-approval.
- Commit request changes and history together in SQLite. Never use an external audit file as the source of truth.
- Persist creation idempotency and its response in the same transaction as the request.
- Enforce one pending or approved request per application in the database.
- Use real SQLite for persistence tests. Exercise competing requests with separate clients/connections.
- Demo authentication is confined to Development/Testing. No private infrastructure is required.
- Use a review branch and PR before merge. Keep acceptance open until Brighton returns a passing final fresh-chat review and authorizes merge.
- Before committing: dotnet build --no-restore and dotnet test --no-build.
- Record only work and checks that actually happened. Keep AI notes brief. Never invent time spent.
- No AI attribution trailers in commits. Do not include other assessments or their names.
