# AU10 independent review — PASS

Reviewer `/root/au10_reviewer`, fresh independent Astra/high, assigned by orchestrator `/root` (chat `01a0fdd1-fd2b-7983-bdbd-d468df5f83cc`). Read-only production/document review on 2 October 2026. No required findings.

## Reviewed identity

Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`; branch `codex/participants-functionality`; packaged HEAD `0ec8add9314a65ab66751bf44bbb4f5195ff854f`. Reviewed the seven-file AU10-only patch against the inherited dirty snapshot, not the accumulated HEAD diff. All seven live file hashes and frozen `current/` files match the supplied identity. Final verification and parsed TRX counters are in `evidence.json` beside this report.

- Patch `/private/tmp/au10-implementation-20261002/au10.patch`, SHA-256 `ffe0b4b63de857abe1eb769e291f07262416e043e3e535fa8e71e3a17b3b6c2b`.
- Source manifest SHA-256 `e4592b7929f4dc78869ff557e8351532e08c824055ed475c6d0f186278c8bc79`.

Inherited AU01–AU09/planner/AU15 work, metadata and untracked root `CLAUDE.md` are excluded and preserved.

## Assessment

`Schedule.cshtml.cs:94–162` checks the observed event version before preserving each unchanged displayed value from the authoritative row. Locked values retain their original instants. Genuinely changed editable values still use the established five-minute, invalid-time and ambiguous-time validation. The atomic authorized Serializable/versioned lifecycle save, audit, capacity carry-through, automatic-opening exceptions, overlap/WOM validation, cutoff derivation and lifecycle restrictions remain owned by the existing code. Direct dependencies were inspected to confirm the integration; they were not broadened into a new audit.

`Schedule.cshtml.cs:121–191` routes suitable lifecycle/order/future/missing-time/WOM errors through existing field ModelState entries while retaining overlap, stale and authorization errors at form level. Confirmation is identified by the actual service result rather than inferred from a generic failed save. The two new validation spans in `Schedule.cshtml:24–25` make hidden confirmation/reason errors visible. The existing ModelOnly summary avoids duplicating those errors. No confirmation-policy replacement is introduced.

`Schedule.cshtml.cs:60–92` reads the event and draft state in one query through the existing Admin/visibility/lifecycle route boundary, returns no-store full exact input values plus version/timezone/phase/editability, and adds no write/audit/receipt operation. `event-schedule.js:9–58` copies and freezes full observed and submitted values, preserves submillisecond precision, carries string versions safely, validates readback shape/event/version, and uses GET only. Matching values mean current-state agreement regardless of actor; differing values and failed reads do not alter intent or retry a mutation. Phase/timezone/editability changes remain explicit context. Current transport integration is within the approved bounded API scope.

FUNCTIONAL_CONTRACTS 4.4 now describes the approved pre-first-Live repair and running/paused/finalized-draft editability. The matching overdue-end recovery sentence in the automatic-start section is reconciled for consistency; it does not change scheduler execution/readiness or expand implementation scope.

## Reused executable evidence

Inspected the actual test code, raw logs and parsed TRX counters; did not rerun passing checks.

- Disposable PostgreSQL plus authenticated Razor HTTP: 12/12 focused cases passed. The fixtures explicitly round-trip non-microsecond-aligned UTC values and assert exact microsecond persisted expectations; both repeated-hour offsets are exercised. Cases also cover unchanged overdue opening/start, disabled legacy opening, changed DST/off-step rejection, mapped errors, overlap/stale form errors, full readback, matching competing writes and a one-microsecond difference.
- Named Live-field-error extension: 1/1 passed, checking rendered confirmation/reason errors alongside Admin/hidden/Live/FinalReview boundaries.
- Shipped JavaScript against controlled localhost HTTP: PASS, including discarded mutation response, unchanged state, competing matching values, exact fractional differences, automatic-opening differences, large versions, changed context, and failed/unauthorized/redirected/malformed reads. Frozen intent/baseline, retained mutable draft and GET-only recovery are asserted.
- Release solution build: PASS, zero warnings/errors. Implementer scoped whitespace/leak/inherited-source checks: PASS; exact source identity independently verified here.

The tests discriminate the changed precision/readback risks; controlled HTTP failure evidence is not a production outage claim. Unchanged earlier-ticket checks were not repeated.

## Limits and next owner

This is independent technical review approval of AU10's approved scope, not manual visual acceptance. Ordinary form enhancement/uncertain-save UI, new picker binding, stay-on-Schedule, confirmation table/navigation, reload persistence and full reference integration remain deferred. The present form redirect and confirmation remain. The readback caller must supply its full resolved UTC intent before dispatch as documented.

No source/doc changes, database/provider/app actions, test/build executions, packaging, browser/reference-picture walkthroughs, or further-ticket preparation by this reviewer. Only this report and evidence were written under `/private/tmp/au10-review-20261002`. No automatic approval rejection occurred.

Next: orchestrator reconciles this PASS with the verified seven-file identity, reports AU10 completion and limits to the assigned planner, then stops under the human's explicit AU10 boundary. No remediation owner is needed.
