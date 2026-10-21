# Review record ? 21 October 2026

Reviewer: Codex, in the implementation session. This is a worked demonstration on deliberately authored defects, not a blind or independent model benchmark.

| Patch | Finding | Concrete scenario / regression check |
|---|---|---|
| A | Block: `Domain.cs:63` disables the expected-version check. | A reviewer holding version 1 can approve the amended version 2 terms. Check `Amendment_preserves_revisions_and_stale_decision_cannot_approve_new_terms`. |
| B | Block: `Domain.cs:49` disables four-eyes. | An RM with reviewer permission can approve their own request. Check `Authorisation_ownership_and_four_eyes_are_separate_boundaries`. |
| C | Block: `Workflow.cs:23` accepts conflicting key reuse. | Reusing a creation key with a different discount returns the previous response instead of 409. Check `Key_conflicts_are_rejected_and_keys_are_scoped_to_actor`. |
| D | No behavioural finding: comment-only wording. | The idempotency regression check should still pass. |

No patch was applied to the application by this review. `scripts/check-review-demo.py` verifies the proposed checks on isolated copies, including an unchanged baseline. Machine results are recorded in `results.json`. This small, obvious set demonstrates the review-and-check process; it does not estimate detection reliability. Review can miss defects or invent findings. Findings need reproduction and human acceptance; this agent does not approve pricing or merge changes.
