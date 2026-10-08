# AU04 independent source/evidence review — 2 October 2026

Verdict: **REQUEST CHANGES — one P2 scope omission**. This amends the initial PASS after the orchestrator identified a concrete attention-filter scope ambiguity. All other review conclusions and source identities below remain applicable. This is independent source/evidence review, not UI integration approval or manual acceptance.

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

Required backend scope missing: the approved authoritative attention filter, detailed below. Material implementation without approved mapping: none found. Changed explicit non-goals or unbudgeted table/service/route/policy/job/abstraction: none found. The DTO and method on the existing service are proportionate support for the approved query. No new failure subsystem, persistence, provider operation, layout rewrite, or navigation binding was introduced.

## Evidence inspected and reused

Evidence root: `/private/tmp/au04-implementation-20261002/`.

- `au04-focused.trx`: 7 passed, 1 failed, 0 skipped. The failure occurred while saving controlled retained-participation fixture data, PostgreSQL `23505` on `IX_teams_event_id_name`.
- `au04-retained-corrected.trx`: the same retained-participation scenario passed after unique synthetic team names; 1 passed, 0 failed, 0 skipped. Combined evidence is eight distinct passing tests, not a fresh eight-test run.
- The four directory scenarios directly exercise the PostgreSQL query/projection boundaries: lifecycle/null/tie ordering and filters, retained/import/missing participation and Dashboard parity, current-role/hidden authorization, and failure/category/inbox semantics. The four existing action cases protect current schedule/lifecycle resolution and hidden exclusion.
- `build-final.log`: Release solution build succeeded with zero warnings/errors. Initial nullable Razor and test audit-constructor compilation failures are retained in the handoff/logs and corrected in reviewed source.
- `diff-check.log`, `leak-check.log`, and `protected-source-check.json`: recorded scoped checks passed; protected-source identities independently corroborated as above.

No runtime tests or build were rerun. Existing recorded results remain applicable to delivered behavior; the missing attention-filter contract requires focused proof with its remediation.

## Limits and next action

Layout, filter URL/navigation bindings, participant rendering integration, browser/visual review and manual acceptance remain deferred to the page integration pass. The existing Razor still uses existing participant presentation; this backend ticket deliberately exposes the replacement query data for later binding. Full-suite/release checks were not run or claimed. Existing source remains uncommitted; no staging, packaging, publication, app restart or user-database operation occurred.

The orchestrator should route the single finding below to the existing implementer, then return the stable correction and focused evidence to this reviewer for a named recheck. No broader review or UI integration is required.


## Amended finding — P2: implement the approved backend attention filter

Location: `src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs:150` (query-state properties at lines 30–46 also have no attention input).

The authoritative result currently narrows the selected population only by phase and name. There is no attention-filter query state or predicate. Consequently requesting the approved attention-only query cannot exclude quiet events, despite the row already exposing its authoritative attention category count.

Scope evidence:

- `DELIVERY_PLAN.md:403–407` requires authoritative filters/sorts and query results needed for the reference; only UI URL/navigation/table changes are deferred.
- `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:716` explicitly identifies the pending attention filter, while item 4 at line 747 classifies attention priority/filter as projection work. Item 5 excludes ordinary setup from attention/filter counts.
- The linked reference contract `docs/references/admin-ui/README.md:478–479` explicitly calls the attention filter an **agreed addition**, identifying `attention=1`. This resolves the ambiguity that the E01 prose alone does not enumerate it.
- The broader deferred Events modal/filter/history UI-binding wording at `DELIVERY_PLAN.md:624–629` does not explicitly defer this backend predicate and does not override AU04's authoritative-query requirement.

Smallest required correction: expose and normalize the attention-only query input, apply it to the existing authoritative row/category data while composing with view/phase/name filters, and preserve pre-filter population counts, hidden authorization, shared inbox units and action-projection availability. Only existing real actionable categories qualify; ordinary setup remains excluded. Add focused evidence demonstrating actionable-versus-quiet filtering and composition, preferably by extending an existing scenario. Keep the toggle/chip, URL/history/navigation control wiring and layout deferred. Promote the missing query behavior into the existing AU04 authority wording so the implementation baseline no longer omits it.

After this targeted scope check all 12 frozen source hashes still match the reviewed manifest. No source edits, tests, builds or broad re-review were performed in this follow-up. The earlier PASS is superseded until this named omission is remedied and rechecked.

Original report provenance: `review-initial.md` preserves the superseded initial PASS byte-for-byte, SHA-256 `e9872111fdf43aa510cf269c3a69cf4818505969c4b97ab2d7a250174de148c2`. Use this amended report for the current verdict.
