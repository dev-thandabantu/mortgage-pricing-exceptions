# Client questions

| Question / owner | Assumption for this exercise | Consequence if different |
|---|---|---|
| Why do RMs need to change a submitted discount: corrections or renegotiation? RM lead. | Corrections while pending; decided requests remain final. | Renegotiation after approval needs supersession and downstream invalidation. |
| Can more than one approval apply to an application? Mortgage process owner. | One pending or approved request; declined requests may be resubmitted. | Multiple approvals require explicit selection/priority; replacement needs a lifecycle. |
| What discount precision, cap and delegated authority apply? Credit risk. | Integer basis points; a configurable demo cap; all reviewers have equal authority. | Validation and reviewer permissions must use actual product/authority policy. |
| Which application changes invalidate approval? Mortgage team and credit risk. | Applications are read-only in this exercise; approval binds to the request revision. | Capture application version/relevant facts and invalidate or re-review when they change. |
| How should a caller recover after losing a decision response? Mortgage integration owner. | Reload the request; decisions are final and a duplicate POST returns 409. | Add decision idempotency if transparent response replay is required. |
