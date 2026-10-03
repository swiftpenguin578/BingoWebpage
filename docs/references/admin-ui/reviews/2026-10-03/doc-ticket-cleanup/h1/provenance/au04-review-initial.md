# AU04 independent source/evidence review — 2 October 2026

Verdict: **PASS**. No actionable findings. This is independent source/evidence review, not UI integration approval or manual acceptance.

Reviewer: `/root/au04_reviewer`, fresh Astra/high, read-only repository review. Recipient/next owner: `/root` orchestrator. No production, test, or repository documentation changes were made by the reviewer.

## Exact reviewed identity

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`
- Branch: `codex/participants-functionality`
- HEAD: `0ec8add9314a65ab66751bf44bbb4f5195ff854f`
- AU04-only patch: `/private/tmp/au04-implementation-20261002/au04.patch`
- Patch SHA-256: `bef67b74e95d432097c3891ee6be34ad93290a3c92aea8f17a43593c19f79145`
- Live manifest: `/private/tmp/au04-implementation-20261002/source.sha256`
- Manifest SHA-256: `c3cbcce29cf80348d1775ecc90f9ca9c8b252878eeca59654a2915c1d784f11b`
- Scope: all 12 paths in `owned-paths.json`, including the complete new integration test and six authority/status files. No whole-branch review of inherited AU01–AU03 work.

All 12 live hashes matched before and after review. Independently reconstructed all 12 current files by applying the supplied patch hunks in memory to captured `before/` contents; captured before hashes matched `before.json`, reconstructed bytes matched live sources, and the patch path set matched the owned path set. Independently confirmed all 29 non-owned baseline identities remain unchanged, including the inherited deleted source identity.

## Scope comparison

Reviewed against current AGENTS.md, CURRENT_STATUS AU04, DELIVERY_PLAN AU04 and 4.2.1, Events E01/E02 brief, and the relevant approved product/data/functional directory and Dashboard contracts.

Required scope delivered:

- All/Current/Past/Hidden populations, population count before phase/name narrowing, authoritative query fields/results and stable column sorts. Default ordering is Live first, dated preparation with unscheduled last, then newest past across phases with stable event-ID ties.
- Nullable capacity and retained Live/past participation through a narrow method on the existing Dashboard owner. The new method reuses unchanged `BuildPopulations` and `BuildImportInfo`; missing actual intervals remain unavailable, import provenance remains explicit, and departed people/identity deduplication follow the existing rules. Dashboard's own population eligibility and totals are unchanged.
- Persisted enabled website Admin authorization, separate persisted SuperAdmin hidden access, and hidden/discarded exclusion from ordinary directory populations. The new participation read independently applies those guards and the existing consistent-transaction requirement.
- Current scheduled failure priority followed by evidence review. Category +N counts the review queue once; existing inbox per-submission/per-failure units remain unchanged. Ordinary setup contributes no attention category.
- Approved behavior promoted into existing authorities. Only the two existing Razor capacity formatting arguments changed for nullable compatibility.

Required backend scope missing: none found. Material implementation without approved mapping: none found. Changed explicit non-goals or unbudgeted table/service/route/policy/job/abstraction: none found. The DTO and method on the existing service are proportionate support for the approved query. No new failure subsystem, persistence, provider operation, layout rewrite, or navigation binding was introduced.

## Evidence inspected and reused

Evidence root: `/private/tmp/au04-implementation-20261002/`.

- `au04-focused.trx`: 7 passed, 1 failed, 0 skipped. The failure occurred while saving controlled retained-participation fixture data, PostgreSQL `23505` on `IX_teams_event_id_name`.
- `au04-retained-corrected.trx`: the same retained-participation scenario passed after unique synthetic team names; 1 passed, 0 failed, 0 skipped. Combined evidence is eight distinct passing tests, not a fresh eight-test run.
- The four directory scenarios directly exercise the PostgreSQL query/projection boundaries: lifecycle/null/tie ordering and filters, retained/import/missing participation and Dashboard parity, current-role/hidden authorization, and failure/category/inbox semantics. The four existing action cases protect current schedule/lifecycle resolution and hidden exclusion.
- `build-final.log`: Release solution build succeeded with zero warnings/errors. Initial nullable Razor and test audit-constructor compilation failures are retained in the handoff/logs and corrected in reviewed source.
- `diff-check.log`, `leak-check.log`, and `protected-source-check.json`: recorded scoped checks passed; protected-source identities independently corroborated as above.

No runtime tests or build were rerun: recorded checks cover the changed risk boundary and review found no concrete unresolved risk requiring additional execution.

## Limits and next action

Layout, filter URL/navigation bindings, participant rendering integration, browser/visual review and manual acceptance remain deferred to the page integration pass. The existing Razor still uses existing participant presentation; this backend ticket deliberately exposes the replacement query data for later binding. Full-suite/release checks were not run or claimed. Existing source remains uncommitted; no staging, packaging, publication, app restart or user-database operation occurred.

The orchestrator may reconcile this AU04 source/evidence PASS and deliver the terminal ticket handoff under its existing authority. No remediation is required by this review.
