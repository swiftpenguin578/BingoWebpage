# B-L1 — absent imported form after reopening

The read guard now depends on form absence, regardless of lifecycle state. Imported history remains explicitly absent/read-only after reopening and legacy re-finalization; no form or answers are created. The existing Q11 register row now names these states.

Executed Release PostgreSQL filter `FullyQualifiedName~ImportedArchivedWithoutFormIsReadOnlyAndExplicitWithoutCreatingHistory`: PASS3/3 (Archived, AwaitingFinalReview, Finalized). Each verifies GET/Current200, readonly/absence message and snapshot, refused add, no SignupForm/SignupQuestion/audit creation. The new reopened case initially expected the terminal Manage redirect; it now asserts its existing service-refusal SignupSetup redirect exactly, while terminal cases retain Manage. No refusal behavior changed. `git diff --check`: PASS.
