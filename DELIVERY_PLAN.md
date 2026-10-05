# Delivery plan

**Active plan:** 2026-09-01. This document owns remaining delivery,
documentation/UI order, and release gates. Current page approval/status is
owned solely by [`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md).
[`CURRENT_STATUS.md`](CURRENT_STATUS.md) remains the authority for checkout
state, blockers, limitations, current work, and immediate ownership. Global UI
rules and implementation ownership are defined by [`UI_SYSTEM.md`](UI_SYSTEM.md).

## Luck percentile and KC comparison — approved implementation, 2026-10-01

**Status:** technically complete and independently reviewed **PASS** after the
explicitly approved changed Sol 6.1/high recovery. All L0–L6 technical requirements
and remaining named findings are resolved. Manual visual acceptance is deferred.
Controlled test fixtures and migration source were used; real provider/user-database
operations, packaging and deployment were not performed or authorized.

**Source baseline:** inspected `codex/participants-functionality` in
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, including
its reviewed committed Participants/Dashboard work. The 573d checkout is stale.
The execution checkout is this same active worktree and branch; do not use stale
573d or Claude's design-reference checkout. Preserve the committed Participants,
Dashboard and documentation work. Recheck only changed or missing dependencies.

### Assignment and ownership

Planner: `UI Planner`, app chat `01a0ec9a-76e3-7252-9850-3f260c612e59`
(host `local`). Recovery orchestrator: native `/root`, app chat
`01a0f855-c75f-7a13-8202-6cea96ae60d1` (host `local`), separately authorized
by the user after the prior `/root/luck_redesign` dispatch hit its native thread
limit. Previous Luna implementation/remediation stopped with three review gaps.
The user explicitly approved new implementer `/root/luck_sol_recovery`, exact
`gpt-6.1-sol` / high, for one changed bounded recovery. The four-file correction
and focused checks completed; same independent reviewer `/root/luck_independent_review`
(Sol 6.1/high) returned PASS on the three findings/direct consequences. The other
22 reviewed candidate files remain unchanged. Prior Luna is idle and no further
worker/review layer or scope was added. Orchestrator retains its approved Sol 6.1/high
setting and final scoped planner callback. Evidence is retained in `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/luck/`
(final manifest SHA-256 `c3ecfa73d424021eb6e6456e321157ad3c7d71ed9239ff24fbaf5373af4bf7be`; disposition `independent-review-pass.txt`).
Manual visual acceptance is recorded in UI_PAGE_MATRIX.
Follow DELIVERY_PLAN section 4.2.1; no extra coordinator or routine verifier.

### Agreed outcome and boundaries

- Luck becomes 0–100%: `100 * (P(X < received) + 0.5 * P(X = received))`.
  Do not recenter expectation at 50 or replace the distribution with an expected-drop
  ratio. Preserve frozen personal probabilities, conditional probabilities, reward
  rolls, mutual exclusion, independent roll groups and numerical safety limits.
- Stats keeps its existing Luck container, team/player views, comparison and search.
  Default mode is **Luck %**; an in-container toggle selects **KC difference**.
  No additional container, boss selector or EHB mode. KC totals deliberately do not
  adjust for kill speed/difficulty. Tooltip: "KC totals don’t account for differences
  in boss kill speed." This limitation is accepted, not a blocker.
- Tile views expose one overall Luck result and each relevant boss/activity's own KC
  and Luck; contributor rows must also use the matching activity result. Never repeat
  the overall percentage as if it were each boss's percentage.
- Both modes come from the same saved snapshot. Evidence approval/reversal does not
  recalculate, clear or routinely warn about an existing result. Quiet **Last updated**
  metadata is sufficient. Upstream delay and up to an hour of normal age are accepted.
- Successful normal WOM fetches calculate Luck using the returned activity and current
  approved evidence. Continue normal/manual refresh eligibility through final review;
  stop ordinary refreshing once results are published. Attempt a final normal refresh
  during publish/archive, respecting the existing hourly limit. Failure or a rate-limit
  skip retains the prior result and does not block publication; report actual refresh
  failure through the existing operation feedback. No separate Luck refresh mechanism.
- Convert supported historical checkpoints once from their own retained inputs, never
  by relabelling signed scores or mixing in newer evidence/rates. This is the agreed
  bounded migration exception to fetch-only calculation.
- No broad public or Admin visual redesign, altered drop-credit rules, new external
  service, generic cache framework, or changes to official scores/winners/evidence.

### Source findings and relevant owners

1. `src/Bingo.Domain/Events/LuckScoreCalculator.cs` already builds bounded binomial
   distributions and convolutions, computes a mid-rank, then normalizes around the
   expected-count rank into -100..100. Replace the final normalization only.
2. `src/Bingo.Infrastructure/Stats/PublicStatsService.Luck.cs` reads/rescores checkpoint
   JSON, falls back to current evidence plus cached activity, and writes checkpoints.
   Approval/reversal in `SubmissionService`, lifecycle/finalization and WOM sync call
   `RefreshCheckpointAsync`. All non-fetch recalculation paths need removal, not just
   evidence callbacks. Evidence revisions remain for other consumers/concurrency.
3. `PublicStatsService.Tile.cs` retains tile-specific received totals and per-metric KC,
   but its metric DTO lacks its own Luck result. Preserve tile-attributed evidence;
   event-wide source counts must not leak into a selected tile's observed count.
4. `EventCompetitionSynchronizationService` and `.MetricCache.cs` own normal hourly
   fetches, leases, generation/source/assignment fencing and metric availability.
   Manual eligibility, due processing, lease acquisition and lease finalization all
   currently require Live. Extend the relevant gates consistently for final review.
5. `EventStatsLuckCheckpoint` is one JSONB row per event with schema version 1, an 8 MiB
   limit, evidence/batch/generation/fingerprints and calculation/provider times.
   PostgreSQL currently constrains `schema_version = 1`; changing the version needs
   an EF migration, designer and model snapshot. No second snapshot table is planned.
6. `BoardPublicationQueries.Luck.cs` and `EventLuckOutcomeBasis` retain first-approved
   event mechanics and first validated metric binding. Global catalogue edits do not
   rewrite them. Existing tests/constraints protect this deliberate historical rule.
7. `IPublicStatsService.cs`, Stats Razor/JSON, `stats-adapter.js`, `stats-page.js`, Stats
   CSS and `_TileActivity.cshtml` consume Luck. `PublicBoardService.GetTileAsync` feeds
   the tile/sidebar projection. Migrate these active consumers and shared previews;
   no second transport endpoint is needed. Official placement data is a separate
   owner; do not rewrite official history as part of derived Luck conversion.

### Calculation and projection contract

**Percentile:** remove expected-rank interpolation and signed scaling; return
`100 * MidRank(distribution, received)` with a bounded numerical clamp. A deterministic
distribution's only possible outcome has rank 50, but no-activity/missing-data UI
states remain unavailable rather than displaying a fabricated 50. Impossible observations
and excessive computation remain unavailable. Use at most one decimal consistently
in percentage presentation; retain unrounded values for sorting/calculation.

**KC difference:** calculate per character and boss/activity metric, then sum those
balances for each player/team. For a component scope let `lambda` be the expected
number of eligible item outcomes per kill: sum `rolls * effective personal probability`
over deduplicated eligible outcomes, respecting the retained mechanics. Let `r` be
the approved eligible outcome count and `k` the corresponding recorded KC:

`KC difference = r / lambda - k`.

This defines the comparison for the combined eligible drop set, consistent with the
existing count-based Luck model. It does not assign a separate inverse-rarity reward
to each item. Independent groups may yield multiple outcomes per kill; do not replace
lambda with the probability of at least one drop. Subtract each character/activity's
KC once, never once per item, roll group or duplicated tile placement. For multiple
characters/rates calculate each balance first, then sum; never divide pooled drops
by an unrelated average rate. Lambda zero/unsupported, missing activity or invalid
inputs cannot yield a number. One drop at 90 KC with rate 1/100 gives +10; one at 120
gives -20; two at 120 gives +80; zero at 120 gives -120. This is an event-total balance,
not a measured dry streak since the last drop. Show signed KC with at most one decimal;
normalize rounded negative zero. Overall sums count all kills equally, as agreed.

**Results:** extend existing DTOs with KC difference and explicit activity breakdowns
keyed by boss identity AND metric, each containing KC, received/expected counts, Luck,
availability and estimate flags. Retain a distinct aggregate result at event/team/player
and tile/team/contributor scopes. Store inputs needed for tile-specific activity counts
and migration; do not rely on current evidence to reconstruct an old breakdown.
All values in a displayed snapshot share batch/evidence/algorithm provenance.

### Snapshot publication, retention and partial data

- Use the existing successful synchronization finalization transaction and event lock.
  After checking the lease, competition, generation, source set and assignments, capture
  approved evidence and calculate/persist with that accepted activity batch. Preserve
  atomic rollback, cancellation handling and stale-writer fencing.
- Reads only project saved results; remove fresh-evidence/cached-KC fallback and routine
  read-time rescoring. A failed fetch cannot overwrite a good checkpoint or its times.
- A successful HTTP response may still be partial. Prefer retaining a prior compatible
  complete snapshot as a whole over publishing a degraded replacement. Do not mix new
  boss results with an old aggregate under one timestamp. Without a complete prior
  snapshot, a successful batch may expose independently complete scopes while dependent
  aggregates remain unavailable. Never discard an existing calculable scope merely
  because a later partial response cannot calculate it; retain the prior whole snapshot.
- Missing/unranked/estimated/zero activity keep honest distinct states. Missing required
  activity blocks the affected aggregate, not independently complete scopes. Numerical
  limits may block a percentile without blocking a sound KC balance from the same inputs.
- Separate snapshot compatibility from ordinary age/evidence changes. Reversal is no
  longer a presentation invalidator. Normal Live -> review -> archived transitions keep
  saved Luck. Preserve privacy, public visibility and invalidation for genuinely different
  competition/assignment/source identities; never label another roster's result as current.
- If a candidate exceeds the payload bound, retain the previous snapshot or return
  unavailable when none exists; record a diagnostic. No truncation or read-time calculation
  escape hatch. No new tables solely to bypass the bound without measured need.
- WOM success is not a claim that OSRS has caught up. Preserve upstream/fetch times;
  do not fabricate a drop-time cutoff from submission/approval timestamps.

### Final review and publish/archive boundary

Reuse existing WOM scheduling/manual fetch and provider limits during AwaitingFinalReview.
Keep website-owned competition dates, including manual-end/resume history, authoritative.
The fetch must describe the correct event window, not post-event activity accidentally
included by a provider schedule mismatch. Validate this at the existing integration boundary.

Before committing publish/archive, attempt a normal refresh only when eligible under
the hourly/retry/lease limits. Do not hold a database transaction open across HTTP. After
the attempt, revalidate existing event/evidence/finalization concurrency tokens; a changed
event must not be finalized using a stale reviewed state. If a fetch is skipped or fails,
publication can proceed with the last snapshot and its real timestamp. Do not enqueue
an unbounded post-archive refresh, bypass provider limits or make Luck a publication gate.
Repeated archive/publish calls must not fetch twice. Existing authorized reopen/unfinalize
flows re-enable refresh only when they actually return the event to an eligible phase.

### Versioning, historical conversion and catalogue policy

Use a version-2 checkpoint contract that unambiguously identifies percentile semantics
and KC balances. Expand the DB constraint to support safe legacy reading/conversion;
never flip version 1 to 2 without actually rebuilding supported results. New writes
produce version 2. Update stale-writer guards so old versions cannot overwrite new ones.

Provide a bounded, idempotent explicit conversion step for historical version-1 payloads.
Only use their retained observations, received totals, rates and coherent attribution.
Preserve original calculation/fetch/upstream times; record conversion/algorithm provenance
separately. Convert whatever scopes have complete retained inputs; leave missing legacy
tile/boss attribution unavailable. Do not guess from current publication/evidence. Malformed
or incomplete legacy payloads remain safely unavailable with diagnostics. No public-read
writes, mass provider fetch, changed evidence/placements or invented historical checkpoints.

Global catalogue rate/mapping edits affect future event bases, not immutable past bases.
Relevant changes to an event's eligible outcome set or legitimate first metric binding
change its source fingerprint, invalidate incompatible presentation, and require a matching
normal fetch. This is distinct from approval/reversal retention. No new mechanism for
rewriting frozen event mechanics is included.

### UI semantics and shared guidance

Keep the existing public composition. Luck uses a fixed 0–100 scale; remove plus signs,
negative tests and the zero-as-expected centre. KC difference uses signed values and a
zero-centred scale appropriate to that mode. Sort and select extremes using the active
mode's unrounded value; preserve search, pinning, team drill-down and unavailable rows.
Use neutral snapshot age rather than routine stale/error wording. Preserve meaningful
provider failure/estimate/missing information without exposing private diagnostics.

One shared localized explanation should serve Stats and tile help: Luck compares approved
drops with modeled outcomes at the same recorded activity and retained rates. Higher
percentages mean luckier outcomes; expectation is not forced to the midpoint. KC mode
explains the rate-equivalent balance and the agreed short kill-speed tooltip. No technical
distribution terminology in ordinary UI. Single-boss tiles should not duplicate identical
aggregate/boss rows unnecessarily. User supplies visual acceptance of the necessary changes.

### Tickets, dependencies and focused proof

| Ticket | Owned result | Acceptance/checks |
| --- | --- | --- |
| L0 — readiness/authority | Confirm changed source and exact final-review/finalization/migration wiring; promote these approved decisions into product/functional/data/UI authorities | One bounded readiness review; no reopening agreed product decisions or broad audit |
| L1 — calculation/contracts | Percentile, KC balance, explicit activity results and reusable bounded distribution work | Deterministic independently enumerated PMFs; no/one/expected/lucky/unlucky counts; no recentering; deterministic/impossible/bounds; mutually exclusive, independent and conditional cases |
| L2 — projections | Event/team/player and tile/contributor aggregate + boss results | Real catalogue DKS ring fixture: Prime Seers, Rex Berserker + Warrior, Supreme Archers; per-boss truth, aggregate not average, exact tile attribution; KC de-duplication across outcomes/rolls/placements and mixed rates |
| L3 — snapshot/versioning | Fetch-only atomic publication, compatibility/retention, schema migration and retained-input conversion | Real PostgreSQL approval/reversal retained until fetch; success/failure/partial/no-prior; stale writers, source change in flight, rollback, schema constraint, idempotent legacy conversion and payload limits |
| L4 — lifecycle refresh | Final-review eligibility and bounded final fetch attempt | Hourly/cooldown/lease gates, manual-end event window, concurrent evidence/finalization, failed/skipped final fetch, no duplicate fetch and no post-publication scheduled refresh |
| L5 — presentation | Both Stats modes and correct boss/contributor display | Focused adapter/render tests: fixed percentile vs signed KC geometry, mode sorting/pinning, formatting/rounding, same timestamp, unavailable states, reduced motion and localization; user visual acceptance |
| L6 — delivery reconciliation | Fresh independent stable-diff review, named remediation/recheck and recorded status | Scoped build/format/diff plus relevant existing suites; distinguish implemented, executed, reviewed and manually accepted; no packaging without authority |

L0 precedes implementation. L1 establishes the shared contract. L2 and L3 may overlap
only with disjoint source ownership and a stable agreed DTO; coordinate their shared
Stats files rather than concurrent conflicting edits. L4 depends on L3's snapshot
boundary. L5 uses the stable contract; L6 follows the complete candidate. Use existing
workflow roles, an independent reviewer, and no extra coordinator/verifier chain.

Use the cheapest meaningful level: calculator tests for math, projection tests for
grouping and real PostgreSQL for persistence/concurrency. Timestamp fixtures must use
deterministic UTC/microsecond precision and exercise non-microsecond round trips where
fingerprints rely on storage. Retain current numerical work guards; reuse prepared
components/distributions where safe rather than rebuilding a full tile per rendered row.
Measure representative multi-account/multi-boss payload and calculation sizes before
adding further caching. No property-only/framework tests or repeated full regression runs.

### Execution disposition — reviewed technical completion, 2026-10-01

L0 authority reconciliation, L1 math, L2 projections, L3 snapshot/version/conversion,
L4 lifecycle/operation feedback and discriminating window proof, and L5 presentation/
affected tests are implemented and executed at their relevant boundaries. L6 source,
recorded evidence and stable identity review passed. Recovery PG 5/5, complete Node
67 passed / 0 failed / 2 existing optional skips, Release compilation, scoped formatter
and diff checks pass; prior cleared proof is retained. User manual visual acceptance
was granted on 2026-10-02 after populated demo inspection and is recorded in
UI_PAGE_MATRIX. Final evidence: `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/luck/`,
final manifest SHA-256 `c3ecfa73d424021eb6e6456e321157ad3c7d71ed9239ff24fbaf5373af4bf7be`; disposition `independent-review-pass.txt`. CURRENT_STATUS owns the next assignment.

### Remaining risks and stop boundary

No outstanding product decisions from this discussion. Readiness must validate the
legacy data sufficient for each converted scope, the archive/provider window boundary,
and existing catalogue independence assumptions. Raise a concrete unsupported mechanic
or required scope expansion rather than silently broadening the probability engine.
Player/team roster histories and rate changes must not accidentally change observation
ownership. Percentiles remain a model of approved eligible drops, not all unseen gameplay.

This section records the implemented Luck behavior and its retained historical
boundaries. Older signed-score, reversal-invalidation, read-time-rescore and
Live-only descriptions are historical source material; the active Luck contract
and current implementation govern delivery.
**Next permitted action:** see CURRENT_STATUS.md for the active assignment and
stop boundary. This historical Luck closure does not dispatch later tickets.


## Review preparation checkpoint — 2 October 2026

Implementation/reference backup is pushed: `acf8ba9` (AU01–AU10 and planner docs),
`be0014e` (references/evidence), `1e8d457` (unchanged CLAUDE.md only).
Base main is `22af254c893bb51e7820d84fc4154ff9af3bcc90`.
This is not a release or permission to restart implementation. AU11 onward stays
stopped. Planner reconciled requirements and the 47 source-finding dispositions; the approved
summary and complete finding map are in
[the existing register](docs/references/admin-ui/FUNCTIONALITY_CHANGES.md#review-reconciliation-and-requirements-summary--2-october-2026).
The user approved the clarified product changes. The final documentation checkpoint
precedes a short whole-branch handoff, not a prescribed area-by-area review procedure.
Claude chooses the review method and checks requirements/tickets as well as code.
Distinguish defects, requirement mismatches and known unfinished tickets; name exact
base/candidate commits. Scope includes the original simplification, not just AU work.
No new independent code review or tests are claimed by this documentation pass.

## Admin functionality queue — recorded 1 October 2026

The user originally authorized the AU01–AU14 / RC01–RC04 sequence, then explicitly
stopped after AU10. AU01–AU10 are committed and technically complete; no next-ticket
implementation or dispatch is currently authorized. Reconciliation records later
agreed outcomes and remaining technical scope for review. On a future explicit resume, execute one ticket at a time in
the approved order. Recording a ticket or approving requirements is not a resume.
Routing follows the current policy in `AGENTS.md` ("Active workflow and model
defaults"). Do not skip unfinished tickets.
This replaces the earlier queue-only authorization. Before each implementation,
promote its approved behavior into the relevant existing authority sections.
Use this section as the execution owner; the reference's
`docs/references/admin-ui/FUNCTIONALITY_CHANGES.md` links here and maps pages.
Do not create a competing general ticket inventory or automatically expand scope.

Baseline: `codex/participants-functionality` in
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Preserve all Participants, Dashboard and Luck work. Recheck only changed relevant
sources at dispatch. AU tickets own the application; the separately authorized
RC tickets below own bounded reference corrections. Claude owns visual design and
canvas synchronization. No canvas/reference redesign, live provider calls, user-database
mutation, staging, commit, push or deployment is authorized by this queue.

### Queue and delivery state

Luck, Participants and Dashboard backend work is complete and committed; remaining
application tickets stay queued under the execution authorization above. AU01–AU10
are technically complete with executed focused proof and independent review PASS.
AU13 is separately complete through the F8 checkpoint below; AU11–AU12 and AU14–AU24
retain the approved/proposed statuses in the table below and are not dispatched by
this cleanup. Shared files/dependencies can inform sequence without merging tickets
into a broad pass.

| Order / ID | Application outcome | Depends on | Implementation | Executed proof | Independent review | UI integration / manual acceptance |
| --- | --- | --- | --- | --- | --- | --- |
| AU01 | Restore cannot produce two visible current events | Reject conflicting restore; reuse current-event boundary | Technically complete; committed at checkpoint 1e8d457; manifest cb8d95f5 | PASS 9/9 PostgreSQL/HTTP plus 3/3 quarantine regressions; Release build | PASS — fresh Astra/high; report 8b78c557; no findings | Deferred |
| AU02 | Enforce signup-code length at the server boundary | Luck complete | Technically complete — /root/au02_implementer; committed at checkpoint 1e8d457; manifest 286f23b0; planner reconciled completion; prior callback failure retained | PASS 5/5 authenticated PostgreSQL; Release build 0 warnings/errors; scoped diff/leak PASS | PASS — fresh Astra/high /root/au02_reviewer; report 9c18fe67; no findings | Existing route; new UI/manual acceptance deferred |
| AU03 | Duplicate-safe event creation and uncertain-outcome lookup | Luck complete | Technically complete — /root/au03_implementer; committed at checkpoint 1e8d457; manifest feafacaa; planner directly reconciled completion and all 22 hashes; callback rejection retained | PASS 13 distinct PostgreSQL/request cases + reset 1/1; final Release build; slug 6/6; architecture 2/2; scoped diff/leak/protected-source checks | PASS — fresh Astra/high /root/au03_reviewer; sole P2 reset finding resolved; report aa90ac4a | Existing route; new modal/UI/manual acceptance deferred |
| AU04 | Events directory ordering, retained counts and attention projection | Dashboard backend available | Technically complete — /root/au04_implementer; committed at checkpoint 1e8d457; corrected manifest c59d2745 | PASS: 8 distinct PostgreSQL cases (7/8 + fixture-corrected 1/1); final Release solution build; expanded attention case 1/1 + affected Release compile; scoped diff/leak/protected checks | PASS — fresh Astra/high /root/au04_reviewer; sole P2 attention-filter omission resolved/rechecked; report 673b0641 | Layout/URL/control/participant bindings and manual acceptance deferred |
| AU05 | Signup setup stale-edit protection and settings version responses | Existing signup services | Technically complete — /root/au05_implementer; committed at checkpoint 1e8d457; manifest 387426bb | PASS: 25 distinct PostgreSQL/HTTP cases across corrected runs; final Release build 0 warnings/errors; scoped diff/leak/protected checks | PASS — fresh Astra/high /root/au05_reviewer; no required findings; report cfb67191 | UI/per-card uncertainty binding and manual acceptance deferred |
| AU06 | Duplicate-safe question/account-field creation | AU05 version contract | Technically complete — /root/au06_implementer; committed at checkpoint 1e8d457; manifest 54af691e | PASS: 12 distinct PostgreSQL/HTTP cases across corrected runs; final Release build 0 warnings/errors; scoped diff/leak/protected checks | PASS — fresh Astra/high /root/au06_reviewer; no required findings; report bcf9392b | Frontend draft/uncertainty binding, AU07 feedback and manual acceptance deferred |
| AU07 | Explicit required-to-optional outcome when first response arrives | AU05/AU06 result contract | Technically complete — /root/au07_implementer; committed at checkpoint 1e8d457; manifest 93c76d18; callback rejected; planner reconciled directly | PASS: 22 distinct PostgreSQL/HTTP cases across corrected runs; final Release build 0 warnings/errors; scoped diff/leak/protected checks | PASS — fresh Astra/high /root/au07_reviewer; no required findings; report ca110d17 | Frontend uncertainty binding and manual acceptance deferred |
| AU08 | Identity field-level conflict handling | Existing Identity concurrency/timezone rules | Technically complete — /root/au08_implementer; committed at checkpoint 1e8d457; manifest d74176f8 | PASS: 14 distinct PostgreSQL/HTTP cases across corrected runs; controlled timezone fixture; final Release build 0 warnings/errors; scoped diff/leak/protected checks | PASS — fresh Astra/high /root/au08_reviewer; no required findings; report 912763bd | Conflict-choice frontend integration, AU09 and manual acceptance deferred |
| AU09 | Identity uncertain-save readback | AU08 result/field contract | Technically complete — /root/au09_implementer; committed at checkpoint 1e8d457; manifest a93e9584 | PASS: 7 distinct PG/HTTP cases across corrected fixture runs; readback/timezone and named multiline transport; Release build 0 warnings/errors; scoped checks | PASS — fresh Astra/high /root/au09_reviewer after single P2 named correction; report f5547bcd | Full ordinary-save/new-reference UI binding and manual acceptance deferred |
| AU10 | Schedule instant preservation, field errors and uncertain readback | Existing versioned schedule save/read | Technically complete — /root/au10_implementer; committed at checkpoint 1e8d457; manifest e4592b79 | PASS: 12/12 PostgreSQL/HTTP; named Live check 1/1; shipped controlled HTTP transport; Release build 0 warnings/errors; scoped checks | PASS — fresh Astra/high /root/au10_reviewer; no required findings; report 24f0870c | Explicit user stop after AU10 report; no next-ticket work; full UI/manual acceptance deferred |
| AU11 | Tile-local manual EHB override for every objective type | Existing calculation/approval/evidence boundaries | B2 implemented: `a628cda` | Focused PostgreSQL/calculator/build passed; au-b2/au11.md | Pending Claude | Controls/manual acceptance deferred |
| AU12 | Credited EHB before score time in placement order | New events only; existing event rules retained; AU11 values reused | B2 implemented: `0f79ede` | Focused PostgreSQL/Up-Down/ranking/build passed; au-b2/au12.md | Pending Claude | Deferred |
| AU13 | Board planning team-size estimate editable after draft finalization | Existing board statistics/settings boundary | Technically complete — F8 checkpoint `6d33ce6`; the post-finalization lock removed here originated in `525d5d1` (BR-2/BR-9) | Focused PostgreSQL 3/3, browser markup 1/1, diff check; evidence `docs/references/admin-ui/reviews/2026-10-03/f8-au13-checkpoint.md` | Accepted in the F1–F9 Claude source review; Claude did not rerun tests | Deferred |
| AU14 | Teams: authoritative uncertain-action readback | Existing commands, immutable pick/team/member IDs and full intent | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending `d3f2d88`; RC04/DRF binding pending | Focused PostgreSQL 7/7 PASS; docs/references/admin-ui/reviews/2026-10-04/au-b5/au14.md | Pending external Claude recheck | Deferred |

| AU15 | WOM: remove typed FETCH confirmation | Existing refresh guards | Implemented by B1 Luna/max; local checkpoint awaiting Claude review; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b1/AU15.md` | Browser markup 4/4; Release build 0 warnings/errors; current and Step0 baseline PostgreSQL/HTTP runs reach the route without `FETCH` and reproduce the unchanged schedule assertion failure (see evidence) | Pending — Claude commit-by-commit review | Deferred |
| AU16 | Audit: restore approved hidden-event history and exact filters | Existing audit presenter/query and hidden-history authority | Implemented by B1 Luna/max; local checkpoint awaiting Claude review; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b1/AU16.md` | PostgreSQL/DST/action-area coverage 2/2; authenticated C11 hidden-history coverage 2/2; EventQuarantine retention 1/1; existing structured paging 1/1; diff check PASS | Pending — Claude commit-by-commit review | Deferred |
| AU17a | Review: current/released Playing assignments of current/former members of the submission’s own team | D11 option b; Informational excluded; retained identity mapped through results | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending `ebd0ef2`; RC07 binding pending | Focused PostgreSQL 7/7 PASS; docs/references/admin-ui/reviews/2026-10-04/au-b5/au17a.md | Pending external Claude recheck | Deferred |
| AU17 | Review: Contribution line and safe uncertain-save readback only | Shared approval allocation; refined BR-1; no per-account context | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending `397d4c9`; RC07 binding pending | Focused PostgreSQL 16/16 PASS; docs/references/admin-ui/reviews/2026-10-04/au-b5/au17.md | Pending external Claude recheck | Deferred |
| AU18 | Final Review: per-version final WOM outcome and reopen/version history only | Existing publish/reopen refusals unchanged; no current-event readiness row | B2 implemented | Focused PostgreSQL/history/localization/build passed; au-b2/au18.md | Pending Claude | Deferred |
| AU19 | Board: tile-targeted approval issues, version comparison and authoritative readback | Existing leases/snapshots/calculators and late-evidence guards | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending `992f6b1`; RC05 binding pending | Focused PostgreSQL 62/62 PASS; docs/references/admin-ui/reviews/2026-10-04/au-b5/au19.md | Pending external Claude recheck | Deferred |
| AU20 | WOM: exact configured-window matching, structured outcomes and replacement/recovery | Existing WOM guards, operation state and schedule boundary | Approved; queued, not dispatched | Not run | Not run | Deferred |
| AU21 | Catalogue: explicit shared-item adoption, rename and recovery scope | Existing catalogue/item mapping and version services | B4 remediation implemented by Luna/max in `c4c52a4`; external Claude review pending; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b4/remediation/` | Focused PostgreSQL AU21 remediation 7/7; Release build and diff check passed | Pending — Claude commit-by-commit review | Current-page/new UI binding and manual acceptance deferred |
| AU22 | Accounts: accurate projections and target-bound reset response | Existing account/reset/authorization services | Implemented by B1 Luna/max; local checkpoint awaiting Claude review; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b1/AU22.md` | PostgreSQL AccountOverview 3/3; authenticated PostgreSQL/HTTP reset target/readback 1/1; Accounts BrowserTests 2/2; overlay Node fixture PASS; diff check PASS | Pending — Claude commit-by-commit review | Deferred |
| AU23 | Catalogue: ordinary rate-text rolls and SuperAdmin advanced mechanics | Existing catalogue authorization, validation and audit; final-chance decision below | B4 backend in `00dfd40`, with D8 snapshot/deployment remediation in `c2b00b2`; external Claude review pending; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b4/remediation/` | Focused PostgreSQL AU23 2/2; Cat01 mechanics 2/2; retired metadata 1/1; AU21 regression 2/2; D8 snapshot 1/1; Release build and diff check passed | Pending — Claude commit-by-commit review | Current-page/new UI binding and manual acceptance deferred |
| CAT-1 | Catalogue: informational activity team size | Existing BossActivity persistence and Catalogue activity contract | B4 remediation implemented by Luna/max in `c2d2220`; external Claude review pending; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b4/remediation/` | Focused PostgreSQL handler 3/3 including ordinary-Admin HTTP binding; migration rehearsal 1/1; D8 snapshot 1/1; Release build and diff check passed | Pending — Claude commit-by-commit review | Current-page/new UI binding and manual acceptance deferred |
| AU24 | Accounts: typed ownership transfer destination confirmation | Existing ownership/version/session services | Implemented by B1 Luna/max; local checkpoint awaiting Claude review; evidence `docs/references/admin-ui/reviews/2026-10-04/au-b1/AU24.md` | Typed-confirmation PostgreSQL 1/1; existing transfer PostgreSQL coverage 9/9; Accounts BrowserTests 2/2; Release solution build 0 warnings/errors; Playwright Chromium fixture blocked at launch (see evidence); diff check PASS | Pending — Claude commit-by-commit review | Deferred |

The original B5 gate failures and limited corrective rerun remain historical
evidence in `docs/references/admin-ui/reviews/2026-10-04/au-b5/docs-register.md`.
The accepted test-health batch resolved those cases and passed the user-executed
whole suite (1,921/1,921) at `26e2434`, recorded in `CURRENT_STATUS.md`.
B5 remediation focused evidence is under
`docs/references/admin-ui/reviews/2026-10-04/au-b5-remediation/`; its unfiltered
whole-suite gate must run on the final commit. No overall remediation pass or
Claude recheck is claimed here. All UI binding remains pending.

For each ticket retain owner/model, changed-source identity, exact check/evidence
paths, unresolved limitations and next owner when work starts. Replace Queued with
In progress / Blocked / Implemented as warranted; keep Executed and Reviewed
separate. Record failures/skips/deferred acceptance honestly. Do not delete a
completed ticket or label the page complete because its backend passed review.
Applicable focused proof and one fresh independent review precede technical
completion; reuse the same implementer/reviewer for named remediation/rechecks.
Use AGENTS.md model/routing policy and explicit assignment overrides; do not infer
a new override from an earlier ticket. Manual acceptance belongs to UI_PAGE_MATRIX.

### UI integration and open-finding ownership — proposed D8 order (3 October 2026)

This is a documentation-only ownership ledger. It does not dispatch a ticket or
resume the stopped queue. The source anchor for every owner below is the
[H5 cleanup handoff](docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/handoff.md#h5--ticket-ownership-for-open-findings-p-2-and-the-no-owner-findings).
The D8 proposal keeps the remaining AU tickets first, then the page integrations,
then the final candidate, R-3 rehearsal, and deploy. Every page integration must
inventory its current handlers and routes and explicitly retain, redirect, or
retire each old owner; no orphaned editor or compatibility route is left by
inference. This applies the existing integration requirement at
`docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:571-583`.

| Proposed place | Ticket | Owner boundary and acceptance |
| --- | --- | --- |
| A1 | AU11, AU12, AU14, AU15, AU16, **AU17a**, AU17, AU18, AU19, AU20, AU21, AU22, AU23, AU24, **CAT-1** | Proposed AU order; AU13 is complete through F8. AU17a is the separately approved full-pool correction. AU17/AU18 are approved with the narrowed Step 0 scopes; AU19 is approved. Only B1/B2 are dispatched by the current assignment. CAT-1 is the approved informational activity team-size move below. The active AU lane assignment below supersedes this earlier proposed AU order. |
| B1 | **RC01 → RC02 → RC03 → RC04 → RC05 → RC06 → RC07 → RC08 → RC09 → RC10 → RC11** | Proposed reference-correction order, after their owning application contracts settle and before affected binding. RC01–RC04 are queued; RC05–RC11 remain proposed. This slot does not approve proposed scope or authorize editing frozen artifacts now. |
| C1 | **P-1 — Participants integration**, including RL-1/P-7 and P-3 | `01-participants.md` P-1/P-2: approved routes/new reference are unbound. Bind structured Add/Restore capacity outcomes (P-6/D2: old results omit effective cap/+1 outcome); retire or redirect obsolete Restore overloads. `14b-g3-g6-review.md` G3b-3: show manual-team members in lists/waiting positions. Execute P-3 and RL-1/P-7 below. Preserve drawer/history/dirty-state, authorization and selected-person-only capacity rules. Route/readback/error/navigation and page acceptance required. |
| C2 | **DB-1 — Dashboard integration**, including DB-2/DB-3 and DB-6 | `02-dashboard.md` DB-1: GetAsync has no page. D7 requires **DB-2** (Confirmed participants removed from a pre-Live roster or never placed remain in WOM's sync fingerprint but disappear from Dashboard's expected set, making EHB Unavailable); align compatibility sets and prove a removed-but-Confirmed fixture. **DB-3**: one imported event makes mixed-history approved-submissions headline unknown; count platform submissions while excluding reconstructed imports, keep imported-only unavailable. **DB-4** separately owns missing provisional/import/tracking-start/latest-event metadata; **DB-5** separately owns phase-relevant next date. DB-7's equal-start cohort and Provisional definitions are documentation rules, not D7's two bugs. Execute DB-6; preserve event links/ended recap/unavailable states and route retirement. |
| C3 | **EI-2 — Events and Identity integration**, including RL-1/EI-3 | `04-events-identity.md` EI-2: new directory/Create/Identity reference and recovery controls are unbound. Bind duplicate-safe creation, field conflict choices, timezone and uncertain readback; render uncertainty without requiring DB recovery (RL-1/EI-3). Preserve stable routes and posted drafts; direct entry/reload/conflict/permission/page acceptance. |
| C4 | **OS-1 — Signup setup / Schedule / Overview integration** | `05-overview-signup-schedule.md` OS-1: combined Signup controls and Schedule/Overview recovery are unbound. OS-3: remove dead Resume control; OS-5: capacity readback belongs to Signup setup. OS-4 observation: `BingoEvent.ConfigureSchedule` retains a dead capacity rule forbidding lowering after first public; reconcile the obsolete rule without reactivating it. Preserve Schedule's no-capacity/no-opening-toggle boundary; lifecycle/readback/navigation/page acceptance. |
| C5 | **BR-10 — Board, Review and Final Review integration**, including RL-1/BR-4/BR-12 | `06a-board-review-final.md` BR-10: bind existing calculators, leases, approved full-pool correction, review/current-state and finalization outcomes to the new references. **RC07** and BR-10 bind G1's structured blocking submission ID/upload time with a direct earlier-review link and queue/filter-preserving return. Retain history/authorization; execute RL-1 findings and structured error/readback/stale-route/page acceptance. |
| C6 | **TD-6 — Teams/Draft integration**, including RL-1/TD-8 and X-6 observation | `11-teams-draft.md` TD-6: bind the deferred Teams/Draft reference (`docs/references/admin-ui/README.md`, Teams/Draft binding notes), in-place draft/control/manual roster states and route recovery; retire/redirect old standalone editors without deleting history. TD-8 remains an observation unless these handlers are touched; then use the named concurrency checks below. X-6 remains an observation with mechanism undecided. |
| C7 | **WA-5 — WOM / Catalogue / Accounts / Audit integration** | `06b-wom-catalogue-accounts-audit.md` WA-5: AU15/AU20/AU23/AU24 and page bindings were missing; bind the settled contracts without another service/route family. Preserve provider guards, SuperAdmin checks, transient secrets and immutable audit. Catalogue follows the decided panel/activity fields below instead of the conflicting frozen reference. Scoped provider-free authorization/readback/error checks and individual page acceptance. |
| C8 | **LKP-1 — Luck mixed-outcome proof** | Dedicated Luck proof owner/assignment after page bindings, before FINAL-CANDIDATE; scope below. No reuse of the already-fixed LK-1 finding ID. |
| D1 | **FINAL-CANDIDATE — branch reconciliation** | Stable AU/reference/integration candidate, required checks/source review and exact identity. No deployment permission. |
| D2 | **R-3 — restored-backup rehearsal** | H4-2 procedure and coverage limits approved by the user on 4 October 2026 after Claude’s review, as recorded in the planner decisions; attribution corrected by the quoted Step 0 user assignment; harness not built/tested and R3 unexecuted. Separately authorize tooling/backup transfer/execution, then follow the runbook isolated procedure on the final candidate. Never use the production host wrapper as rehearsal. |
| D3 | **DEPLOY — explicit release action** | Requires passing final candidate/R-3 plus explicit production approval. No deployment authority in this ledger. |

#### Active AU lanes — Step 0 assignment, 4 October 2026

Authority: [the user's quoted message](docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md)
and [the full assignment](docs/references/admin-ui/reviews/2026-10-04/au-step0/assignment.md).
Step 0 is one documentation-only commit reported before implementation begins.

| Lane | Tickets / model | Checkout and exclusive implementation ownership |
| --- | --- | --- |
| B1 | AU15 → AU16 → AU22 → AU24; gpt-5.6-luna / max | New worktree `/Users/christopher/.codex/worktrees/au-b1-small-fixes/BingoWebpage`, branch `codex/au-b1-small-fixes`, from Step 0. WOM Fetch page (RefreshAsync unchanged), Audit, Accounts/ownership services/projections, their tests/translations. No migrations, scoring, Board, finalization, catalogue, draft, lifecycle or WOM sync/management changes. |
| B2 | AU11 → AU12 → narrowed AU18; gpt-6-astra / high | Existing participants-functionality worktree/branch from Step 0. Board/editor, tile/template EHB, estimates, contributions, standings, snapshots, Final Review and migrations; no B1-owned changes. |

Each lane has one direct implementer and one commit per ticket, then stops for
Claude's independent review. B1 merge-back is permitted only after Claude B1 PASS,
locally into the feature branch, followed by B1 checks on the merged result.
No push, main merge, B3–B5, RC/UI implementation or rehearsal work is started here.
Product questions pause the affected ticket and go to `/root`; independent assigned
work may continue. No planner polling or wait_threads; report before turn end.

Planner ownership detail: B1 owns edits to existing C11FinalizedRosterIntegrationTests
and EventQuarantineIntegrationTests required by AU16, plus translation files.
B2 adds isolated test classes for its new behavior; if it needs a B1-owned file,
report before editing. Lane-specific documentation/evidence is in `au-b1/` or
`au-b2/` under `docs/references/admin-ui/reviews/2026-10-04/`. Shared authority
changes are restricted to each lane's ticket sections; do not change the other
lane's rows or global CURRENT_STATUS. Planner reconciles global state. This keeps
parallel code/test ownership disjoint and bounds documentation merge resolution.

#### Approved AU scope additions and status corrections

| Ticket | Owned addition or acceptance boundary |
| --- | --- |
| AU13 | F8 at `6d33ce6` closes the editable post-finalization planning estimate. The lock removed by that change originated in `525d5d1` (BR-2/BR-9); keep the note in the AU13 evidence and do not reopen this ticket. |
| AU15 | WA-6 (`06b-wom-catalogue-accounts-audit.md`): RefreshAsync lacks structured rejection reasons. Keep the generic cooldown wording. Structured reasons belong to AU20; do not imply that removing the typed FETCH confirmation removes server cooldown/retry guards. |
| AU16 | WA-4/WA-7 (`06b-wom-catalogue-accounts-audit.md`): Audit wrongly hides hidden-event history, uses substring/instant filters, and two tests lock those violations in. The two tests must be inverted when AU16 runs; this is a named acceptance check, not a claim that it has run. |
| AU17a | `06a-board-review-final.md` BR-9/BR-10 and D5: correction still filters to current Playing accounts/current members. D11 option b narrows the pool to current/released Playing assignments of current/former members of the submission’s own team; Informational accounts are excluded. Derive unambiguous participant identity and preserve it through readiness/results. Pending-only metadata correction still requires reason, preserves image/upload time, evidence locks and immutable approvals. Prove valid former assignments and invalid attribution without partial writes. B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending in `ebd0ef2`; independent Claude recheck and RC07 binding pending. |
| AU17 | Approved, narrowed by Step 0 Decision 1: only the design’s Contribution line before approving (Approving adds 1 / Worth 3, but only 1 remains / Completes the objective / Nothing left to add), plus safe readback after an uncertain save. No per-account prior-approved count; nothing outside `Review.dc.html`. S9 adds the credited participant’s team-leave timestamp for RC07’s decided warning; active-account-since/switched-from content is dropped. G2 closed paused-window wording; full-pool correction stays AU17a. |
| AU20 | WA-2/WA-6/WA-9 (`06b-wom-catalogue-accounts-audit.md`): actual-time matching would reject final-review fetches, current outcomes lack structured reasons, and linking can race a pending Create. The full approved requirements and focused checks are in **AU20 — configured-window and end synchronization** below; those requirements govern this ticket. |
| AU21 | WA-3/WA-7 (`06b-wom-catalogue-accounts-audit.md`): drop edits silently rename/re-image shared items across activities. Make shared-item **rename**, adoption, image and metadata scope explicit; preserve independent item identity/version and dependency-safe deletion. |
| AU22 | WA-1/WA-7 (`06b-wom-catalogue-accounts-audit.md`): reset consume-time authorization was missing; F4 completed consume-time re-authorization of both recipient and issuer under the existing lock; remaining work is projection/readback binding, not a new reset policy. |
| AU23 | 4 October decision: rate text including `N x` is ordinary Admin input on add/edit/reactivation. Remove the “Changes to reward rolls require an operator” refusal. `3/1024` means one roll at 3/1024; `3 x 1/1024` means three rolls at 1/1024. New drops use `default` roll group. Real PostgreSQL authorization checks must allow ordinary rate-text roll changes and reject non-default advanced input; only SuperAdmin edits advanced roll groups. Final-chance/retired-parent and activity-context rules below supersede earlier advanced-field proposals. |
| AU24 | WA-5 (`06b-wom-catalogue-accounts-audit.md`): approved typed ownership confirmation is not implemented. Require typed destination public-username confirmation and do not reuse the legacy `TransferOwnershipAsync(actorId, password, destinationUsername)` overload. |

#### Review leftovers and proof-gap ownership

| Ticket | One-line scope and acceptance |
| --- | --- |
| **RL-1 — routed review leftovers** | Coordination ledger, not an extra standalone implementation. Each subfinding runs at the named C slot below; DB-2/DB-3 belong only to DB-1. No blanket transaction design or new product rule is authorized. |
| RL-1 / P-7 → P-1 (C1) | `01-participants.md` P-7 + D3: Restore makes a live WOM lookup and may permanently block a renamed/unknown withdrawn account. Use stored account data, like Admin Add; no WOM request. Prove retained reservation/eligibility/capacity and no-write failures with PostgreSQL/provider-free fixtures. |
| RL-1 / EI-3 → EI-2 (C3) | `04-events-identity.md` EI-3: the uncertain catch path rolls back then re-reads DB, so a commit-time connection loss may throw again and produce 500. Render the posted draft/uncertain page **without a database read**, as Create does; tolerate failed rollback and add an injected lost-commit HTTP test. Never claim the save succeeded. |
| RL-1 / BR-4 → BR-10 (C5) | `06a-board-review-final.md` BR-4: direct reject/reverse POSTs with a reason bypass client-only confirmation. Enforce the existing reason/confirmation requirement at the server and prove missing-confirmation no-write behavior. |
| RL-1 / BR-12 → BR-10 (C5) | `06a-board-review-final.md` BR-12: omitted Review/reopen versions skip optimistic checks. Fail closed on missing/zero baseline and prove unchanged state/audit plus ordinary valid/stale behavior. |
| RL-1 / TD-8 observation → TD-6 (C6) | `11-teams-draft.md` TD-8: Scramble's no-pick check can race Pick; RemoveDraftTeam can race AddMember, leaving membership on an inactive team. G3a did not close these races. If binding touches those handlers, resolve within the existing Serializable boundary and prove both interleavings; otherwise retain as observation, not silently close it. |
| RL-1 / X-6 observation → TD-6 (C6) | `07-cross-cutting.md` X-6: the post-roster WOM-outcome audit is separate, its failure is swallowed, no-op outcomes write rows, and raw provider exception text can enter audit. The roster transaction itself is already atomic. Mechanism remains undecided; do not move a later provider outcome into an already-committed transaction or imply an approved fix. |
| **P-3 — Participants proof gap** → P-1 (C1) | `01-participants.md` P-3: F04's Add-directly-to-Confirmed-when-full claim lacks executed proof. Exercise ordinary full outcome and explicit selected-person `+1`, exact cap, no other waiting promotion, repeat/no-write behavior and PostgreSQL persistence. |
| **DB-6 — Dashboard proof gap** → DB-1 (C2) | `02-dashboard.md` DB-6: no executed assertions select the latest ended recap, its Provisional flags or latest additions; pending/rejected submissions are absent. Exercise all on real persisted projections and distinguish source inference from executed proof. |
| **LKP-1 — Luck mixed-outcome proof** (C8) | `10-fix-batch-review.md` F2 test-gap note: current test makes every outcome unsupported; one unsupported outcome among supported outcomes is only source-traced. Dedicated Luck proof assignment exercises mixed outcomes, replaceable incomplete checkpoint, retained compatible complete snapshot/freshness, explicit unavailable states and no read-time WOM/rescore. This is a new proof ID; original LK-1 is already fixed by F2. |

#### AU20 — configured-window and end synchronization (remediated; worker-reported focused checks; external recheck pending)

Source: `06b-wom-catalogue-accounts-audit.md` WA-2, WA-6, WA-9 and
`decisions.md` “WA-2 (decided, extends AU20)” in the 4 October remediation evidence.
The original actual-window tolerance would make exact final-review matching fail;
the following is the approved replacement, not a request for a future rewrite.

1. Compare WOM start/end exactly with the website's **configured** UTC start/end
   at every stage, including Final Review. Actual instants still drive upload
   cutoff, drop eligibility and the review window; never substitute them for this check.
2. Early end and Resume always succeed locally within ordinary lifecycle rules;
   WOM availability or a provider rejection never blocks the local action.
3. Early end sets WOM end to the click instant rounded **up** to the next whole
   minute (21:59:55 → 22:00:00), and persists the same configured end locally.
   Actual end retains the precise click instant; no lead time is required.
   Resume requires the Admin's validated replacement future end, using existing
   schedule increments; store/use that end, never the resume-click time, with no
   click-time rounding. The three former tolerance sites are shared linking/
   replacement/fetch validation, Schedule save, and **Resume**.
4. Retry temporary update failures with spaced backoff, never a fast loop. WOM
   accepts edits ending in the past provided end is not before start; retries may
   continue until official results publication. HTTP 400
   `COMPETITION_START_DATE_AFTER_END_DATE` is a non-transient rejection.
5. While WOM end is unmatched, run no fetch after actual end: that could count
   post-end gains. After successful end update, normal fetches resume with the
   correct window. If still unmatched at publication (prolonged outage, absent/
   invalid verification code or non-transient rejection), mark the event
   **“WOM end could not be updated”**, skip the final fetch and use the last fetch
   before actual end as official WOM data. Luck freshness shows that fetch's age;
   no invented fresh values or zeros. ID-only external links without a verification
   code always take this fallback after early end.
6. Preserve external deletion prohibition and existing cooldown, scheduled slot,
   lease, operation and history guards. Provide structured eligibility/reason/
   next-permitted-time/credential/operation outcomes; AU15 retains generic cooldown
   wording. External replacement before/during Live and disconnection before first
   Live follow approved option 1 regardless of stored/rejected code, exact configured
   window and active/unresolved guards; never reuse the old code or delete externally.
   WA-9: external linking also waits for Sending/Unknown website Create, so its late
   completion cannot overwrite a newly saved link. Readback never performs a fetch
   and old success does not prove a new operation.
7. Accepted limit: WOM end values use each player's last snapshot inside the window;
   precision depends on player updates. Users know to log out immediately before
   the end. This limit is accepted, not a new compensation or attribution algorithm.

Focused acceptance: deterministic sub-minute early end, minute-boundary end,
validated future Resume, exact/timezone-equivalent configured windows through
Final Review, non-microsecond input through real PostgreSQL precision, each old
five-minute tolerance site, transient retry spacing and non-transient rejection,
unmatched-end fetch suppression/success/fallback/publication, ID-only credentials,
external delete refusal, Sending/Unknown Create-link race and late responses after
replacement. Use controlled provider doubles, never live WOM. The early-end texts
in DATA_MODEL, TECHNICAL_ARCHITECTURE and PRODUCT_REQUIREMENTS now describe the
implemented behavior. Six scoped B3 checkpoints and their execution evidence are
under `docs/references/admin-ui/reviews/2026-10-04/au-b3/`; external Claude review
remains pending. No current-page display, RC/UI binding or release acceptance is
implied by implementation checks.

AU20 error classification is a **planner technical decision**, corrected by
[remediation brief25](docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/supplied-brief.md)
and superseding the initial item3 resolution: 5xx, timeouts, network failures,
408 and 429 are temporary. Named 400, missing/invalid code, 401/403/404 and other
validation rejections are permanent. For an end-only unknown write, a read of the
unchanged old window proves not applied and permits spaced resend; the target
window confirms success. Other unknown operations keep their existing protections.

**User decision D6 (option a):** “publication stops the WOM end update for good,
including after a reopen.” Reopened/republished results keep the same WOM basis;
no retry restarts. Source: [08-decisions.md, B3 review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md),
with the verbatim section in [supplied decisions](docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/supplied-decisions.md).


#### Catalogue decisions — AU23, CAT-1 and WA-5 (4 October; B4 backend delivered, WA-5 binding pending)

Source: 4 October user decisions in the durable remediation `decisions.md`,
superseding earlier proposals in `06b-wom-catalogue-accounts-audit.md` WA-5 and
Catalogue C7 / AU23 mapping.

- **AU23 / final chance:** always enter the final chance for the specific item in
  one's own name at the agreed team size; conditional raid-table chances must be
  resolved before entry. Retire “Only after” from new input, editor and panel;
  keep `conditional_on_parent`/`parent_probability` columns, existing records and
  immutable snapshots/history. **No EHB parent-chance fix ticket.** The user queried
  production on 4 October and reported zero conditional rows; this is operator
  evidence, not our verification. Existing runtime differences for legacy conditional
  records are not rewritten by this ticket; stop if the pre-change check finds any.
  This pre-check is user-approved by the quoted Step 0 assignment; see [Step 0 approval provenance](docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md).
- Rate text, including `N x`, is ordinary Admin add/edit/reactivation input. B4
  removed the current refusal at `Catalogue/Index.cshtml.cs` while retaining
  validation/concurrency/audit. `default` is the group
  for every new drop, including on newly created activities. Existing production
  groups (`barrows-equipment`, `purple table`, `doom-1-16-aggregate`,
  `fortis-full-run-unique`; 56 active rows reported by the user) remain unchanged.
  Roll groups are calculation inputs, SuperAdmin-only. The legacy probability-scope/
  assumed-participant columns remain historical context after CAT-1 moves the
  informational value to the activity; changed per-drop context input is refused
  rather than rewritten. Parent fields are retired from new input, not opened to
  either role. No separate roll-count input.
- **CAT-1 — move informational agreed team size to activity (approved, A1 after
  AU23; before WA-5):** one integer >=1, default 1, for the boss/activity, beside
  efficient kills per hour. The rate and EHB use the same agreed efficient strategy;
  this field is explanatory and never enters a calculation. Every Admin can edit it.
  Move the context from per-drop `assumed_participants`/`probability_scope` without
  changing existing approved/published snapshots or history. The user reported zero
  non-default per-drop contexts on 4 October; re-query immediately before migration
  for set values and conflicts within each activity. A new set/conflict requires a
  user decision, never silent loss/coalescing. Required migration/designer/snapshot
  travel together. Verify default, validation, Admin edit, no calculation effect,
  retained groups/history and rollback shape with controlled PostgreSQL fixtures.
- **WA-5 binding:** the “How the rate is counted” header displays the actual group
  (`default`, `purple table`, etc.). Rows are chance per kill (including `N x`),
  source, and note only if present. Bottom: “Roll group can only be changed by the
  Super Admin.” SuperAdmin edits group in that panel; ordinary Admin sees it read-only.
  Remove chance-per-roll, rolls-per-kill, whose-chance and Only-after rows, the
  operator sentence, and the rate-roll refusal in frozen `Catalogue.dc.html:378`
  and `:801` when binding. Put editable informational **Team size** next to **Kills
  per hour** in Settings and Add activity, with the same fields/validation. This
  deliberately supersedes those portions of the frozen reference. Do not edit that
  artifact in this cleanup; RC10/WA-5 later implement the decision.

Recorded observations have no separate ticket: X-5 is recorded here for the
integration owners (PageModel mutations rely on the per-request cookie recheck and
must not be treated as persisted authorization), while BR-11's 3 October reported production
zero must be re-run with the corrected query; it is not release-pass evidence. These observations do not start
implementation or widen any ticket.

### AU01 — Restore exclusivity

Source finding: Overview B1, `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/overview-status-extract.md`.
Starting owners: `EventQuarantineService`, `BingoEvent.Restore`, shared current-event
boundary used by event Start/Resume. Hiding current A, starting B, then restoring A
currently permits two visible current events.

Target: when restoring would conflict with another visible current event, reject
without changing hidden metadata; do not implicitly hide/end B. Reuse the existing
shared boundary/lock and definition of current event. Preserve SuperAdmin authority,
auditing, history and restoration of non-conflicting archived events. The user’s authorization to
execute the recorded ticket adopts this rejection outcome; escalate only a concrete
conflict with protected product behavior discovered during implementation.

Proof: real PostgreSQL sequential reproduction and concurrent Restore versus
Start/Resume, one successful non-conflicting restore, rejected state unchanged,
and actual authorized transport recovery where affected. No global lifecycle audit.

### AU02 — Signup-code server validation

Source finding: Signup setup B1,
`docs/references/admin-ui/reviews/2026-10-02/earlier-pages/signup-setup/review.md`.
Starting owner: `Participants.OnPostSignupCodeAsync`; its StringLength(100)
annotation is not enforced by the handler before hashing/persistence.

Reject an overlong submitted code before mutation and return usable validation.
Preserve enabled+blank retaining the existing hash, enable-without-hash requiring
input, disable clearing the hash, authorization, phase restrictions and version
checks. Do not reject unrelated form fields by blindly applying whole-page
ModelState, expose the stored code or change hashing policy.

Proof: actual authenticated POST boundary with 100/101-character input and the
retain/clear cases; rejected input leaves hash/version/audit unchanged. Use
controlled fixtures, never real codes in evidence.

### AU03 — Event creation retries

Starting owners: Admin Events `Create.cshtml.cs`, existing event creation service,
Events E03 reference. Preserve name/timezone validation, private draft creation,
permissions and existing atomic operation.

Give one logical create request a durable authorized identity. Concurrent/repeated
retries must create at most one event and return the same result. Support Check
again after an uncertain response without guessing by name; same-name events
remain legal. Define handling of the same key with different input and ensure
another actor cannot obtain unauthorized outcome data. Choose the smallest
persistence mechanism with a concrete need; include full migration artifacts if
required. No generic operation-receipt framework.

Proof: PostgreSQL duplicate/concurrent retries, lost-response readback, different
payload/key and unauthorized lookup, actual request validation. UI stays deferred.

### AU04 — Events directory data

Starting owners: Admin Events `Index.cshtml.cs`, `SharedShellService`, completed
Dashboard mappings. Implement approved directory read semantics: Live first,
preparation by date with unscheduled last, past newest across phases; stable ties;
retained participation for Live/past, nullable capacity and honest imported coverage.
Attention displays failure priority followed by review work, plus other issue
categories; ordinary setup is not an error/attention count. Preserve the shared
inbox's existing units and Hidden/SuperAdmin boundary. The mock WOM failure is
illustrative, not authorization for a new failure subsystem.

Expose authoritative filters/sorts and query results needed for the reference;
reuse existing reads/mappings rather than recalculate Dashboard independently.
Proof: focused query/authorization tests for phases, null/ties, historical people,
attention categories and hidden access. UI URL/navigation/table changes remain
in the page integration pass.

### AU05 — Signup setup versions and stale edits

Implementation contract: non-destructive form mutations require `expectedFormVersion` under the event lock; existing forms forward it. Settings results separately retain submitted event baseline and authoritative current values/version (capacity, waiting enabled, code-required/present only). Opt-in JSON on existing capacity/code handlers preserves ordinary redirects; pending client state binding stays deferred. No new persistence or generic concurrency abstraction.

Starting owners: `Questions.cshtml.cs`, signup mutation services/contracts,
capacity/code handlers. Add explicit client-baseline checks to question/account
add, custom edit, account rename, move and co-captain enable where currently absent.
Reuse existing delete/disable question-version and impact-count checks.

Settings saves return authoritative values/new event version while retaining
separate capacity/code transactions. An unrelated card refresh must not erase the
immutable baseline of another uncertain operation; support the required result
contract, with actual client state binding deferred. Preserve FirstResponseAt locks,
phase/role guards, targeted assignment release and queue promotion. Do not introduce
whole-page atomic saving or rebuild already loaded answer/impact counts.

Proof: stale sequential clients and controlled concurrent edits at real PostgreSQL
boundaries; correct result versions, no rejected side effects, existing delete/
disable impacts retained. Frontend cross-card uncertain recovery belongs to binding.

### AU06 — Question/account-field add retries

Starting owners: question/account add handlers/services. Depend on AU05's contract;
reuse AU03's approach only if it fits without introducing an unnecessary framework.
Return created question/field ID and reconcile retries by request identity, not
label/type matching. Preserve drafts while a result remains ambiguous. Same labels
from another admin must not stand in for this request. Retain protected primary/
captain fields, first-response restrictions and separate system/account types.

Proof: real PostgreSQL duplicate/concurrent/lost-response adds, same-label unrelated
creation, actor ownership and changed-input retry handling. No new profile accounts:
this ticket creates event form fields, not players' globally saved accounts.

AU06 implementation contract (2 October 2026): one globally unique durable add ID
is bound to actor/event, canonical requested values and original form baseline;
changed-input/baseline or cross-owner reuse fails without revealing a prior result.
Current authorization/visibility precedes replay; own committed replay bypasses
new-write stale/lifecycle rejection, returns its exact field ID, and never repeats
an audit/version change. Deleted/inactive outcomes are explicit and never recreated.
Complexity: one operation entity/table/migration, one method on existing signup
service, minimal hidden-field/JSON transport; no new service/page/policy/job or
framework. Preserve Serializable event locking, existing normalization/protected
fields and ordinary routes. Include reset/history compatibility. Frontend draft and
uncertainty integration, AU07 feedback and manual UI acceptance remain deferred.

### AU07 — First-response normalization feedback

Current add silently makes a requested required question optional after the first
response. Preserve that existing rule and no backfill, but return an explicit
outcome plus the authoritative optional definition when normalization occurs.
The reference already explains it; a rejection alternative would need a deliberate
contract choice, not an incidental change during implementation.

Proof: a first response arrives between rendering/editing and add; only an optional
question persists, the result explains it, retries retain the same definition.
Also verify ordinary pre-response required creation remains supported.

AU07 response boundary: the ordinary null-to-first-accepted `FirstResponseAt`
transition alone is response metadata and preserves the editable form version.
Any simultaneous definition/settings mutation or explicit Version mark/advance
still advances it. No stale-baseline bypass is permitted. The immutable first
marker, required/type restrictions and all current guards are rechecked under the
existing Serializable event lock. A required custom add after that boundary succeeds
as optional with an explicit `CompletedAsOptional` outcome, explanation and original
committed definition; there is no rejection or backfill. Exact replay/readback uses
AU06 identity plus its uniquely linked immutable creation audit and verified original
intent, preserving the normalization and definition after later edits. Missing or
corrupt creation audit fails closed without returning a guessed definition or writing.
Complexity: reuse AU06 operation identity and retained creation audit; no added
persistence/service/route. Minimum existing JSON/form feedback only. Full frontend
uncertainty binding and manual acceptance remain deferred.

### AU08 — Identity conflicts

Starting owners: existing Identity transport/service and timezone-review contract.
Provide safe field-level comparison/merge for stale clients: preserve intended
edits, retain another admin's untouched-field updates, and require resolution for
conflicting edits. Keep schedule-only timezone conflicts separately stale and
require a fresh review of their consequences. Do not change permanent slug ownership,
50-code-point name limit, UTF-16 description/buy-in limits or existing phase locks.

AU08 implementation contract: compare each canonical Name, Description,
BuyInDescription and Timezone against the submitted original baseline and current
persisted value. Untouched fields retain current values; intended changes apply
when current still equals baseline or already equals intent. Different same-field
changes block the entire save until explicitly resolved. Keep mine / Use current
choices carry reviewed current values; a newer differing same-field value requires
resolution again. Preserve original baselines and drafts on failure rather than
silently advancing them. Current-version legacy requests may retain their existing
path; stale requests without a complete baseline fail closed and require reload.
The rendered form carries baseline and reviewed-value transport; full conflict-choice
UI integration remains deferred.

Timezone confirmation separately binds original/proposed zones and all current
participant-facing timeline consequences at PostgreSQL microsecond precision.
Changed schedule consequences return separately identified stale feedback and a
fresh preview; confirmation is required again before any Identity field saves.
Recompare inside the existing Serializable transaction; preserve atomic audit,
phase/visibility/authorization guards and provider-conflict recovery. Reuse the
existing event and Identity page owners with a small domain comparison value/result;
no new service, table, route, policy, job, migration or receipt. AU09 is excluded.
The existing generic route filter incorrectly used the pre-Live combined capability
for Identity. Map only Identity to its existing ConfigureIdentity capability, so the
approved Live/Final Review text-edit behavior is reachable through real HTTP while
the existing permanent first-Live timezone lock remains authoritative.
Valid Unicode/UTF-16-limit values must fit the existing atomic audit: retain truthful
truncated text excerpts budgeted against serialized JSON storage, without schema
changes. Existing timezone confirmation transport must retain the returned failed
editor/drafts even when another Admin's change removes the need for a timezone
preview. This adds no conflict-choice UI, readback or uncertainty workflow.

Proof: disjoint and same-field concurrent edits, schedule-only conflict, stale
resolution retry and unchanged values against PostgreSQL/actual transport as
appropriate, including non-microsecond-aligned input crossing persistence before
review. UI Use theirs interactions remain for integration.

### AU09 — Identity readback

Use existing authorized current-state reads to support Check again after an
uncertain save. Compare the relevant full intended values and report whether the
event now has them, without claiming this request saved them. No new Identity
request receipts are required. Preserve editing drafts and truthful ambiguity on
read failure or different values; do not blindly resubmit the mutation.

Proof: applied/lost-response, unapplied, another-admin matching/different update,
authorization and read failure. UI wording is Up to date, not proof of Saved.

AU09 dispatch contract: freeze the complete canonical Name, Description,
BuyInDescription and Timezone expected tuple separately from the original edit
intent/baseline. Use current takes exactly the explicitly reviewed value; untouched
fields take the latest values actually observed and used at dispatch. Never derive
expected values from a later read. Check again uses the existing authorized Identity
read boundary and compares all four full values. A match means only Up to date now,
never that this request saved them. An unseen disjoint server merge or later edit
may therefore remain Different/uncertain even after an applied save. Different or
failed reads preserve the frozen tuple and editing draft without retry, rebase,
overwrite or discard; failed reads are Unknown. No receipt or reconstruction of
the server's effective merged tuple is introduced. Backend/transport integration
is sufficient for AU09; full new-reference UI binding and manual acceptance remain
deferred. Existing timezone confirmation transport switches an uncertain mutation
to read-only checks; it must never blindly send that mutation again.

### AU10 — Schedule preservation and integration gaps, source-reviewed

Sol 6.1/high source comparison completed with changes required; evidence:
`docs/references/admin-ui/reviews/2026-10-02/earlier-pages/schedule/review.md`. No execution or production
fix is claimed. Preserve existing atomic serializable/versioned authorized save,
audit, timezone/DST/five-minute rules, overlap/WOM checks and lifecycle exceptions.

- Preserve each unchanged field's original UTC instant. Schedule.cshtml.cs currently
  reparses editable fields through minute-only text, losing seconds/subseconds and
  rejecting untouched valid instants displayed in a repeated DST hour. A fix must
  preserve precision without accepting changed ambiguous/nonexistent local times.
- Parsing already supplies field ModelState errors; lifecycle validation returns
  one Error string. Add suitable field mappings there, retaining overlap as a form
  error, rather than replacing all validation.
- Reuse authorized reads for uncertain-save reconciliation against an immutable
  submitted full schedule: exact instants, automatic-opening state, version and
  current phase/editability. Preserve unknown outcomes after read failure; matching
  state means Up to date, not proof this request saved it. No receipt system needed.

Focused eventual proof: unrelated edit preserving exact seconds/microseconds and
untouched ambiguous-history values; changed DST errors; applied/lost-response,
unapplied, competing matching/different precise instants, authorization/read failure,
stale schedule, per-field mapping and PostgreSQL precision. Use deterministic
microsecond-aligned persisted expectations plus nonaligned input round trips.
Picker binding, stay-on-Schedule, confirmation table and navigation remain UI work.
The app already has one confirmation, not a confirmation ladder to remove.
Reconcile stale FUNCTIONAL_CONTRACTS 4.4 with approved pre-Live start/end editing
through draft and overdue repairs. Existing scheduler behaviour remains unchanged.
600ms Saving is a proposed reference setting, not an exact approved constant.

### AU11 — Tile-local EHB override for every objective type

Approved by the user during Board planning on 1 October. Allow an optional manual
TOTAL tile EHB override for catalogue/drop tiles as well as the existing entered
estimate for manual/non-drop challenges. No override uses normal calculated EHB;
manual challenges still require their entered estimate. Keep the calculated
baseline available for comparison and resetting a calculated tile. This setting
belongs to the event tile; do not change catalogue rates or other events.

The effective value feeds existing board/line estimates, credited proportional
partial progress, player contribution statistics and the EHB ranking input. A
separate points model is deferred. Preserve probability/KC/Luck mechanics; changing
an effort estimate must not modify actual drop rates or observed activity. Trace
and validate existing allocation code before choosing the smallest override path.

Preserve authorization, editing lease/version, validation and immutable approval
snapshots. Competitive edits invalidate unpublished approval as today. Any submitted
evidence continues to lock affected tile scoring, regardless of evidence status;
no override can rewrite published evidence, archived values or official results.
Starting owners: Board page model/editor data, TileTemplate/BoardTile, EhbCalculator,
BoardEstimateService, approval snapshots and proportional contribution allocation.
The accepted exception is recorded in PRODUCT_REQUIREMENTS sections 10–11,
FUNCTIONAL_CONTRACTS 6.2 and DATA_MODEL; implementation is still pending. Do not revive reusable template workflows or catalogue overrides.

Acceptance/proof: automatic fallback, set/change/reset on calculated tiles, required
manual estimate, invalid values, multiple objectives and weighted/partial allocation
with no double-counting, line/board/player totals, approval round trip, stale editor,
submitted-evidence rejection and isolated event/catalogue/history preservation.
Use executable boundary checks and PostgreSQL where persistence/concurrency matters.
AU11 implemented in B2 using the existing tile-local persisted field; evidence:
`docs/references/admin-ui/reviews/2026-10-04/au-b2/au11.md`.
New editor controls and manual visual acceptance remain deferred; Claude independent review pending.

AU11 remediation D1 adds `20261004093705_ClearLegacyDropTileEhbOverrides`:
re-run and record the DropRequirements/non-null override count immediately before
deployment. Up clears all matching template values and reports the count; Down is
non-restorable. Frozen scoring snapshots are untouched. See the production runbook
and `au-b2/remediation/03-a2.md` for controlled populated-database evidence.

### AU12 — EHB before current-score completion time

Approved placement direction: full-board finishers still lead and earlier full-
board completion wins; otherwise compare completed lines (rows plus columns),
completed tiles, highest credited EHB, then earliest current-score completion time.
Preserve credited partial progress, exact shared ranks and existing rank numbering.
User decision D3, 4 October (forwarded Claude-chat decisions): for the new rule only,
compare credited EHB rounded to four decimals in both ordering and shared-rank
equality. Legacy EHB comparison retains full precision.
No separate points, discretionary tie-break, altered evidence timestamps or name/ID
ordering used to split a genuine tie. Use one consistent ranking rule across public
provisional standings and finalization; retain existing immutable official results.

Starting owners: PublicProgressCalculator Rank/SameRank and its actual consumers,
finalization/official snapshots, public standings/copy and existing rule authorities.
User decision, 2 October: apply only to new events. Existing events retain the
prior rule even without official results. Implement an explicit creation/rule
boundary; do not infer it from today's date or retroactively re-rank existing events.
Resolve the minimal persistence mechanism at readiness. No separate points infrastructure.

Acceptance/proof: higher EHB beats earlier score time after equal lines/tiles;
board completion, full-board finish time, lines and tiles retain precedence; equal
EHB falls back to score time; exact ties/null times deterministic and truthful;
partial progress still contributes; provisional/final placement parity; earlier
immutable official versions and historical events unchanged. Preserve PostgreSQL
microsecond timestamp precision at persisted ranking boundaries.
B2 implementation uses immutable `events.placement_rule`, legacy 0 / new 1, per
planner technical resolution on 4 October. Existing rows backfill to 0; ordinary
creation explicitly selects 1 and historical imports retain 0. Evidence:
`docs/references/admin-ui/reviews/2026-10-04/au-b2/au12.md`. Claude review pending.

### AU13 — Retain editable planning team-size estimate

Approved: keep expected players per team manually adjustable after draft
finalization. Remove the per-actual-team EHB-per-player breakdown from the new UI;
retain the overall planning statistics and the manually selected estimate rather
than replacing it with finalized roster sizes. No additional explanatory label or
warning was requested. Existing sensible field labels may remain.

Starting owners: Board OnPostTeamSize, Board statistics projections and current
expected-size storage. Remove only the draft-finalized restriction on this planning
setting where applicable; preserve event/board authorization, terminal/history,
editing and concurrency boundaries. Do not broaden all Board editing phases.
Changing this estimate affects planning per-player/per-day values only, never team
membership, draft distribution, competitive tile EHB, credited stats or ranking.
Avoid adding a parallel estimate store if existing storage serves this purpose.

Acceptance/proof: set/update before and after draft finalization; persistence and
reloaded projections use the selected value; existing bounds/permission/version
checks remain; finalized rosters, published competitive snapshots and scoring are
unchanged. Include unequal actual team sizes to prove no silent actual-roster
substitution. New UI binding and manual acceptance deferred; no code started.

### AU14 — Teams uncertain-action reconciliation, recorded 2 October 2026

The completed-reference comparison confirms existing in-place Pick/Undo/Scramble,
control renewal, participant/search data, distribution and readiness services.
Do not rebuild them. The remaining transport gap is safe authoritative readback
for timed-out actions. At handoff choose the smallest contract retaining immutable
pick/team identities and intended fields; current state must not be claimed as
proof a particular request succeeded or as permission for blind retry. No generic
receipt framework is authorized by this ticket. Preserve authorization, current
controller, latest-pick rules, transaction/version checks and membership history.

Focused proof must distinguish a reused pick number after Undo/re-pick, a competing
admin picking to another team, a redraw yielding the same order, inclusion-only
team edits and readback failures. Bind existing roster synchronization statuses;
queued or failed WOM work must not be described as completed. Reconcile the
HasUsableCaptain projection with existing command eligibility;
no new lifecycle permission or algorithm is approved. B5 backend is implemented
in `d3f2d88`: current-state readback retains full intended fields, immutable pick/team/
membership identities and versions, plus existing roster synchronization outcomes.
Failed reads remain unknown; a matching state never identifies a request or allows
blind retry. AU14 is backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC04/DRF).
Remediation adds roster PublishedAt and WOM operation CreatedAt and hardens
failed reads; see `docs/references/admin-ui/reviews/2026-10-04/au-b5-remediation/`.
Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b5/au14.md`.
Evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/teams-identity/teams-review.md`.

### AU15 — WOM manual fetch without typed confirmation, 2 October 2026

User approved replacing the typed FETCH challenge with a normal Fetch now action.
Remove the challenge/confirmation wrapper in WiseOldMan.cshtml and the exact-word
check in OnPostFetchCompetitionAsync; reuse RefreshAsync unchanged. Preserve Admin
authorization, antiforgery, existing phase eligibility, one-hour last-success guard,
normal schedule/retry due times and in-flight lease protection. Failure does not
entitle the user to bypass those guards. No force refresh, manual update-all,
provider-policy change or extra automatic fetch on page load. Reflect pending and
cooldown outcomes honestly. Reference uses the normal action; binding any disabled
state/next-available timing must reflect all authoritative guards, not only an hour
from the last attempt. Do not broaden phase access in this correction.

Focused proof: permitted authenticated POST without FETCH reaches the existing
refresh service; cooldown/retry/in-flight rejection issues no provider request;
unauthorized/antiforgery and phase restrictions remain intact. Use controlled
provider fixtures, never live WOM or user databases. The B1 implementation is
recorded in `docs/references/admin-ui/reviews/2026-10-04/au-b1/AU15.md`; its
browser contract and Release build pass. Both the current and Step0 baseline
PostgreSQL/HTTP fixture runs reach the route and reproduce the unchanged schedule
assertion failure, so that gate remains unverified for this environment. One
independent review of the stable change remains required; new-reference visual
acceptance remains separate. The ticket is implemented and awaits Claude review.

### Deferred UI binding and reference ownership

The following remain implementation work when the new UI is connected, not more
backend rewrites: Participants/Dashboard binding; Events modal/filter/history;
Identity stay-on-page/conflict/readback; Overview composition/readiness/control
and evidence-code recovery binding; Signup setup route/tabs combining existing
separate handlers and retiring old capacity ownership. Reuse per-row answer/impact
counts and rename the multiline Text label without adding a new field type.

Teams / Draft has no newly agreed backend feature in this design pass. Track as
UI integration: remove the standalone participant table and affiliation/image
controls without deleting retained data; searchable roster assignment over existing
participants, click-to-pick through the existing endpoint, compact active workspace
and automatic compact sidebar. Preserve snake/Undo, admin control ownership,
manual rosters and finalized corrections. No captain-operated shared draft, new
Pause/Resume, CSV or new draft mechanics. Completion source comparison can raise
concrete transport/data gaps; do not invent tickets speculatively.

The shared saving/spinning state should retain a brief minimum display duration
so fast requests do not flash. Use the approved prototype timing as a baseline,
possibly slightly shorter during integration; no exact duration is locked. Actual
success still requires confirmed completion, failures are truthful, and reduced
motion is respected. Share the timing rule; do not copy mock request delays into
backend work or change unrelated exit-animation timing.

The user authorized Codex to make bounded reference behaviour/wording corrections
on 1 October, reserving Claude usage for visual design and subsequent artifact
synchronization. RC01/RC02 below supersede the earlier Claude correction prompts;
do not send those stale prompts for duplicate implementation. Their source reviews
are not application implementation or executed browser proof. Identity's named
reference corrections have SOURCE PASS; application binding remains deferred. Track future page findings
here under stable new AU identifiers,
separating existing capability, approved backend gap, proposed product decision
and UI binding. Toast Undo and general manual queue reordering remain deferred/
unapproved; do not add them to implementation just because prototypes contain them.



### Reference corrections — Codex ownership, approved 1 October 2026

These are separate from AU application tickets and the active Luck assignment.
The named scopes were approved, but execution is stopped after AU10. No worker
is dispatched by this planning update; a new user resume is required.
Use the current routing policy in `AGENTS.md`. Do not silently change models or
create another visible chat. Reuse established source evidence and keep remediation
and named rechecks within that ticket’s same worker pair. No broad extraction/review pass.

Reference edits now belong to `docs/references/admin-ui/` in the assigned
`participants-functionality` checkout/branch, alongside the application baseline.
The synced freeze is committed at `be0014e`; Documents is a retained source copy,
not the execution checkout. Reference mocks must not become production services.

| ID | Scope | Implementation / checks | Independent review | Canvas sync |
| --- | --- | --- | --- | --- |
| RC01 | Overview R1–R4, cancelled-Stats README fact, AU20 Resume/early-end contract | Queued; not started | Named recheck pending | Claude, after verified correction |
| RC02 | Signup setup R1–R4 and matching README statements | Queued; not started | Named recheck pending | Claude, after verified correction |
| RC03 | Schedule R1–R4, direct Overview picker consumers and README | Queued; not started | Named recheck pending | Claude, after verified correction |
| RC04 | Teams uncertain-action/team-save recovery and truthful WOM outcomes | Queued; not started | Named recheck pending | Claude, after verified correction |

**RC01:** correct public-link destination copy against actual Signups/Teams handlers;
make evidence-code failure/stale/uncertain mocks truthful and reconcile before
retry; suppress Restore after failed hidden-event reads; separate manual from
scheduled opening eligibility. Correct cancelled-event Stats documentation without
adding a new link. Preserve current lifecycle rules and approved At-a-glance spacing.
Source evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/overview-status-extract.md`.

**RC01 AU20 addition (brief25 item6):** correct the Overview Resume dialog to always
require the validated future replacement end, even if the retained end has not
passed. Correct early-end reference behavior to preserve precise actual end and
store configured end rounded up to a minute. Carry the required register row into
OS-1's inventory; choose final copy before binding. Reference editing remains queued.

**RC02:** compare the complete intended question definition including ordered
choices; never resolve an uncertain add by another admin's same-label question;
retain the uncertain settings request's own comparison baseline across other-card
saves; protect unsaved inline renames when another rename/add starts; describe
promotion as waiting-list order. Preserve separate card saves and reuse existing
confirmation/recovery controls. Source evidence:
`docs/references/admin-ui/reviews/2026-10-02/earlier-pages/signup-setup/review.md`.

**RC03:** retain unchanged exact schedule instants and immutable full submitted
values for readback (including opening flag); preserve retained overdue enabled
opening validation exception; use shared DST-safe conversion in Overview reopen,
resume and evidence-code consumers; constrain picker to short viewport height with
reachable overflow/footer. Extend uncertain readback mock with read failure that
retains uncertainty. Correct unconditional automatic-close README requirement and
outdated claim of an existing confirmation ladder. Evidence/source lines:
`docs/references/admin-ui/reviews/2026-10-02/earlier-pages/schedule/review.md`; 15 files stable, source-only.
R4 permits necessary bounded shared CSS height/overflow changes, not redesign.
Focused checks reproduce seconds/subsecond and repeated-hour preservation, exact
readback conflicts/read failure, retained overdue opening, skipped/repeated local
Overview dates, short viewport footer reachability and nested-dialog keyboard use.
Related application precision defect is AU10, not a reference fix to claim as done.

**RC04:** fix the three functional findings from the stable Teams reference review.
Use immutable pick identity for Undo and its readback, with current membership
checked; avoid attributing another admin's pick or a changed order to this request.
A matching unchanged redraw cannot prove failure. Remove unjustified safe-retry
claims. Existing-team readback must match its ID and every intended field including
draft inclusion; new-team recovery must not identify creation by name alone.
Roster success states must distinguish local republishing from actual WOM
NotManaged/Unchanged/Pending/Sending/Retry/Failed/Conflict/Unknown results.
Preserve approved composition, scramble motion and current interaction model.
Reproduce the named timeout-without-save scenarios and readback failures, then
reuse the same reviewer for the named recheck. Evidence:
`docs/references/admin-ui/reviews/2026-10-02/earlier-pages/teams-identity/teams-review.md`.

The review's fourth finding (forced `busyMinQuick=250`) is a pending presentation
choice, not an automatic removal requirement. The user likes brief visible saving
feedback and asked for slightly shorter draft feedback. Preserve it for now;
reconcile prototype latency versus a production presentation minimum at integration.
No new delay standard or removal is approved by the reviewer alone.

Identity's named correction recheck returned SOURCE PASS on 2 October 2026; no
remaining named reference defect. Twelve files were stable; no browser/runtime
checks were executed by the reviewer. AU08/AU09 are technically complete;
application UI binding remains pending. Evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/teams-identity/identity-review.md`.

For all: preserve approved layout, typography, colours, spacing, components and
animation feel. Expect JavaScript/state, markup/copy and documentation changes;
no cosmetic CSS redesign. A necessary directly related shared fix needs focused
consumer checks, not a whole-site pass. Do not fold in AU01/AU02 backend fixes,
new product choices or unrelated improvements. Run focused executable reference
checks that reproduce the named failures/recovery paths and a scoped diff/style
check. Independent reviewer rechecks named findings and direct consequences;
user visual acceptance remains separate if any visual state materially changes.

At completion record changed files, checks/evidence, reviewer outcome and exact
file hashes. Mark repository corrected/reviewed and canvas awaiting sync separately.
For this bounded sync the verified repository files are authoritative: Claude copies
them to matching artboards and shared files without redesign or overwriting with
older canvas content, then verifies parity. This is not blanket reassignment of
visual design authority. Keep completed RC records; append future bounded reference
correction tickets rather than spending Claude usage on behavioural bug fixes.

### Approved and proposed application follow-ups — AU16–AU24 and CAT-1

These scopes come from the seven completed source comparisons and later explicit
user decisions, not a new audit. Accepting the requirements summary records the
delivery target; implementation still needs a user resume. Reuse existing services
and narrow projections; no generic receipt system, new persistence framework or
cosmetic redesign is implied. Transport choices are resolved at ticket handoff.
Keep visual binding separate when the existing backend already provides the data.
This subsection records scope; delivery state follows each owning ticket. AU20 is
implemented/remediated and awaits external recheck. AU14/AU17a/AU17/AU19 are backend implemented, remediation round 2 done, Claude recheck pending, binding pending; see their owning rows and remediation evidence.

| ID | Bounded outcome and protected behavior | Focused acceptance / known decision boundary |
| --- | --- | --- |
| AU16 | Audit: restore already-required hidden-event history; authorized single-entry read/event choices; exact action vs area filters and timezone-aware calendar dates; preserve immutable redaction and hidden workspace restriction | Hidden event entry readable only under ordinary Audit permission; secrets excluded; specific vs prefix results; DST spring/fall days; unavailable entry/strict paging; use existing presenter and truthful historical fallbacks |
| AU17a | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC07) | D11 Playing-only current/released assignments of current/former members of the submission’s own team; invalid attribution leaves no partial writes; retained identity survives readiness/results; image/upload/history unchanged. Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b5/au17a.md` |
| AU17 | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC07): Contribution line plus safe current-state readback only | Correct adds/remaining/completes/nothing-left outcomes; readback must not attribute uncertain success to a request. No per-account prior-approved count or content outside `Review.dc.html`; S9’s credited-account team-leave warning is in scope, while active-account-since/switched-from content is dropped; full-pool work belongs to AU17a |
| AU18 | B2 implemented, awaiting Claude review: final WOM refresh outcome on each published version, plus version/reopen history (who and when) | Successful, failed and distinct skipped outcomes retained in immutable publication inputs; no backfill. Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b2/au18.md`. No another-event-current readiness row; publish/reopen refusals and WOM cadence unchanged. AU12 owns ranking |
| AU19 | B5 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC05): structured Board issues, version comparison and authoritative current-state readback over full intent | Compare identity/name/description/artwork/objectives/EHB; late evidence retained; publish vs discard distinguished; no automatic replay. Reuse existing calculator/leases/snapshots. AU11–AU13 own scoring/planning changes |
| AU20 | Approved configured-window/end synchronization and structured recovery | All numbered requirements and focused acceptance in **AU20 — configured-window and end synchronization** above are mandatory, including early end, Resume, fallback, WA-9 and accepted snapshot limit; implemented and remediated, external recheck pending |
| AU21 | Catalogue: explicit shared-item adoption on add, source/item identities/versions and scoped CRUD/mapping/value recovery | Creation uncertainty cannot duplicate/crash; every intended field and shared image scope explicit; preserve independent drafts, item provenance/price invalidation, valid reactivation mechanics under AU23’s ordinary rate-text rule and dependency-safe deletion. No silent shared-item merge or automatic price adoption |
| AU22 | Accounts: bind existing overlay/projections to accurate counts/detail and role/status/ownership readback; transient target-bound reset response | Late response cannot expose A's secret in B; no secret history/log/storage or secret readback; permission loss/session invalidation handled; status not changed by revoke; current-state matching not attributed to request. Reuse reset permission-under-lock rule, no implicit new reset expected-version policy; typed transfer is AU24 |
| AU23 | Ordinary rate-text rolls; SuperAdmin advanced roll group; final-chance input and retirement of Only after | Mandatory Catalogue decisions above; real PostgreSQL role/validation/audit/stale checks, default new groups, existing groups and immutable snapshots preserved; no parent-EHB ticket |
| CAT-1 | Informational agreed team size moves to boss/activity | Mandatory pre-migration set/conflict recheck, whole number >=1/default 1, ordinary Admin edit, no calculation effect, historical snapshots unchanged; see Catalogue decisions above |
| AU24 | Ownership transfer: add approved typed destination public username alongside current owner's password | Reject missing/wrong/mismatched recipient text server-side; confirmation retains selected recipient/version; atomic sole owner, eligibility/session invalidation and secret privacy preserved. No new reason or typed confirmation on ordinary role changes |

The earlier reports are evidence of source observations, not claims these gaps
were implemented. The complete 47-finding map is in the functionality register;
report links below preserve reproduction details. Existing server protection is
not rebuilt just because the reference mock omitted it.

### Bindings not shown in the design references

Established by the user's 4 October instruction: “From now on, every ticket that adds
functionality the references don't show adds its row.” Seeded from `review-notes/08-decisions.md`,
“Known UI differences from the frozen design references”, with Catalogue decisions
owned by the section linked below. This register includes missing bindings and
intentional differences from a reference; it does not authorize implementation,
reference edits or new displays on the current pages. All AU work targets the new UI.

Every ticket that adds functionality the design references do not show must add or
update its row here, including backend-only outputs awaiting binding. The UI
integration plan must make every row a required item in the affected page's
route/handler/binding inventory, resolve its open decisions before binding, and
include it in binding review. Keep decisions in their existing owner and link them
here; do not silently omit a row because the reference has no corresponding control.

| What it is | Page and design reference | Source ticket/decision | Binding ticket | Open decisions |
| --- | --- | --- | --- | --- |
| AU19 approval bindings group per-position `board-incomplete` issues into the reference’s single “(N empty)” item, jumping to and highlighting the first empty position; `Working` is the publication projection during a correction | Board — [Board.dc.html](docs/references/admin-ui/Board.dc.html) | AU19, `08-decisions.md` “AU phase plan”; review36c F6/F8 and brief38 item6 | RC05 / BR-10 | Grouping is required at binding. Per-drop rate notes shown by the reference are not supplied by the contract; publish currently returns only its first refusal reason where the reference lists every reason. Both differences remain open points for BR-10 before binding; no additional backend or UI behavior approved here |
| Blocked approval/contribution state replaces any numerical approval claim; direct link to the earlier pending upload preserves queue/filter context | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html) does not show it | BR-1/G1 and B5 AU17; shared allocation returns structured blocking submission ID/upload time; `08-decisions.md` “Step 3 decisions” | RC07 / BR-10 | None; preserve the approved navigation and return context |
| Cap-limited Contribution wording: an exhausted or partial drop/item cap can limit Add while the objective still has remaining work | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html), Contribution line | B5 review B2; brief35 item 3; shared approval allocation | RC07 | Backend numbers verified; wording remains a binding decision, no current-page display added |
| Correction picker includes released Playing accounts/former team members, marked Released/Left team/Current; Informational never selectable | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html) shows a current-team account list without these markers | AU17a; D11 option b in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-brief-decisions); B5 `au17a.md` | RC07 / BR-10 | Scope decided; bind the markers and derived participant without independent participant editing |
| Uncertain Teams recovery retains immutable pick/team/member IDs and all intended fields, including inclusion/image/role/account; same order or reusable pick number never proves a request. Failed/unavailable reads remain unknown | Teams — [TeamsDraft.dc.html](docs/references/admin-ui/TeamsDraft.dc.html) predicates use reusable numbers, partial fields and changed order | AU14; Teams source findings 1–2; B5 `au14.md` | RC04 / DRF | Bind current-state wording; unavailable new-team creation identity remains uncertain; no automatic replay or safe-retry claim |
| Local roster publication (PublishedAt) is separate from existing last recorded WOM management/operation/local-queue outcomes; only operation CreatedAt >= roster PublishedAt establishes that the operation was created at or after the current roster publication; UpdatedAt cannot establish current-roster relevance (an older in-flight update can finish after republish); a merged pending update keeps its older CreatedAt and conservatively reads as older; queued/failed/unknown never means today’s roster is synchronized | Teams — [TeamsDraft.dc.html](docs/references/admin-ui/TeamsDraft.dc.html) claims WOM is updated after local republishing | AU14; Teams source finding 3; B5 `au14.md` | RC04 / DRF | Bind the existing outcome statuses honestly; no new provider operation or permission |
| Omit the “another event is current” readiness row despite the reference; retain server lifecycle/finalization guards | Final Review — [FinalReview.dc.html](docs/references/admin-ui/FinalReview.dc.html) shows the row | Narrowed AU18 / Step 0 decision; no additional current-event readiness UI authorized | RC08 / BR-10 | None; omission is decided |
| Manual-team members in participant lists and waiting positions | Participants — compare with [Participants.dc.html](docs/references/admin-ui/Participants.dc.html) during binding | TD-2 option B; G3b-3 | P-1 | Verify the existing reference against the decided membership behavior during P-1 |
| Authorized single-entry Audit read, including “This entry isn't available” | Audit — check [Audit.dc.html](docs/references/admin-ui/Audit.dc.html) | AU16; B1/B2 decision D4 | RC06 / WA-5 | Check the reference and settle the single-entry binding in the integration plan |
| Audit event dropdown filter with hidden events marked | Audit — check [Audit.dc.html](docs/references/admin-ui/Audit.dc.html) | AU16; B1/B2 decision D4 | RC06 / WA-5 | Check the reference and settle the filter binding in the integration plan |
| WOM refresh outcome follows the reference's unsuccessful-only note; stored next eligible time remains data only | Final Review — [FinalReview.dc.html](docs/references/admin-ui/FinalReview.dc.html) does not display next eligible time | AU18; known UI differences recorded after B1/B2 review | RC08 / BR-10 | No additional next-eligible-time display approved |
| Tile EHB override set/change/reset with calculated baseline; submit explicit change-override intent | Board — bind the control in [Board.dc.html](docs/references/admin-ui/Board.dc.html) to the reviewed backend contract | AU11 and its explicit-intent remediation | RC05 / BR-10 | None; verify the intent mapping during binding |
| Catalogue rate entry and “How the rate is counted” panel follow the decided fields and permissions | Catalogue — intentional differences from [Catalogue.dc.html](docs/references/admin-ui/Catalogue.dc.html) | AU23 backend delivered in B4; [Catalogue decisions — AU23, CAT-1 and WA-5](#catalogue-decisions--au23-cat-1-and-wa-5-4-october-b4-backend-delivered-wa-5-binding-pending); `08-decisions.md` “Drop-rate mechanics” / “Catalogue layout” | RC10 / WA-5 | None; bind the delivered final-chance input, retired Only-after input, ordinary rate-text rolls and SuperAdmin roll-group editing |
| Informational activity Team size in Settings and Add activity beside Kills per hour | Catalogue — intentional difference from [Catalogue.dc.html](docs/references/admin-ui/Catalogue.dc.html) | CAT-1 backend delivered in B4; [Catalogue decisions — AU23, CAT-1 and WA-5](#catalogue-decisions--au23-cat-1-and-wa-5-4-october-b4-backend-delivered-wa-5-binding-pending); `08-decisions.md` “Catalogue layout” | RC10 / WA-5 | Bind the delivered field; preserve the migration pre-check and any resulting data decision |
| Structured confirmation naming every affected activity before a shared item rename or image change, including Add-drop adoption with a different normalized image; no per-activity split | Catalogue — the reference only shows a generic “also used by” indication and does not show the named-activity confirmation | AU21/D7 option (a) and D9 option (a); [08-decisions.md, B4 review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-review-decisions); B4 remediation item 1 | RC10 / WA-5 | Return affected activity IDs and names; accept a submitted set only when it equals the current set; current-page binding remains refused until the new UI supplies the intent |
| WOM end-update status NotRequired/Pending/Succeeded/Rejected/CouldNotUpdate (“WOM end could not be updated”); EndUpdateTargetAt, EndUpdateRequestedAt and sanitized EndUpdateErrorCode; FinalReviewReadiness.WomEndUpdateStatus and FinalizationOperationResult.WomEndUpdateStatus. Backend only, no new current-page display | No reference shows it; candidate pages are WOM [Wom.dc.html](docs/references/admin-ui/Wom.dc.html), Overview [Overview.dc.html](docs/references/admin-ui/Overview.dc.html), Final Review [FinalReview.dc.html](docs/references/admin-ui/FinalReview.dc.html) | WA-2 / AU20; `08-decisions.md` “B3 (AU20) brief decisions”; brief 23 item 4 | WA-5 / OS-1 / BR-10 as selected by the UI integration plan | Placement and final binding owner remain open; the UI integration plan must decide before the affected pages are bound |
| Structured fetch eligibility, AU18 skip reason and next permitted time; typed credential/current-operation identity, phase and next attempt | WOM — [Wom.dc.html](docs/references/admin-ui/Wom.dc.html); these backend outputs exceed the generic Fetch wording. Final Review retains its separate AU18 data-only next-time decision above | WA-6 / AU20; Step 4 D4 and B1/B2 D2 in [B3 source attribution](docs/references/admin-ui/reviews/2026-10-04/au-b3/item6-documentation.md); brief 23 items 1 and 6 | WA-5 / RC09, as settled in the UI integration plan | Decide which structured WOM outcomes are displayed and their binding before integration; keep current-page generic Fetch wording and no new Final Review next-time display |
| Resume always requires a validated future replacement end, even when the retained configured end is future; early end stores the ceiling-minute configured end and precise actual end | Overview — [Overview.dc.html](docs/references/admin-ui/Overview.dc.html) currently conditionally asks for the replacement only after the retained end has passed | WA-2 corrected Resume rule; AU20 item2; [remediation brief](docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/supplied-brief.md) item6 / review24f R1 | RC01 / OS-1 | Always-present replacement end is decided; settle dialog text and bind its validation before OS-1 acceptance |
| Cancelled/Finalized/Archived admin pages open read-only; each page binding removes its view redirect from EventMutationCapabilityPageFilter; all changes remain refused under D16 | All event admin pages — Identity, Schedule, Signup setup, WOM, Final Review, Participants references | D17 / D16 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | Each page integration inventory | Decided future obligation; no current-page redirect removal |
| Signup answers shows every custom question as question: answer, editable until draft start then read-only; drop Participant’s note and its row flag | Participants — Participants.dc.html | S1 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | P-1 | Decided future binding; no new field |
| Start checklist and postponed automatic start, when another event is still current (Live or in Final review): “Publish the results of ‹other event› first”, linking to that event | Overview — Overview.dc.html | S2 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | OS-1 | Decided future binding |
| Failed automatic signup opening attention: “Signups didn’t open automatically: ‹reason›”, styled like “Automatic start postponed”, with normal Open signups now and existing checks | Overview — Overview.dc.html | S3 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | OS-1 | Decided future binding |
| “‹account› left ‹team› at ‹time›. Drops before that time count for the team; later ones don’t.” Drop active-account-since/switched-from content | Review — Review.dc.html | S9; remediation item3 supplies nullable credited participant team-leave time in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | RC07 | Backend read implemented; warning binding pending |
| Deactivate confirmation names affected draft boards with an event Board link for each; approved/published snapshots stay unaffected | Catalogue — Catalogue.dc.html | S10 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | RC10 / WA-5 | Decided future binding |
| Remove Move up/Move down in queue; manual queue reordering is not approved | Participants — Participants.dc.html | S13 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | P-1 | Decision closed; reference/binding change pending |
| Reset panel: “The link stops working after 60 minutes, after one use, or if this account’s role, status or ownership changes.” | Accounts — Accounts.dc.html | C-ACC-1 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | AU22 / Accounts binding | Decided future binding |
| AU24 ownership-change confirmation includes typed username | Accounts — Accounts.dc.html | C-ACC-2 / AU24, [33c sweep](/Users/christopher/Documents/BingoWebpage/review-notes/33c-sweep-admin-tools.md#c-acc-2--plan-gap-au24s-typed-username-field-has-no-row-in-the-bindings-not-shown-register); brief35 item8 | AU24 / Accounts binding | Required field missing from reference; future binding |
| Open/Reopen checklist includes every applicable server refusal with repair links (Discord configuration, usable code, valid questions, public-event overlap, future close); confirmation warns text answers are public when applicable, and the “reopening keeps existing signups” warning is dropped; Live WOM-sync-failed attention links WOM and says lifecycle actions aren’t affected | Overview — Overview.dc.html | A-Overview-3/6/7 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | OS-1 | Decided future binding; no Events-directory attention item |
| Readiness includes any missing server blocker, including Submission cutoff required and Final-review cycle is missing, with server text | Final Review — FinalReview.dc.html | B-Final-1 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | RC08 / BR-10 | Decided future binding; preserve narrowed AU18 omission |
| Create checklist retains its three rows and adds each further applicable server refusal | WOM — Wom.dc.html | C-WOM-2 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | RC09 / WA-5 | Decided future binding |
| AU17a picker and Review/Board/Teams JSON readbacks answer a lost or disabled session with302 to login, not JSON; detect the redirect and show what wasn’t saved before sign-out (C-CMP-2) | Review — Review.dc.html; Board — Board.dc.html; Teams — TeamsDraft.dc.html | B5 remediation item7 planner wording correction (planner’s, not the user’s); C-CMP-2 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | RC07 / BR-10 / RC04–DRF | Future binding obligation; existing authentication unchanged; handler Forbid is not an HTTP403 claim |
| Participant-list Paid/Unpaid `Payment` handler delegates to the existing service policy in every state except Discarded; list display remains unchanged | Participants — Participants.dc.html | S4 option (a) in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md); brief40 S4 | P-1 | **Server part done (brief40); binding pending** |
| WOM `FetchCompetition` is available in Live and Awaiting Final Review, while WA-2 unmatched-end and all existing synchronization guards remain authoritative; development due stays Live-only | WOM — Wom.dc.html | S6 option (a) and WA-2 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md); brief40 S6 | WA-5 / RC09 | **Server part done (brief40); binding pending** |
| Direct Teams roster setup allows a missing configured end before actual start and refuses only a configured past end, with action messages naming the start and end | Teams — TeamsDraft.dc.html | S8 option (a) in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md); brief40 S8 | RC04 / DRF | **Server part done (brief40); binding pending** |
| Catalogue activity edits use the add form’s validation rules and messages before any write or audit | Catalogue — Catalogue.dc.html | brief40 F3; add/edit validation decision | RC10 / WA-5 | **Server part done (brief40); binding pending** |
| Schedule refuses clearing `Signup close` while signups are open, while a different valid future close remains allowed | Schedule — Schedule.dc.html | brief40 F4; decided Part 3 F4 in [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) | EI-2 / Schedule | **Server part done (brief40); binding pending** |

These sweep rows are future binding obligations, not implemented UI claims.
The five brief40 server fixes are complete with binding pending. F1 belongs to Teams binding.

### Proposed reference corrections — RC05–RC11, not dispatched

| ID | Page and exact findings | Required correction boundary |
| --- | --- | --- |
| RC05 | Board B1–B7 | Full-intent uncertainty/draft retention; takeover/version safety; late evidence; routes; decimals; faithful weighted EHB/short-event fixtures |
| RC06 | Audit A1–A4 | Hidden-history exception, exact action, DST date bound, reachable filter panel; strict page query normalization |
| RC07 | Review R1–R8 | Honest action/actor/reason recovery; explicit uncertain leave; dirty/stale protection; real reversal allocation; correct route/account/objective context and completed-manual guard |
| RC08 | Final Review F1–F6 | Honest readback, captured pending context/reason, version-link retry, complete shared-rank metadata, correct stale state, modal scroll lock |
| RC09 | WOM W1–W6 plus approved exact-window change | Exact UTC start/end matching and wording (remove five-minute allowance); coordinate affected Schedule reference validation. Credential capability, captured event responses, actual fetch freshness, protected unsent drafts, unresolved status and scroll lock; preserve submitted-secret clearing |
| RC10 | Catalogue C1–C8 | Safe full-intent creation/edit recovery; independent drafts; pending identity; fixed baselines; mapping/price invalidation; explicit shared-image scope; reactivation and direct routes |
| RC11 | Accounts A1–A8 | Target-bound transient reset secret, truthful readback, protected drafts, route/focus recovery, retained disabled status, captured transfer version, normalized search and modal scroll lock |

These extend the recorded correction queue without changing any completed ticket.
RC07/UI integration must bind the same structured approval-block data produced by
the Review service and preserve the queue's search/status context when opening
the earlier review and returning to the queue.
Catalogue ordinary rate-text rolls, retired parent input, activity team-size context
and advanced roll-group controls follow the 4 October AU23/CAT-1/WA-5 decisions;
typed ownership confirmation follows AU24, not the superseded report questions. No blanket removal of
reference functionality to match the old app. Full-pool evidence correction follows the later approved decision. WOM external
link option 1 is now approved after investigation; AU20 aligns the application with
the artifact, preserving active/unresolved-operation and date/lifecycle guards.

Common acceptance: reproduce the named normal/failure/stale/uncertain/navigation
paths with controlled fixtures, verify directly affected shared consumers and
reduced motion, retain approved layout and motion feel, then one independent source
review/named recheck. Do not replace production scoring/security with mock logic.
Record implementation, executed checks, review and canvas sync separately. Claude
syncs the reviewed file hashes after correction; no broad design/extraction pass.
Use [Board/Audit](docs/references/admin-ui/reviews/2026-10-02/remaining-seven/board-audit-review.md),
[Review/Final Review](docs/references/admin-ui/reviews/2026-10-02/remaining-seven/review-finalreview-review.md),
[WOM/Catalogue](docs/references/admin-ui/reviews/2026-10-02/remaining-seven/wom-catalogue-review.md)
and [Accounts](docs/references/admin-ui/reviews/2026-10-02/remaining-seven/accounts-review.md).

## Dashboard backend pass — approved, 2026-10-01

**Status:** backend technically complete, focused proof recorded and independent
Sol 6.1/high R1–R5 recheck PASS on 1 October. UI integration remains deferred.
Approved reference requirements are D01–D10 in
`/Users/christopher/Documents/BingoWebpage/docs/references/admin-ui/FUNCTIONALITY_CHANGES.md`.
This plan defines backend preparation; it does not apply the reference UI.
Earlier Dashboard exclusions belong to their earlier passes and do not exclude
this explicitly requested planning pass.

**Baseline:** the completed, independently reviewed Participants candidate in
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
branch `codex/participants-functionality`, including its committed Participants changes.
Do not start from the stale 573d checkout or the design-reference checkout.
Confirm checkout/ownership at dispatch; do not commit, copy away or replace
Participants work merely to establish a Dashboard branch.

### Outcome and boundary

Provide one authorized, read-only application result for the community Dashboard:
historical totals, latest-event additions, participation breakdown, latest recap,
sortable event history, current/upcoming card and community account figures.
All related figures must derive from the same eligible events and people sets.
Return stable event IDs and typed dates, values and availability/provisional
metadata so later UI binding can supply real Overview links and honest states.

Statistics/chart/history include Live, AwaitingFinalReview, Finalized and Archived;
exclude hidden/quarantined, cancelled and discarded events throughout. Live and
AwaitingFinalReview are labelled **Provisional** only in those statistics (no
extra Live badge required). The latest-event recap is ended-only: final review,
finalized or archived. Current/upcoming card may show the actual Live phase.
Website account figures remain a separate community population. Headline label
is Events; later UI binding can indicate current/provisional counts.

This pass does not replace Admin Index markup or its existing callers, introduce
a transport endpoint, change routing, install the canvas runtime, implement
animations, edit Claude's reference, or perform manual visual acceptance.
Read failures must remain failures rather than fabricated zero results; the later
UI binds loading, retry, localization and navigation to this boundary.

### Approved definitions — 2026-10-01

User decisions supersede the earlier ended-only population:

1. Retain disabled website accounts in registered totals and historical unique/
   returning people; emergency credentials are excluded. Login counts use the
   same website population and stored LastLoginAt. Disabling access must not
   erase participation. Unlinked people count once per event but never become
   invented website identities; expose unlinked coverage separately where needed.
2. Select the compact card deterministically: Live first, then the earliest
   scheduled future/preparation event, then an unscheduled setup fallback.
   Multiple Live events choose latest actual start; remaining ties use stable ID.
   Do not change lifecycle state or silently make overdue schedules look future.
3. Latest ended recap uses actual end descending, then actual start and stable ID.
   Latest contribution/chart chronology uses actual start; ongoing history sorts
   before ended history by default, then latest actual start, while ended history
   uses actual end descending. These deterministic display ties are technical
   ordering, never competitive tiebreaks. Returning classification uses strictly earlier
   actual start (equal starts share a cohort, not an arbitrary ID advantage).
   New accounts means CreatedAt after the latest actual end through one request
   clock; without an ended event use the last 30 days. Missing authoritative
   dates remain unavailable instead of borrowing unrelated creation dates.

Existing requirements remain: count qualifying historical team participation once
per person per event, preserve pre-Live departure exclusion (Live eligibility ends at the shared request clock), imported history is
unlinked, approved submissions exclude reconstructed import contributions, and
zero differs from unavailable. Current official placement snapshots own winners;
provisional/reopened review cannot borrow an old official winner. Preserve shared
first place rather than inventing a winner tiebreak. EHB means stored period gain
with account coverage, never signup EHB and never a Dashboard-triggered WOM fetch.

### Readiness and source mapping

One bounded independent Sol 6.1/high read-only readiness review resolves actual
source mappings. The orchestrator records technical resolutions; the implementer
reconciles affected DATA_MODEL/source mapping under the approved behavior already
recorded in PRODUCT_REQUIREMENTS and FUNCTIONAL_CONTRACTS. Readiness is not an
implementation review or permission to change product scope.
Reuse D01–D10 and this inspection; no repeat whole-project audit.

Known sources: Account has WebsiteAccount/EmergencyCaptain, CreatedAt, LastLoginAt
and disabled state; EventParticipant.AccountId is nullable; TeamMembership retains
JoinedAt/LeftAt; BingoEvent retains actual lifecycle dates; finalization snapshots
have explicit active/unfinalized versions and placement snapshots. Historical
import constructs CsvImport participants without AccountId and membership starting
at the historical event start. These facts are source evidence, not production
data inspection.

Readiness must settle:
- Membership interval eligibility, distinct participant/account handling,
  unlinked platform records and any historical import special cases. Account
  transfers and multiple game accounts must not inflate people.
- Approved submission eligibility/source exclusions, active finalization and
  frozen board denominator, ties and retained names after current data changes.
- EHB mapping: CachedEventCompetitionActivityProjection reads persisted data,
  but its current-roster calculations must not be assumed to represent departed
  historical participants. Identify compatible stored/frozen coverage; expose
  unavailable data instead of rebuilding or fetching it. If a required historical
  source does not exist, report the exact limitation before adding persistence.
- One consistent read snapshot and request clock, enabled Admin authorization,
  hidden-event exclusion and supported event destinations.
- Actual enum/date fallbacks and proposed card/cohort edge cases. Surface any
  product contradiction rather than deciding it inside query implementation.

### Execution slices and journey coverage

Owner for each backend slice: one implementer under one orchestrator, sequentially.
Readiness and final independent review are separate roles. Independent review uses
Sol 6.1/high per user preference; exact dispatch identities/models are recorded
at dispatch. Implementation uses gpt-5.6-luna/max. Orchestrator gpt-6.1-sol/high is explicitly user-approved for this assignment
(1 October substitution for unavailable gpt-5.6-terra/medium). No routine
extra verifier. Planner /root; app task 01a0ec9a-76e3-7252-9850-3f260c612e59.
Orchestrator must own waits, wake each next worker, and report completion/blockers
to /root through collaboration; do not rely on an app-thread callback.

Companion assignment, user-authorized 1 October: perform a read-only functional
completion comparison of the finished Events reference against this application's
actual capabilities. This is not browser inspection or Events implementation.
A Sol 6.1/high reviewer reads Events.dc.html, shared behavior and README in
the Documents reference folder and maps E01–E04 to actual application sources.
Return concrete supported/gap/intentional-prototype distinctions, small behavior
deviations, source locations, and proposed register updates to /root through this
orchestrator. Protect Claude's files. No production edits, new tests, browser
inspection or broad UI redesign for this comparison. Root owns final product
decisions and the shared functionality-register reconciliation. This read-only
assignment may overlap Dashboard readiness within available slots.


| Slice / entry | Action and result | Proof and later reachable step |
| --- | --- | --- |
| B1, enabled Admin opening Dashboard, no/imported/mixed Live and ended history | Read eligible history, shared people sets, headline totals, latest additions and chart cohorts; no persisted mutation | Real PostgreSQL fixtures: zero vs unavailable, multi-account/team moves, departed membership, hidden/cancelled/discarded exclusion, linked/unlinked counts and equal-start cohorts; later chart/history open real event |
| B2, same read with provisional/official/reopened history | Add recap, current official winner(s), frozen board completion and optional covered EHB; produce six stable null-last history sorts | Persisted finalization/reopen and retained snapshot fixtures, approved vs reconstructed/reversed submissions, partial/missing/measured-zero EHB, board ratio/ties; later recap/history link Overview |
| B2, current/upcoming and community data | Select card, phase/relevant date and signup counts; website total/new/logged-in counts using one clock | Multiple Live/scheduled/unscheduled candidates, no candidates, disabled/emergency accounts, null logins, zero denominator and precise timestamp boundaries; later card opens selected event |
| Cross-cutting, unauthorized/disabled actor or failed read | Reject unauthorized access and propagate recoverable read failure; repeated reads do not write or synchronize | Exercise real authorization/application and PostgreSQL boundary, deterministic fixtures, cancellation/failure with no fake successful zeros, consistency under relevant concurrent change; later UI handles retry/lost access |

Use existing fixture conventions and controlled databases, never user-owned data.
Timestamp tests use explicit UTC microsecond-aligned instants and non-aligned input
with PostgreSQL round-trip proof wherever comparison/normalization is affected.
Do not prove historical correctness solely with in-memory grouping tests.

### Complexity budget and completion

Budget: one focused application read contract/result family and Infrastructure
query implementation if no existing appropriate boundary can own these global
statistics; ordinary DI registration and focused tests. No new tables, migrations,
background jobs, external API calls, analytics framework, cache infrastructure,
policies, UI pages or HTTP routes. Reuse existing authorization and authoritative
snapshot/cache semantics without invoking write-capable lifecycle/sync operations.
Avoid per-row lookup growth; inspect query shape with the focused integration proof.

Complete B1 first as an executable checkpoint, then B2 and focused checks. Run
applicable build/format/diff checks once on the stable candidate and one fresh
Sol 6.1/high independent review; named findings return to the same implementer
and reviewer. No broad browser suite or unrelated backend regression expansion.

Completion requires implemented result contract, passing relevant executable
proof, independent review, reconciled authorities and a concise UI-binding
handoff identifying source/coverage limitations. Update D01–D09 separately with
actual implemented/executed/reviewed evidence; do not mark D10 integrated.
Record commands/results and deferred manual journeys in existing status/checklist
owners. Stop before UI binding, manual acceptance, packaging, push or deployment.

### Completion and UI-binding handoff — 2026-10-01

B1 and B2 are implemented in the six Dashboard-owned source/test files listed
in `CURRENT_STATUS.md`. The application contract is
`IAdminDashboardService` (with the compatibility alias
`ICommunityDashboardService`); Infrastructure registers both interfaces and
performs one no-tracking, repeatable-read projection. The result carries stable
event IDs, typed UTC dates, measured/unavailable values, EHB coverage and
provisional flags. The later Admin Dashboard UI can bind event links from
`OverviewPath` (`/Admin/Events/Manage/{id}`), the current card, recap, chart and
history without adding a route or changing the existing Admin Index callers.

Recorded worker execution evidence is retained under
`docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/dashboard/`:

- Application Release build: `dotnet build tests/Bingo.Application.Tests/Bingo.Application.Tests.csproj --configuration Release --no-restore`, passed with 0 warnings/errors.
- Ordering proof: `dashboard-ordering-fixed4.trx`, **2/2 passed**, from the focused `DashboardHistoryOrderingTests` run. All six history fields use null-last ordering in both directions and stable EventId ties.
- Integration Release build: `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore`, passed with 0 warnings/errors.
- Recorded PostgreSQL proof: `dashboard-b2-post-ordering.trx`, **7/7 passed**, from the focused `AdminDashboardIntegrationTests` run. It includes deterministic UTC fixtures, a non-microsecond input persisted through PostgreSQL, authorization/transaction/cancellation failure behavior, and no-write checks. The source TRX was omitted from the durable H1 copy; its metadata and disposition are retained there.

D01–D09 below are implemented, executed at applicable backend boundaries and
independently source-reviewed. UI acceptance remains separate:

- **D01 — implemented/executed/reviewed:** enabled Admin read boundary, event ownership,
  valid membership intervals, linked/unlinked identity, disabled-account
  retention, emergency exclusion and hidden/cancelled/discarded filtering.
- **D02 — implemented/executed/reviewed:** headline statistics and availability metadata,
  including zero versus unavailable and provisional Live/final-review states.
- **D03 — implemented/executed/reviewed:** latest additions/community population with one
  request clock, strict stored dates, disabled-account retention and emergency
  exclusion.
- **D04 — implemented/executed/reviewed:** actual-start chronology and equal-start cohort
  handling for contribution/chart/history population.
- **D05 — implemented/executed/reviewed:** ended-only recap, active official placements,
  retained snapshot names, shared winners, published frozen-board denominator and
  reopened/provisional winner suppression.
- **D06 — implemented/executed/reviewed:** compatible stored EHB bulk coverage with
  complete, partial, measured-zero and unavailable states; no historical per-row
  provider fetch or signup EHB substitution.
- **D07 — implemented/executed/reviewed:** six typed null-last history sorts with stable
  EventId ties; UI query-string/back/reload binding remains deferred.
- **D08 — implemented/executed/reviewed:** deterministic Live/scheduled/unscheduled card
  selection, overdue metadata, nullable capacity and stable ties without lifecycle
  writes.
- **D09 — implemented/executed/reviewed:** website-account total/new/logged-in figures
  using the captured request clock and precise stored date boundaries.
- **D10 — not integrated:** no Dashboard UI, route, transport endpoint, browser
  walkthrough or manual visual acceptance is claimed.

Known source limitations are intentional: imported-only approved-submission
coverage remains unavailable; missing or incompatible historical EHB remains
unavailable unless compatible stored generation/fingerprint coverage exists;
measured zero remains distinct from unavailable. Read failures and provider
exceptions propagate. No tables, migrations, jobs, cache, provider fetch,
lifecycle write or new policy was added. Independent reviewer
`/root/dashboard_backend/independent_review` (`gpt-6.1-sol` / high) passed the
final named R1–R5 recheck. It inspected source and recorded evidence without
rerunning tests. The backend pass is technically complete; UI integration and
manual acceptance remain deferred.

### Named review remediation handoff — 2026-10-01

The fresh independent review by `/root/dashboard_backend/independent_review`
(Sol 6.1/high) returned **CHANGES REQUIRED** with R1–R5. The same implementer
applied only those named corrections and focused proofs. The same reviewer
completed the final named recheck with **PASS**, resolving R1–R5 and their direct
consequences without a repeated broad review.

Recorded named-remediation evidence is retained under
`docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/dashboard/`:

- `dashboard-remediation-r2-r3-r1-r5.trx` — **8/9 passed** in real PostgreSQL.
  The only failure is class initialization with `28P01 password authentication
  failed for user "postgres"` before the query-shape test body; the other eight
  cases pass, including the new interior Live departure, missing/inverted
  ended-boundary availability, independent login boundary, weighted published
  drop plus importless AdminCreated roster, and reopened winner suppression.
- `dashboard-remediation-r5-queryshape-retry.trx` — **1/1 passed** when the
  initialization-only failure was isolated. It covers the fixed query shape,
  no tracked writes, read-failure propagation and repeatable-read boundary.
- `dashboard-remediation-r3-original-login-boundary.trx` — **1/1 passed** for
  the affected original cohort fixture, including the four-account independent
  login-window expectation.
- `dashboard-remediation-ordering.trx` — **2/2 passed**. The six history
  fields, including EventDate, sort null-last in both directions with stable
  EventId ties.
- The earlier `dashboard-remediation-r1-r5-fixturefixed.trx` (9/9) was reported
  historically but was not retained in the durable copy; it is not durable proof
  of the latest source identity. Release builds and the prior Dashboard 7/7/non-microsecond
  PostgreSQL evidence remain retained, and the latest test command rebuilt the
  affected integration project successfully.

The R2 contract now distinguishes an eligible ended-state event from a usable
actual ended boundary: missing or inverted dates make Community
`NewWebsiteAccounts` unavailable with `Since=null`; the 30-day creation
fallback is reserved for genuinely absent ended-state events. R1, R2, R3, R4
and R5 are implemented with the named focused evidence above, while the
independent review is **PASS** after the same reviewer verified the stable
source identity and recorded proof. Final disposition is recorded in
`docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/dashboard/dashboard-review-final.meta`.
The Dashboard PASS covers the earlier source hashes `846aed4b65b5ac3f1a853dda275a6e6269cdb97453600eaeb82e4fd99bc5d55f` (service) and `871051bc2e148237ab7d9927de2c56cb44b3e1d06ca3fb47afccdca4ccc70f99` (interface). The later change is AU04’s `GetEventParticipationAsync`; AU04’s independent review covers that change.
No broad suite,
UI/reference work, manual acceptance, packaging, commit, push, merge or
deployment is authorized by this handoff.

## Participants backend pass — approved 2026-09-30

**Outcome:** implement the agreed Participants service/domain/persistence behavior
before applying the new UI. Preserve existing UI/forms and public self-signup.
Manual interaction/visual testing and new frontend controls/routes are deferred by
explicit user instruction; executable backend checks are not deferred.

**Checkout:** `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
branch `codex/participants-functionality`, starting at
`993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c` from clean `admin-simplification`.
The older `573d` planner checkout and Documents reference checkout are not the
implementation baseline. Claude owns the reference export/tokens/components;
workers do not edit those files.

**Roles:** planner `/root`, app task `01a0ec9a-76e3-7252-9850-3f260c612e59`;
collaboration orchestrator `/root/participants_backend`, Sol `gpt-6.1-sol` / `high`
(explicit user substitution for unavailable Terra); implementer/remediator
`gpt-5.6-luna` / `max`; independent reviews `gpt-6.1-sol` / `high` (user override).
Use the lean workflow in section 4.2.1. No extra coordinator or verifier.

### Execution and ownership

1. Run one bounded independent read-only readiness review of this complete slice
   under section 4.1. Reuse the completed reference functional audit; do not redo a
   whole-site audit. Identify account/primary/question mapping, missing stored EHB,
   service/old-caller compatibility, concurrency and directly affected projections.
   Resolve ordinary implementation choices in this plan; escalate only genuine
   product decisions with evidence and a recommendation. No implementation before
   named readiness blockers are resolved.
2. One implementer owns the connected service changes below sequentially; the same
   SignupService is shared, so do not split concurrent writers by feature name.
   Deliver an early working behavior/checkpoint and proceed through authorized
   scope without waiting for a new approval per operation.
3. Once implementation and focused checks are stable, use one fresh independent
   reviewer. Route defects to the same implementer and named rechecks to the same
   reviewer. Reuse passing evidence and do not launch a broad extra verification.
4. Consolidate CURRENT_STATUS and the integration handoff in this plan: service
   entry points/request/result shapes, commands/results, unresolved limitations,
   migrations if any and future UI binding. Stop before commit/push/deploy, reference
   edits, app restart, user-data mutation or manual acceptance.

### Readiness decisions — 2026-09-30

The bounded Sol 6.1/high readiness review by
`/root/participants_backend/readiness` found no unresolved product blocker or
schema requirement. Implementation uses the existing `PrimaryRegularAccount`
question plus matching `SignupAnswer` as primary authority. Swap/map question
associations for selected Playing accounts while retaining each assignment's
character identity, EHB and provenance. `AdminPrimaryCharacters` and the pre-Live
planned-account fallback must use the same authority for Admin-created records.

Preserve full-question `CreateAdminParticipantAsync` and
`CorrectAdminParticipantAsync` callers; expose distinct saved-link Add and
account-only correction/primary-switch boundaries. Saved-link Add rejects missing
stored EHB with actionable saved-profile feedback and requires unambiguous active
protected-primary/Playing-slot configuration. It does not use WOM or default EHB
to zero. Existing shared global links remain allowed.

New/changed transactions recheck the active Admin under the existing account lock
before the event lock. Queue order/positions use the existing
`WaitingListedAt ?? SignedUpAt` promotion authority consistently. Move captures the
pre-existing next eligible signup waiter before appending the mover, preserves
reservations, and ends current membership/role authority with history. Focused
PostgreSQL proof includes the real draft-start transaction boundary, not solely a
pre-set draft-lock fixture. No new UI binding or migration is selected.

### Journey coverage and proof

All new roster operations use an enabled Admin in an accessible event
Draft/SignupOpen/SignupClosed before team-draft start. Tests call the real application
boundary with controlled fixtures; these service capabilities are for later binding
from Participants table/Add/edit. Existing route callers remain protected.

| Slice | Action and persisted outcome | Boundary checks / next read |
| --- | --- | --- |
| P1 (F01/F02/F03) | Confirm selected waiter, with explicit +1 capacity only when full; move Confirmed to queue end and promote another; normal/override restore | PostgreSQL atomic capacity/status/order, no extra promotion, no immediate re-promotion, full/no-waiter/open-place rejection, restored sequence/reservation conflict, ended membership authority/history; persisted Participants/draft projections |
| P2 (F04) | Add saved accounts without questions/WOM, selected primary, initial payment, normal/override placement | Active owner/link validation, slot limits, duplicate within participant/event, missing EHB/configuration handling, false captain, absent custom answers, no provider or saved-link writes, all-or-nothing failure, existing Add caller compatibility |
| P3 (F05/F06) | Switch primary; add/remove/correct event accounts within configured slots without global mutation | A/B switch preserves identities and per-account EHB, at least one/exactly one primary, draft/name reads agree, shared links allowed, event conflicts rejected, saved names/links/order/EHB unchanged, correction preserves status/order/non-account answers |
| Cross-cutting | Authorization, stale/repeated/concurrent requests and draft-start races | Existing PostgreSQL fixture boundaries, expected versions/current actor checks, no writes/audit success on rejected request, no double capacity increase, unchanged ordinary promotion/private payment-note/finalized-roster behavior |

### Protected scope and complexity budget

Reuse `ISignupService`, `SignupService`, EventParticipant/assignment/question/answer
entities, EventParticipantAuthorityQueries and existing integration fixtures.
Preserve privacy, current actor authority, notifications, audit, locks, PostgreSQL
uniqueness, immutable competitive evidence, existing Live/finalized workflows and
stored numeric precision. Fixtures use deterministic microsecond-aligned UTC times
with affected precision/round-trip cases where relevant.

No new tables, services, dependencies, policies, jobs or speculative API framework
are budgeted. New request/result types and methods on existing boundaries are
allowed where needed. Production UI, client routing/history, CSS, animation,
reference runtime and manual test execution are out of scope. Do not add temporary
UI to exercise backend methods. Existing HTTP binding changes only if needed to
keep current callers safe/compatible, then test the affected real pipeline; new
frontend transport binding can wait. No Undo or general manual queue reorder.

Use scoped build/formatter/diff checks and discriminating Domain/Application and
real PostgreSQL tests for changed behavior. Add tests where existing checks cannot
detect the changed risk. Do not rerun the whole solution or broad browser suite
without a concrete reason. Environment failures are reported with the exact
unverified boundary; no tests on the user's database or external WOM calls.

**Completion:** all three slices implemented and independently source-reviewed,
focused checks passed or explicitly reported blocked, contract/data docs reconciled,
existing UI compatible, and a useful binding handoff recorded. This is technical
backend completion only. Future manual journeys are in MANUAL_TEST_CHECKLIST.md;
page approval remains in UI_PAGE_MATRIX.md.

### Completion and UI-binding handoff — 2026-09-30

**Technically complete.** Same-worker implementation and named remediation are
finished. Independent Sol 6.1/high reviewer
`/root/participants_backend/independent_review` accepted the stable source and
required executed proof, with no remaining findings or proof gaps. The reviewer
verified source, logs/TRX and command/assembly identity rather than rerunning the
checks. Manual acceptance and new UI binding remain deferred; page approval is
unchanged. No schema/migration, new service/dependency or temporary UI/HTTP endpoint
was introduced. Existing full-question Admin Add/correction and public signup
callers remain compatible.

Changed implementation files: `ISignupService`; Domain `EventParticipant` and
`EventParticipantCharacter`; Infrastructure `SignupService`,
`EventParticipantAuthorityQueries`, `ParticipantLiveService` and
`EventSignupLifecycleService`; Web Admin Participants and public Confirmation /
Signups read models; integration `Slice4ParticipantLifecycleIntegrationTests` and
`DraftOperationsIntegrationTests`. The six planner-authored documents remain
preserved; the complete checkout/status is in `CURRENT_STATUS.md`.

| Service entry point | Request binding | Authoritative result |
| --- | --- | --- |
| `ConfirmWaitingParticipantAsync` | `ConfirmWaitingParticipantRequest`: event, participant, actor; explicit `ExpandCapacityWhenFull`; expected event/response versions | `ParticipantQueueMutationResult`: success/error, participant/status/position, promoted participant, effective cap, changed |
| `MoveConfirmedParticipantToWaitingAsync` | `MoveConfirmedParticipantToWaitingRequest`: event, participant, actor; expected event/response versions | Same queue result |
| `RestoreAdminParticipantAsync` | `AdminParticipantRestoreRequest`: event, participant, actor; explicit expansion; expected event/response versions; existing WOM validation token where applicable | `ParticipantLifecycleResult`: success/error, status/position, changed, existing validation token |
| `AddSavedParticipantAsync` | `AddSavedParticipantRequest`: event, owner, actor; `PlayingCharacterIds`, `PrimaryCharacterId`; payment defaults Unpaid; explicit expansion; expected event version | `AdminParticipantResult`: success/error, participant/status/position, existing validation token shape |
| `SwitchAdminPrimaryAsync` | `SwitchAdminPrimaryRequest`: event, participant, next character, actor; expected current character and response version | `EventAccountMutationResult`: success/error, participant/primary character, Playing/total account counts, changed |
| `AddEventParticipantAccountAsync` | `AddEventParticipantAccountRequest`: event, participant, character, role, EHB, actor; optional question and expected response version | Same account result |
| `RemoveEventParticipantAccountAsync` | `RemoveEventParticipantAccountRequest`: event, participant, assignment, actor; expected response version | Same account result |
| `CorrectEventParticipantAccountAsync` | `CorrectEventParticipantAccountRequest`: event, participant, assignment, replacement character/EHB, actor; expected response version | Same account result |

Requests use authenticated actor identity; the service canonicalizes actor name and
rechecks enabled Admin authority inside the transaction. Saved Add uses character
IDs resolved through the owner's active saved links, not link-record IDs or
client-supplied saved EHB. Missing stored EHB rejects with saved-profile feedback;
Add does not call WOM. Supply observed versions/current primary to detect stale
intent and show the returned error/changed state. Capacity expansion requires the
future UI's explicit full-capacity confirmation; it only confirms/restores/adds the
selected participant. Move requires full capacity and another eligible waiter.

Primary authority remains the protected `PrimaryRegularAccount` question plus its
answer. Account correction releases the old assignment and appends the replacement,
retaining old identity/EHB/provenance and auditing the actual change. Saved global
names/links/order/EHB remain unaffected. Successful account changes notify the
linked owner using the existing privacy-safe event; promotion recipients include
enabled Admins. Failed and repeated no-change requests do not duplicate success
notifications/audit. PostgreSQL persistence is authoritative; refresh affected
participant/draft projections after success. Add and restore retain legacy result
shapes, so refresh event capacity/count/status rather than inferring capacity from
an error string. Waiting reads use `WaitingListedAt ?? SignedUpAt` consistently.

Saved final evidence is retained in the repository at
`docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/participants/`:

- Release Web build: `web-build-after-test-fixes.log`, 0 warnings/errors.
- Focused PostgreSQL seven-case run: `remediation-focused-final.log`, 7/7
  passed, covering selected/parallel confirmation, move, saved-account journey,
  restore rollback/retry and saved-Add negative/custom-answer boundaries. Later
  named cases supersede the initial draft/primary proof in that run.
- Real draft-start conflict/recovery: `draft-interleaving-controlled-proof`, 1/1.
  The controlled event-reader barrier is inside the real Serializable
  `DraftModel.OnPostStart` route; committed selected-capacity/account changes force
  a handled snapshot conflict and a successful real-route retry.
- Secondary-primary/stale projection proof: `primary-projection-proof-final`, 1/1;
  final same case `primary-invalid-current-slot-proof-final`, 1/1, additionally
  targets an existing Playing assignment whose question is inactive, and proves
  unchanged primary/assignments/answers/version/audit/notifications after a later
  same-context note save.
- Same-character EHB release-and-append/retry proof:
  `ehb-correction-proof-parsed`, 1/1, against PostgreSQL's partial unique key with
  deterministic microsecond-precise WOM provenance and unchanged global profile.

The durable copy retains the named compact `.log` and `.meta` command/exit
records; raw `.trx` artifacts were omitted. Original commands use
`dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore
--configuration Release --filter FullyQualifiedName~<case> --logger trx`, with
exact filters in the corresponding metadata:
`DraftStartInterleavesWithSelectedCapacityAndAccountMutationAtTheRealBoundary`,
`SavedAddAndEventOnlyAccountMutationsPreserveGlobalLinksAndPrimaryMapping`, and
`SameCharacterEhbCorrectionAppendsHistoryAndRepeatIsNoOp`. These freshly build the
Release test assembly; no stale `--no-build` assumption or full-suite claim applies.
Do not sum overlapping rechecks as unique test coverage.

Scoped formatter used `dotnet format Bingo.slnx --no-restore --verify-no-changes
--verbosity minimal --include <the twelve changed source/test paths>`; exact paths
and exit 0 are in `format-scoped-final-exact.meta`, independently checked by the
reviewer. Later changed-test formatting exit 0 is worker-reported with quiet
`format-slice4-invalid-slot.log`. `git diff --check` passed independently for
orchestrator and reviewer; saved exit metadata is `diff-check-final.meta`.
Earlier broader terminal-only worker counts are distinguished in CURRENT_STATUS,
not repeated as fresh candidate evidence. Earlier failed logs remain retained.

Bounded build diagnosis identified generated-output write denial (MSB3371) in the
checkout outside default writable roots; narrow authorized escalation recovered
build/test execution. No active environment blocker remains. All four named source
findings (assignment history, owner notifications, Admin promotion recipients,
rejection-safe tracked state) and their required targeted proof are accepted.
Temporary trace/debug tests and hardcoded diagnostic paths were removed from source.

Orchestrator reports completion to planner `/root` through internal collaboration /
final delivery. The previously rejected app-thread callback is not retried. Stop
here: future redesigned Participants UI integration/confirmations and manual
journeys need their own assignment; no packaging, reference edits, broad-suite
rerun, user-data mutation or app restart is authorized by this completion.

## Admin simplification — approved 2026-09-26

This completed baseline is part of the whole branch review, not excluded by later
AU work. Original ticket definitions and decisions are preserved byte-for-byte in
[the original plan snapshot](docs/references/admin-ui/reviews/2026-10-02/simplification/original-plan.md).
It is provenance, not current routing, dispatch authority or permission to restore
retired behaviour. The current product/contracts/data/UI documents and refinements
below govern the review target. Original exclusions applied to that pass only;
Dashboard, Luck and the new Admin reference work were authorized separately later.

Baseline implementation commit: `993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c`.
[Baseline verification excerpts](docs/references/admin-ui/reviews/2026-10-02/simplification/baseline-status.md)
preserve the September 28 user-run 1,555/1,555 result and September 29 independent
correction PASS. The later correction reran named failures/build only, not the whole
suite. These are historical results, not a fresh whole-branch verification pass.
Deployment, production cleanup and current runtime state are not inferred from them.

| Original tickets | Agreed simplification outcome retained | Later refinements / delivery distinction |
| --- | --- | --- |
| PRE-01 | Baseline/retained-data investigation, no guessed production migration | Historical evidence; no current production access implied |
| ADM-01, ADM-02 | Honest outcomes, useful sanitized Audit, shared confirmations | New shared Admin reference supersedes visual composition; AU16 server-side hidden-history/query correction is approved and queued; RC fixes pending |
| SEC-01 | Retire emergency authority, preserve actors/history | No reactivation through later UI |
| CAT-01 | Simplify Catalogue; retain SuperAdmin roll-group editing in the per-drop rate panel; retire separate roll-group/import surfaces | AU21/AU23 backend delivered in B4 remediation; the decided rate panel permits SuperAdmin editing and is read-only for other roles |
| EVT-01 | Name/timezone creation, permanent slug, atomic defaults, Identity ownership | AU03/AU08/AU09 complete; new UI binding pending |
| ACC-01 | Existing-account support/security; no user creation/merge or emergency controls | AU22/AU24 pending; typed ownership confirmation is an existing requirement gap |
| EVD-01 | Real team-role submission authority, immediate participant account switch | AU17a/D11 retained Playing correction backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC07); no roster changes |
| LIF-01 | Timestamp-led scheduling, phase-aware manual controls, +30m early-end grace | AU01/AU10 complete; RC01/RC03 and reference binding pending |
| TEM-01 | Existing website accounts, IncludedInDraft; retire accountless/CSV/owner-transfer | New team reference omits affiliation/image controls; old data preserved |
| EVD-02 | Ordinary later attempts, binary review, reasoned correction/reversal | AU17 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC07); preserve original evidence and separate approval |
| SGN-01 | Mutable pre-draft questions, optional-after-first-response, protected first Playing | AU05–AU07 complete; RC02/UI binding pending |
| DRF-01 | 2+ team draft, 0/1 manual roster; no pause/finalized reopen; cancel only zero picks | AU14 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC04); click-pick, compact board and stay-on-Teams UI accepted |
| SGN-02 | Admission code, always-enabled waiting, capacity rules and normal promotion | F01–F04 add explicit selected-person +1 exceptions; backend complete |
| BRD-01 | Board leases/snapshots, approval/publication and evidence locks | AU11 manual total EHB override, AU13 estimate; AU19 backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC05) |
| PAR-01 | Pre-draft correction, withdrawal/restore, payment/notes | F01–F06 backend complete; new UI binding pending |
| RES-01 | Mandatory gates, exact ties, immutable publication directly to Archived | AU12 new-events-only ranking and AU18/RC08 pending |
| ROS-01 | Separate finalized pre-first-Live Add/Remove; permanently fixed Live roster | Later reference retains Live role changes, not membership changes |
| BNR-01 | Retire event banners with guarded cleanup/history policy | Baseline migration/code present; production disposal not claimed |
| WOM-01 | Website-owned dates; provenance separate from credentials; external never deleted | AU15 normal Fetch and AU20 exact UTC windows/code-bearing detach implemented; AU20 recheck pending |
| ACT-01 | Actions derived from current pending reviews/scheduled failures | AU04 directory data complete; no vacancy/follow-up/missing-Captain revival |
| BRD-02 | Lightweight Board reads, targeted estimate freshness | AU19 projections backend implemented, remediation round 2 done, Claude recheck pending, binding pending (RC05); calculator and approved snapshots preserved |
| WOM-02 | Dedicated WOM operations, Overview summary/link | AU20 implemented/remediated, recheck pending; RC09 pending; no user-scheduled creation, normal Create starts now |
| VER-01 | Integrated proof and bounded remediation | Historical evidence linked above; reviewer evaluates entire base-to-candidate diff |

Current routing is in AGENTS.md, not the original wave/coordinator/model prose.
All later unfinished AU/RC scopes remain listed in this plan. No implementation
resumes during documentation reconciliation or review preparation.

## Drop announcements and NEW tracking — approved behaviour, 2026-09-12

Historical delivery context: completed-pass instructions/model assignments below
do not authorize new work or override current product/contracts/UI authorities.
Current stop boundaries and routing are above and in AGENTS.md.

Status: implementation authorized by the user after the single independent readiness
review and resolution of its named lifecycle decision. Execute the three bounded
passes below, then focused independent review, remediation and acceptance preflight.
Commit, push, merge and deployment are not authorized.
Checkout: `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`,
base `c165bbcb321547637d03b4e9dc3d2e206e5944b3` from `origin/main`.
This section owns scope, pass order, complexity limits and acceptance. The active
journey is `PUB-UPDATES-01` in FUNCTIONAL_CONTRACTS; UI_SYSTEM owns countdown semantics.

### Approved outcome and presentation

Replace the generic public Board/TeamBoard progress-refresh notice with one live
announcement queue. Approving a submission creates an eligible update; pending,
rejected and reversed evidence never appears as current approved progress. Each
approval produces one announcement, not a second completion announcement.

- Only authenticated website accounts actually participating in the current event
  receive announcements or personalized NEW state. Admin privilege alone does not
  qualify. Eligibility is enforced server-side, not by hiding markup. Reuse existing
  account/membership authority; the mapping is recorded in the readiness outcome below.
  Delivery is event-wide: every eligible participant receives eligible approvals from
  every team/player in that event, not only evidence credited to their own account.
- Eligibility continues through Live and AwaitingFinalReview. Finalizing the event
  clears the banner queue, all per-entry Drops NEW marks and the DROPS navigation
  badge for every account, including offline accounts, and stops further announcements.
  This clears update/NEW state, not the actual approved feed entries or evidence.
  Connected pages reconcile immediately after committed finalization; returning or
  reconnecting clients observe the same cleared state. Stale reads/actions must not
  resurrect cleared updates, including if the existing Unfinalize workflow is used.
- Display throughout non-Admin pages, including the landing/account pages. Admin
  hides the banner without acknowledging it. Navigation preserves queue, selected
  approval, expanded/compact state and remaining cooldown, without replaying entry.
- There is at most one active scheduled event. Do not introduce multi-event queues
  or event-selection UI. Preserve the existing non-overlap scheduling rules.
- User-approved standalone visual/motion reference:
  `/Users/christopher/Documents/Codex/2026-09-11/drop-announcement-prototype/outputs/drop-announcement-prototype.html`.
  Reuse its accepted expanded/compact shapes, entrance/exit and directional switching;
  integrate existing site fonts, tokens, themes, localization and accessibility.
  Prototype demo controls, fake data and stand-in board are not production scope.
- Main title/artwork: achieved item for a drop; frozen tile name and tile artwork
  for a non-drop objective. Never use an evidence/submission screenshot in the banner.
  Missing-artwork handling,2026-09-14: try item then distinct tile artwork; if absent
  or failed, render text-only without an empty thumbnail frame/reserved column. Preserve
  banner dimensions, copy styling and all interactions; no invented placeholder icon.
- Kicker is `New tile progression: {progressAfter} / {target}` (localized English/Danish),
  using the existing announcement progress/target values; `Tile completed` stays
  counter-free when this approval completes
  that team's tile. Completion has green status styling; progression is coral.
  Completion must describe this approval's effect, not merely today's tile state.
- The container's thin top border remains a separate coral countdown indicator.
  It starts full and drains over ten seconds to compaction. Focus/interacting inside
  resets it to full and holds it there; clicking/tabbing away starts a fresh ten
  seconds. Moving between controls inside must not restart a running countdown.
  Preserve hover protection from the prototype, using the same reset/hold rule;
  countdown starts only when neither hover nor focus remains. No auto-focus on arrival.
  Respect reduced motion and keep all actions keyboard/touch reachable. The local
  countdown has no per-tick server writes and is distinct from the two-minute cooldown.
- Compact label is `N NEW UPDATES` (localized singular/plural), with Expand and dismiss.
  Previous/next is manual, updates artwork/title/player/team/action together, has
  disabled endpoints and is omitted with the counter for a single entry.

### Approved prototype restoration — 2026-09-13

The user approved correcting every presentation/motion difference reported by the
direct prototype comparison, with one exception: retain the integrated X hover
(blue icon without the prototype's soft background). Use the exact prototype above
as implementation source, reusing its markup, scoped CSS/keyframes and animation
sequencing wherever possible; do not recreate an approximation.

- Restore staged 320ms entrance, delayed animated dismissal for both shapes, measured
  640ms expanded/compact height transitions with coordinated 320ms content phases,
  sequential directional slide exit/entrance and original easing/cancellation handling.
- Restore square switching controls, styled/centred non-wrapping counter and original
  counter placement; remove the duplicate counter beside the kicker. Restore prototype
  narrow breakpoint/spacing/control sizes, border contrast, shadow and artwork framing.
  Preserve only the current X hover exception; other focus/geometry follows the reference.
- Keep the approved coral ten-second focus/hover-held countdown, green completion
  status, live queue/acknowledgements/NEW state, persisted cooldown, navigation and
  submission protection. The expanded countdown must replace the static coral edge
  rather than drain over an unchanged coral border. The user subsequently approved a
  thin neutral top outline behind that countdown, matching the other outer edges, so
  the normal outline remains visible as the coral retracts. Retain header stacking level 1101,
  real Drops anchor destinations, localization, theme support and reduced-motion behavior.
- User-reported shared-shell correction: outside Board/views, the banner background
  and specifically its top edge are transparent. Resolve the colour-token scope so
  the opaque surface and coral countdown/top edge render on all non-Admin pages in
  both themes; do not depend on Board-only variables or create a new theme framework.
  Follow-up manual correction: preserve the now-accepted surface/outline and extend
  Board colour parity to all remaining banner text, controls and status colours.
  Replace misaligned font-based navigation chevrons with centred inline SVGs while
  preserving control sizes, accessibility and motion.
- Scope is the shared banner partial, its CSS/JavaScript and focused client checks.
  No backend, database, feed redesign, seed/reset, package or new framework changes.
  Verify timing/interruption behavior with the existing focused runnable checks,
  compile the Web project if Razor changes, then perform one scoped independent
  prototype/behavior comparison. Ordinary browser preflight remains waived; visual
  acceptance belongs to the user. No commit, push or deployment is authorized.

### Queue, cooldown and delivery

- First eligible arrival opens expanded when the conditions below allow it. New arrivals append without stealing the
  current selection, resetting the ten-second countdown, or extending the cooldown.
- Automatic expansion atomically starts a two-minute cooldown per account/event.
  Dismissal starts that same cooldown again. Save it server-side so reopening,
  refreshing, multiple tabs and other devices cannot bypass it. A simultaneous
  expansion claim must not yield multiple automatic expansions for that account.
- During cooldown, arrivals update the existing banner; if dismissed, new arrivals
  show compact. Expiry alone causes no display change. The next approval after
  expiry may expand showing the new approval; protect active interaction/submission
  and submission-result states from interruption. Manual Expand remains available.
- Automatic expansion requires both an expired cooldown and an outstanding eligible
  approval newer than the account/event's last automatic-expansion approval boundary.
  Atomically persist that boundary with the cooldown, covering only the claimed
  snapshot so concurrent later approvals remain eligible. Navigation, refresh and
  returning after a long absence use this same rule; elapsed time alone is insufficient.
  Expiry itself has no timer-driven expansion. Offline approvals remain collected.
- A successful automatic expansion selects the newest approval in its claimed queue.
  Manual expansion restores the last available selection; arrivals while already
  expanded never steal selection. Deterministic ordering uses approval chronology
  and a stable tie-break. Acknowledged or reversed items cannot qualify for expansion.
- Acknowledged announcements never return; this requires durable per-account/event
  state. It must survive connection loss, out-of-order or duplicate invalidations,
  stale tabs and action retries without losing later approvals.
- Remove a reversed approval from banner and NEW eligibility. The existing immutable
  submission rule remains: a reversed attempt cannot be directly reapproved. An
  approved linked corrected attempt is a new eligible update with its own identity.
- Do not change competitive progress, evidence history or approval/reversal semantics.
  The current event is in signup; deployment must not mark future approvals as seen.
  Do not add a historical backfill workflow. Readiness must establish a deterministic
  initial tracking boundary for existing Development approvals and later participants.

Readiness initialization proposal: establish a stable per-event tracking start at
feature deployment for existing events and creation for later events, before any
participant's first visit; combine with the current membership join boundary. Do not
initialize at first login, which would lose offline approvals. Existing-event history
before rollout stays outside tracking; the actual current event is in signup, so all
its future eligible approvals are captured. Use a persisted boundary, not a mutable
client timestamp. This additive initialization never changes competitive history.

### Two acknowledgement states and Drops integration

2026-09-13 manual live-feed regression: an approval invalidation must preserve
existing timestamps and use actual approval age for newly inserted entries, matching
normal Drops rendering. Historical unloaded entries must not be promoted into the
newest group merely because an invalidation fetched them. Preserve filter/paging,
chronology/groups, scroll, open popup and acknowledgement semantics. Scope is the
existing live feed rendering and necessary timestamp transport plus a focused
regression check; no redesign, new timer/service/dependency or approval-rule change.

2026-09-13 direct manual correction: Danish progression wording is `Nyt tile fremskridt:
{0} / {1}`; retain the English term `Leaderboards` in Danish UI and its help reference.
After successful CLEAR ALL NEW, show a localized confirmation through the existing
shared toast owner. A failed request must not show success; use existing error toast
feedback. Preserve acknowledgement semantics, NEW presentation and all banner motion.

2026-09-13 authorized follow-up scope: implement the automatic-expansion boundary
and selection rules above, and complete English/Danish announcement wording,
accessible labels, dynamic counters and related NEW/CLEAR ALL NEW text through the
existing localization system. Preserve approved visuals, motion, ten-second timer,
acknowledgements, eligibility and submission protection. Budget: one scalar on the
existing account/event state plus a consistent additive migration; no new table,
service, route, job or dependency. Use focused client/persistence/localization checks
and independent scope review. NEW mark design and clearing-behavior review belongs
to the user's next pass; do not change those behaviors now. Ordinary browser
preflight remains waived; no reset, commit, push or deployment is authorized.

| Action | Banner acknowledgement | Drops NEW acknowledgement |
| --- | --- | --- |
| Dismiss | All approvals in the displayed queue snapshot | None |
| GO TO DROP, popup successfully opens | That approval | That approval |
| Open that popup directly in Drops | That approval | That approval |
| CLEAR ALL NEW | All approvals covered by that action | All current NEW approvals in this event |
| Event finalization | Entire event queue for every account | All event NEW marks and navigation badge for every account |

Merely navigating to Drops, compacting, waiting or switching banner entries does not
acknowledge either state. Failed navigation/popup load must not acknowledge viewing.
Opening the popup is the view boundary, not closing it or viewing every image pixel.
GO TO DROP goes to the current approval's event Drops view and opens its existing
evidence popup, even if the entry is outside the initial 25/filter result. On success,
select the next queued item and compact, or hide the banner if the queue is empty.
Never silently clear other entries when viewing one. CLEAR ALL NEW applies across
filters/pagination; approvals after its server-defined snapshot remain new. Mutations
must be authenticated, ownership-scoped, anti-forgery protected and idempotent.

Add NEW to individual entries, CLEAR ALL NEW to the existing Drops view, and a small
coral NEW beside DROPS in the event navigation whenever any eligible update is new.
The navigation badge follows Drops NEW state, not banner dismissal. These states
synchronize across visits/devices; no personal-notification read state is reused.

Approvals update the visible banner and navigation badge live without navigation.
When Drops is displayed, insert eligible entries into the current feed live, preserving
scroll/reading position, filters and any open popup. Filter-excluded arrivals still
update the banner and unfiltered NEW state. Reversal removes stale approved entries
without destroying unrelated popup/submission state. No automatic full-page reload,
board redraw/reset or interruption of tile/sidebar/captain interactions.

### Technical boundaries and candidate complexity budget

This is the proposal for readiness to validate, not permission to add infrastructure.
Keep Web -> Application -> Domain/Infrastructure and PostgreSQL authoritative. Reuse
SignalR as a non-authoritative invalidation channel; rejoin and reconcile on reconnect.
Do not put recipient state/private evidence into anonymous event groups. Existing
generic public subscriptions may remain where other protected behaviour needs them.

- Zero new packages, jobs, brokers, Redis instances, replicas or general event-bus/
  notification frameworks. Zero new rendered pages, global roles or policies.
- Candidate upper bound: two small tables, account/event state (cooldown and bulk
  boundaries) and sparse account/approval acknowledgements (independent banner/NEW
  state). Prefer reusing compatible persistence when it preserves account ownership
  and concurrent bulk/view semantics. Do not fan out one row per participant on every
  approval. No new approval-history table unless readiness demonstrates necessity.
- At most one focused application contract/infrastructure service for querying and
  mutating update state; reuse board projection/membership/evidence code. Extend the
  existing hub, shell and Drops handlers. Budget up to one focused authenticated
  endpoint family for bounded updates/acknowledgement/cooldown claims and one explicit
  submission deep-link parameter on the existing Drops route. No separate drop page.
- Add only columns/indexes needed for deterministic incremental reads, completion
  facts or acknowledgement concurrency. Readiness must identify any required approval
  snapshot field and migration. Additive migrations must include designer/snapshot;
  never rewrite retained competitive facts to initialize tracking. If a preflight can
  fail closed, document exact operator diagnosis/correction/retry before implementation.
- Readiness identified a required immutable completion-at-approval fact on Submission,
  computed inside the owning approval transaction across all tile requirements. Budget
  this small field plus an event tracking-start boundary within the additive migration.
  Reuse existing frozen tile/item identifiers and public artwork routes; no new image
  assets or artwork storage system. Do not infer completion from current/rebalanced
  progress or mutate old submissions to pretend their completion fact was captured.
- Account/event and account/approval acknowledgement keys must be unique and concurrency
  protected. `(ReviewedAt, SubmissionId)` supplies deterministic display order, but is
  not by itself proof against late transaction commits. Bounded reconciliation and
  clear/dismiss snapshots must retain approvals that commit after a read/clear boundary.
  Pass-2 support uses a stable committed approval ordinal within the existing event-
  locked approval transaction, exposed as a queue snapshot boundary for paging/dismissal.
  This fits the already budgeted cursor/concurrency columns and avoids storing a new
  server snapshot on every read or sending the full queue as IDs. Dismissal covers the
  represented queue across pages; later ordinal approvals remain eligible. Emit the
  existing generic invalidation after committed finalization so connected pages clear.
- Client: one shared announcement partial/module using native CSS/JS; extend current
  feed/evidence scripts, shared navigation and existing stylesheet/localization.
  No parallel dialog or toast framework. Remove the replaced generic refresh notice
  and its obsolete client hooks/tests where no remaining consumer requires them.

Verified starting inventory: `PublicRecentDrop` already has SubmissionId/ApprovedAt,
tile/drop names, ProgressAfter/Target; `PublicBoardService` builds it through an expensive
full-board projection. `ProgressHub.WatchEvent` currently allows anonymous subscriptions
to non-hidden events; `SignalRProgressNotifier` sends generic `progressChanged` without
drop data. `public-progress.js` reveals a manual-refresh notice. `public-recent-drops.js`
fetches full-page HTML for filters/load-more. `public-evidence.js` opens a dialog from
rendered trigger data. Extend these seams without assuming generic invalidation means
new approval, or that a requested submission is in the rendered first page.

Performance: use bounded small authoritative reads/updates, coalesce approval bursts
and avoid full-board/page requests for each connected client per approval. Countdown
animation is local. Do not query/write continuously while idle. The approved baseline
in the production capacity sub-gate is 100 SignalR viewers and 400 successful public
requests on 2-vCPU/4-GB; user explicitly waived an additional load-test gate for this
slice. Reassess performance after the event using observed evidence if necessary.
Focused correctness/security/recovery checks remain required.

### Ordered passes and acceptance journeys

Execution checkpoint: all three Luna High implementation passes are complete.
Domain tests 3/3, PostgreSQL persistence tests 4/4 (including >100 queue snapshot),
real HTTP login/CSRF/account-scope test 1/1 and Development reset/controlled non-drop
fixture test 1/1 passed. Migration consistency, Web/Infrastructure builds and focused
Node countdown/state checks pass. Full-diff Terra High review and named-fixes recheck
are clear after correcting event-wide recipients, NEW-label removal and submission
interaction protection (including a deferred claim-response race). Final focused
PostgreSQL integration is 6/6; Release build and scoped formatting pass. Ordinary
browser navigation/popup/feed checks and visual acceptance remain with the user under
the verification change below.

**User-directed verification change — 2026-09-12:** Skip the ordinary agent browser/
manual-acceptance preflight unless a check is difficult for the user to reproduce.
The user will perform ordinary navigation, popup, feed and visual checks. Retain the
passing automated authorization, persistence, concurrency and deferred-response race
proof; do not rerun it merely because the manual preflight is waived. A concrete
technical blocker already observed may be diagnosed and corrected before handoff.
This waives the ordinary preflight gate for this slice, not user visual acceptance.

**Development seed extension approved — 2026-09-13:** The user requested 10–20
pending submissions in the Live seed to test announcements, including submissions
that complete tiles. Seed `test-15-dkl-live` with 15 total pending submissions across
varied tiles, including at least three whole-tile finishing approvals and some partial
progress. Retain the DA-07 non-drop finisher, existing approved progress and existing
rejected/withdrawn/reversed/linked-correction cases. This expands only Development
test data and its focused verification; announcement rules, UI and production data
are unchanged. The existing reset command regenerates the batch; do not reset a
user's in-progress manual-test database without authorization.

One Terra High read-only readiness review covers the entire proposal before workers
start. It must resolve source contradictions, concrete budget, minimum Development
seed reachability, removed-behaviour consumers, order and independent deployability.
No repeated planning review absent a newly discovered product contradiction. Proposed
implementation passes (Luna High under the supplied current role instructions):

1. Durable acknowledgement/cooldown/query contract and additive migration; approval
   identity/completion, eligibility, finalization clearing and transaction/concurrency
   protections. Reuse the existing finalization authority; no new lifecycle framework.
2. Shared non-Admin banner, SignalR reconciliation, timer/motion and navigation state.
3. Drops deep-link popup, NEW rows/nav badge, CLEAR ALL NEW and bounded live feed.
   Final integration, focused independent review and manual-acceptance preflight.

The passes are ordered for implementation, not individually approved releases. The
user has authorized the complete contracted implementation; assign one concrete pass
at a time, verify its handoff and continue in order. Stop for a real product/budget
conflict or after integration/review/preflight for user visual acceptance. No worker
may independently start the next pass or package/release the feature.

| Journey / starting state and real entry | Required result | Pass / smallest proof |
| --- | --- | --- |
| Eligible seeded participant opens public event through Events; Admin approves a pending item in Review | Live banner/item art, Drops badge, no board reset; private/nonparticipant clients excluded | 1-2; authority + one approval-delivery scenario |
| Same participant, five approvals across the 120-second boundary; click/keyboard focus inside then away | Stable selection during burst/interaction; top line refills/holds then drains over a fresh 10 seconds; eligible later arrival expands | 2; client fake-clock state-machine check + manual motion |
| Navigate Board -> Drops/Teams -> landing -> Admin -> public; close/reopen and use second device/session | State retained/hidden appropriately; one account cooldown; offline backlog restored newest first | 1-2; shared-state concurrency/reconnect scenario + manual navigation |
| Dismiss queue; approve another item; reopen site | Old banner entries absent; their Drops NEW persists; later approval eligible without bypassing cooldown | 1-3; snapshot acknowledgement/retry test |
| GO TO DROP from landing for an item outside loaded/filter results; then open another item through Drops UI | Correct existing popup opens; only successful view clears both states; next banner item compact; failure preserves NEW | 3; authenticated rendered-navigation/popup test |
| Open Drops, apply real filters/load-more, receive approval; CLEAR ALL NEW races with next approval | Stable reading/popup context; correct filtered insertion and unfiltered NEW; concurrent later approval remains new | 1/3; bounded feed + transactional clear test |
| Admin approves non-drop progress and tile-completing evidence; then reverses through Review | Tile title/art, correct completion status; reversal removes current update; approved linked correction is new | 1-3; completion/reversal identity case + manual themes |
| Event timer ends with final reviews outstanding; approve in Review, then finalize through Admin | Reviews still announce until finalization; finalization clears all banner/NEW/nav state online and on return, preserving the feed and evidence | 1-3; finalization/reconciliation race case + manual lifecycle |

Development reset: reuse existing `DevelopmentScenarioSeeder`, seeded Admin and genuine
participant/captain roles plus a Live event, item and non-item tiles, pending evidence,
and an approval one contribution short of completion. Readiness must name exact existing
fixtures/routes and the minimum missing data; no broad demo dataset or production reset.
Carry final reachable steps into MANUAL_TEST_CHECKLIST after readiness corrections.
Manual evidence covers desktop/narrow, light/dark, pointer/keyboard/touch and reduced
motion. Automated proof concentrates on real authorization, persistence, concurrency,
delivery/reconnect and popup success/failure seams; no redundant per-layer coverage.

Non-goals: wider Drops redesign, browser-title personal-notification counts, scheduling
changes, anonymous tracking, cross-event announcement UI, evidence/review policy changes,
automatic board refresh, historical backfill project, sweep tickets/C05/C09 and artwork
work in other checkouts. Stop and report before a material rule/budget change or unrelated
fix. Optional readiness suggestions do not expand scope. No package/push/deploy authority.

### Independent readiness outcome — 2026-09-12

One Terra High read-only review completed against the complete contract and scoped
source. No implementation or runtime tests were performed. No second planning review
is required after recording the named decision/corrections below.

**Resolved product decision — 2026-09-12:** User approved continuing announcements
through final review and specified that finalization clears everything in both banner
and Drops NEW state, including the navigation badge. `EventStatePolicy.ReviewEvidence`
allows Live and AwaitingFinalReview. The completion boundary is committed finalization,
not the scheduled end time, an Admin page visit, or a participant visiting Drops.
`EventLifecycleService` already enforces one current non-hidden Live/review/finalized
event during start/resume (Development fixtures excepted); preserve this instead of
adding event-selection or multi-event announcement logic.

**Recipient mapping:** reuse current website-account/participant/team membership
relations, requiring an enabled WebsiteAccount, Confirmed participant, current team
membership and active team in the non-hidden event. Waiting-list, withdrawn/former
members, anonymous viewers and Admin-only authority do not qualify. This is a new
recipient projection; do not change EvidenceAuthority's existing role behaviour.

**Accepted technical corrections:** independent banner/NEW acknowledgement needs the
two small tables already budgeted, uniqueness and concurrency protection; no recipient
fan-out. Persist the two-minute cooldown atomically. Add a small completion-at-approval
fact; resolve artwork through existing snapshot identities/routes. Readiness proposed
the initialization boundary above; implementation must establish the exact safe
additive migration/seed initialization without backfilling old competitive facts.

GO TO DROP needs a NEW optional submission-id query parameter on the existing Board
Drops route (already in budget); BoardModel currently has no such parameter. Resolve
that one authorized entry separately from filters/initial 25 and reuse public-evidence
dialog transport. Acknowledge only after actual successful popup opening, not merely
fetching its payload. Full-board projection is not the live-update query.

Replacement inventory: Board/TeamBoard `data-progress-update` markup and
`public-progress.js` refresh hooks, affected browser tests, and HowTo's `Refresh now`
copy/localization. Preserve `notification-inbox.js`'s independent generic
`progressChanged` consumer. No domain enum/removal or approval-history rewrite.

Seed/preflight basis: `DevelopmentScenarioSeeder`'s `test-15-dkl-live` has a published
Live board, linked participant/captain/co-captain accounts, approved/partial progress,
pending/reversed/corrected review cases, and a waiting-list negative-role fixture.
Reuse Events -> that event -> Board -> Drops and Admin -> Review with that event.
Add only one deterministic pending contribution that completes a known tile; pin a
non-drop objective from existing fixtures or add only the missing single objective
needed for DA-07. Test data must also initialize tracking deliberately so seeded
historical approvals don't swamp the first-arrival journey. Exact participant handles
and fixture ids belong in the implementation preflight, not guessed links here.

Pass order stands. Backend/shell passes are additive implementation stages, but neither
is a complete independently deployable feature; pass 3 is the first complete release
candidate. Existing capacity evidence accepted; no new load-test gate. Manual steps
DA-01..09 are recorded in MANUAL_TEST_CHECKLIST but are not yet runnable acceptance.

## 1. Documentation consolidation

Historical delivery context: completed-pass instructions/model assignments below
do not authorize new work or override current product/contracts/UI authorities.
Current stop boundaries and routing are above and in AGENTS.md.

1. **UI authority pass — complete:** `UI_SYSTEM.md` holds active global UI
   rules and exact implementation ownership; `UI_PAGE_MATRIX.md` holds active
   page families, protected composition, exceptions, approvals, and gates. The
   old Admin contract and UI roadmap are preserved as exact archive copies
   behind root tombstones.
2. **Workflow authority pass — complete:** `FUNCTIONAL_CONTRACTS.md` holds
   final end-to-end journeys, actors, reachability, authority handoffs,
   failure/recovery behavior, and acceptance outcomes. The old
   `FUNCTIONAL_WORKFLOWS.md` is a non-authoritative root tombstone and its exact
   source is preserved under `docs/archive/`.
3. **Archive promotion — complete (2026-08-15):** the superseded
   implementation roadmap, completed Slice 1–10 implementation plans, and
   Slice 1–3 manual result records were preserved as exact working-tree copies
   under `docs/archive/`. Their source paths, archive paths, SHA-256 values,
   source working-tree/base identification, and current destinations are
   recorded in [`docs/archive/INDEX.md`](docs/archive/INDEX.md). No durable
   rule was missing from the active authorities, and no candidate root files
   remain.
4. **Core-document reconciliation — F-05 subpass complete (2026-08-15):**
   active notification terminology, persistence shape, privacy/read-state,
   destination, authority, and selective idempotency wording was reconciled
   against accepted code. This was documentation-only; no product decision or
   implementation change remains.
5. **Application Atlas retirement and durable-finding routing — complete
   (2026-08-15):** the exact Markdown/HTML working-tree bytes are preserved in
   `docs/archive/superseded-assessments/`, all durable items are either routed
   to an active owner or explicitly archive-only, and F-04/F-06 are resolved.
   No broader compression or further archive promotion is
   included.
6. **Active core-document boundary reconciliation — complete (2026-08-15):**
   active authority boundaries, stale status framing, and cross-document
   routing were reconciled without changing product/UI behavior or promoting
   archive material. F-04, F-05, and F-06 are resolved.
7. **Replacement-link/content verification — complete (2026-08-15):** 15
   active root Markdown files and 62 local links were checked with no broken
   targets or anchors; 20 archived files/hashes match
   `docs/archive/INDEX.md`; 3 root tombstones are short, rule-free,
   non-authoritative, and correctly linked; and the manual checklist has 24
   headings and 180 checkbox items with a valid archive-evidence link. No stale
   retired-root links, stale phrases, duplicate authority entries, or
   Atlas-as-active wording remain. Documentation consolidation is complete;
   `git diff --check` passed for the documentation/archive changes. The current
   UI order is owned by section 2 below. No release-readiness claim is included.

8. **Hidden-event quarantine implementation — complete, manually accepted, and
   included in the deployed PR #5 candidate (2026-08-31):** migration
   `20260831142836_AddEventQuarantine` is included in the production candidate;
   the affected Web Release build, domain quarantine (10),
   destination policy (20), quarantine integration (initially 2, then focused
   remediation suite 6), migration rehearsal (1), architecture, Bash syntax,
   and `git diff --check` gates passed. An independent Sol High review found
   four initial blockers—Hide reachability/rendering, emergency-credential
   access/audit, realtime access/invalidation, and legacy notification
   backfill/index—which focused Luna xhigh remediation closed. Follow-up review
   closed two migration-only emergency-audit classification issues; final
   independent closure verdict is PASS. The user manually accepted the bounded
   rendered navigation and Hide/Restore journeys after that remediation.

   The contract remains limited to `AwaitingFinalReview`, `Finalized`, and
   `Archived` eligibility, the separated SuperAdmin Hidden area/limited Manage
   inspection, and fail-closed ordinary paths. The production rehearsal event
   remains Hidden and is reachable only to SuperAdmin through
   `/Admin/Events?filter=hidden`; the separate historical import succeeded on
   2026-09-01. The accepted manual and automated whole-application regression
   and production launch are recorded below.

## 2. Launch and UI order

Historical delivery context: completed-pass instructions/model assignments below
do not authorize new work or override current product/contracts/UI authorities.
Current stop boundaries and routing are above and in AGENTS.md.

The user approved a complete Public UI identity replacement on 2026-08-22.
The public Board ecosystem's accepted behavior remains protected, except for
the user-approved 2026-08-24 correction that makes team cards navigate to the
ordinary team-board page rather than opening a team-board popup. Its old visual
identity is superseded and is reopened only for the ordered structural migration
below. Admin is outside this experiment. The frozen pass order is:

The user-authorized bounded masthead normalization prerequisite for the existing
left-side text roles in Signup create/edit, Signups, Teams, the three Board
views (View Bingo, Recent Drops, and Leaderboards), and Captain operations/detail
was completed before the accepted Board and canonical submission work. Use Privacy as the shared
implementation authority for kicker/title/support typography, `.65rem` internal
stack rhythm, and the ordinary versus Board-only reduced shell gap. Preserve
each family's right-side additions, metadata, artwork, rails, actions, and lower
geometry. This normalization did not change the frozen pass order or grant
manual approval; page-specific approvals remain authoritative.

1. **Pass 0 — authority and non-breaking foundation:** reconcile the active
   public visual authorities and define the reusable light/dark tokens, theme
   hook, typography, stable family layouts, outline controls, focus, motion,
   and stylesheet ownership. Do not globally alter an unmigrated family's
   geometry.
2. **Pass 1A — atomic landing rewrite, complete and accepted 2026-08-22:**
   replaced `/` presentation markup and
   responsive composition structurally from the approved landing reference.
   Preserve the landing PageModel, event projections, destinations, standalone
   Login navigation and validated local `ReturnUrl`, localization, and routes;
   do not preserve legacy/generic public
   Razor composition, old containers, generic component geometry, typography,
   links, buttons, icons, or responsive CSS. Static landing editorial copy is
   composition-flexible: it may be rewritten, shortened, reordered, or replaced
   in natural English and Danish to serve the approved hierarchy, rhythm, and
   line lengths. Preserve product meaning and CTA destination/action semantics;
   dynamic event facts and backend behavior remain frozen. The user manually
   accepted the final landing result.
3. **Pass 1B — launch-critical signup/auth, complete and accepted:** Signup,
   Confirmation, Login, Onboarding, AccessDenied, Error, and StatusCode are
   complete from their approved authentication/status contracts. Preserve their
   accepted routes, handlers, localization, and standalone Login navigation.
4. **Pass 2 — account and public utilities, complete and accepted:** Settings,
   Change Password, My Accounts, My Events, Forgot/Reset Password, Setup,
   Notifications, and Privacy are complete. The `/HowTo` guide was subsequently
   implemented and approved in `50077fd`; preserve its five-step content and
   ordinary anchor fallback.
5. **Pass 3 — roster/event family, complete and accepted:** exact-link Signups and
   Teams/roster pages,
   including phase-safe, privacy-safe, empty, and permission states. The user
   approved PUB-REF-16 for Teams/roster on 2026-08-24: use a left title/back/time
   masthead with the existing Landing-family diagonal DK artwork integrated on
   the right; omit team images and role icons; distinguish Captain/Co-captain by
   text color; group teams with whitespace rather than an inter-team divider
   grid; and show two draft picks per row at large widths. The generated image's
   uneven spacing and detached-looking mark are directional artifacts rather
   than pixel targets. This reference approval does not itself authorize Pass 3
   implementation.
6. **Pass 4 — public Board ecosystem, complete and accepted:** the structural
   presentation rewrite of the Board masthead, the ordinary TeamBoard page, Tile/sidebar, approved
   Evidence/lightbox, and the shared submission drawer. PUB-REF-02,
   PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15 are explicitly reactivated
   targets for this pass; current user screenshots are rejection evidence only.
   Team cards navigate normally to the existing TeamBoard route at every
   viewport; remove the popup interception and popup-only restoration behavior
   without creating a new route or navigation framework. Preserve every
   board/team/tile route, ordinary history, focus, tile-sidebar, submission,
   evidence, authorization, and realtime contract. The current team-overview
   grid beneath the masthead is explicitly protected as already near target;
   preserve it and add the missing PUB-REF-02 Recent Activity footer rather
   than rewriting that grid. The shared drawer remains
   the only participant/Captain submission interface. Light composition is
   canonical; dark changes tokens only and must not change dimensions,
   placement, tile geometry, or outlined tile numbers. A legacy-composition
   reskin does not satisfy this pass.

   Pass 4 is implemented in three bounded subpasses:

   - **4A — Board masthead and overview completion:** structurally replace the
     rejected equal-panel masthead from PUB-REF-02, preserve the current
     near-target team-overview grid exactly except for required masthead
     integration, and add the missing real Recent Activity footer beneath the
     grid. The Board overview uses the shared 88rem Wide page width and normal
     responsive gutter; this does not copy Landing hero height or artwork. Do
     not change team navigation,
     TeamBoard, Tile, submission, Recent Drops, or Leaderboards in this subpass.
   - **4B — ordinary TeamBoard workspace:** atomically change team-card
     navigation to the ordinary TeamBoard page, remove popup interception and
     restoration behavior, rebuild TeamBoard from PUB-REF-03, and retain/adapt
     the real tile-rail, shared submission drawer, evidence viewer, focus,
     history, and realtime behavior. The popup must not be retired in a separate
     earlier change that leaves those interactions without an owner.
   - **4C — secondary Board views:** structurally reconcile Recent Drops,
     Leaderboards, their empty/filter/narrow states, and shared evidence
     presentation against PUB-REF-04, PUB-REF-14, and PUB-REF-15 without data,
     query, service, Admin Preview, overview-grid, or TeamBoard behavior changes.
7. **Pass 5 — canonical submission workspace consolidation, complete:** the
   Captain and participant submission workspaces are consolidated into one
   implementation owned by `/Submissions` and `/Submissions/{id:guid}`. The
   accepted implementation preserves the complete retained ledger, Captain-only
   focus/status sections, server-authorized role boundaries, canonical
   notification destinations, compatibility aliases, drawer transport, and
   Admin authority. It was independently reviewed, remediated, manually
   accepted, and committed in `88cd8f8d6014e947e2a5e97717be460ca2ea9d66`.
8. **Remaining Admin UI families** — deployment-ready implementation remains
   subject to the recorded page-specific manual-approval state, but that manual
   approval is not a gating task before whole-application regression or release.
   Currently unapproved Admin visual debt is non-blocking while functionality
   works; security, authorization, privacy/data-loss/data-integrity, and
   workflow-blocking defects may still interrupt launch-critical work.
9. **Dashboard/action inbox** — deployment-ready intentional shell-owned WIP
   presentation; it is not a gating manual-approval task and does not precede
   whole-application regression or release.
10. **Whole-application regression and production release gates — complete:**
    the user accepted manual whole-application regression based on sustained
    site use, and the recorded automated local regression passed. PR #5 was
    merged and deployed with passing CI, deployment, and focused production
    smoke. The planned Admin test event remains the real-world follow-up safety
    net and has not run.

### Pass 5 completion record — canonical submission workspace

**Approved outcome and completion — 2026-08-31.** This was an implementation
consolidation and routing/authorization correction, not a visual redesign. The
canonical overview is `/Submissions` and the canonical detail is
`/Submissions/{id:guid}`. The overview is team-wide for authorized current
members and includes retained rows credited to departed teammates. Captains and
co-captains see team focus and team submission status as the two Captain-only top
sections and retain server-authorized broader editing of eligible team
submissions. Ordinary participants do not see those sections and may edit only
their own eligible non-read-only submissions. The detail remains visually
equivalent to the approved Captain submission detail.

**Compatibility and destinations.** `/Captain` and
`/Captain/Submissions/{id:guid}` are thin compatibility redirects/aliases to the
canonical routes and never separate rendered implementations. Cross-role legacy
links must resolve through the canonical route and authoritative server
authorization. Personal submission/evidence notifications, including those
received by Captains/co-captains, resolve to `/Submissions/{id:guid}`. Relevant
general submission navigation resolves to `/Submissions`. Admin review
notifications remain `/Admin/Review/Details/{id}`. `/Captain/Submit/{tileId?}`
remains only the shared drawer transport/handler plus compatibility redirect.

**Protected boundaries and clarification.** The implementation did not redesign
accepted submission UI, add a second
workspace, weaken owner/team/captain authorization, alter retained-state
read-only or cutoff rules, change privacy/evidence-integrity/Admin authority,
change persistence or submission query semantics, add no-JavaScript-only parity,
or broaden into unrelated cleanup. The reported absence of a linked resubmission
from Admin Evidence Review was a reader/reviewer misinterpretation; source
inspection found no query exclusion. Existing behavior and tests remain
protected, and no query change or additional manual release gate is required.

**Verification and acceptance.** The implementation was independently reviewed,
remediated, manually accepted, and committed in
`88cd8f8d6014e947e2a5e97717be460ca2ea9d66`. Verification included Release
builds, 7/7 navigation/integration tests, notification workflow, UI assertions,
focus helper, ledger JavaScript, diff checks, independent review, and user
acceptance. The relative `_EvidenceUpload` partial 500 is fixed. The existing
linked-resubmission behavior and tests remain protected; the reported absence
from Admin Evidence Review was a reader/reviewer misinterpretation, not an
additional release gate.

**Complexity budget and stop rule.** The completed pass added zero tables,
migrations, jobs, NuGet dependencies, navigation frameworks, generalized
abstractions, or new rendered page families. Reuse the existing submission services, persistence,
drawer transport, notification model, policies, and approved detail composition;
the canonical route owner, compatibility aliases, role-conditioned sections, and
directly required focused tests/source fixes were the only additions in budget.
Future changes stop for user direction before changing any approved product
rule, persistence/query semantics, Admin authority, or this scope.

### Pass 1B implementation contract — signup, authentication, and status

**Approved outcome and references.** Rebuild the scoped public presentation
structurally from PUB-REF-05 (Signup), PUB-REF-06 (Confirmation), and PUB-REF-09
(Authentication/status). The accepted landing shell, typography roles, light and
dark token discipline, focus treatment, and restrained motion are reusable; the
accepted landing composition itself is frozen. The condensed PUB-REF-09 sheet
defines hierarchy and relationships, not literal panel dimensions.

**In scope.** `/Events/{slug}/Signup` create/edit, `/Events/{slug}/Confirmation`,
`/Account/Login`, the shared Login form partial, `/Account/Onboarding`,
`/Account/AccessDenied`, `/Error`, and
`/Errors/StatusCode`. Implement new family-owned Signup, Confirmation,
Authentication, and Status markup/primitives in `site.public-ui.css`; replace
the scoped pages' generic `public-ui-*` surface/masthead/data/action composition
rather than stacking another override layer over it. The standalone Login route
is the sole runtime login surface. Anonymous signup links navigate there with a
validated local `ReturnUrl`; the superseded route-backed login popup/dialog and
its exclusive runtime machinery are removed. Remove only superseded rules proven
to belong exclusively to these scoped pages.

**Frozen behavior.** Preserve every PageModel, handler, Razor route,
authorization boundary, validated local `ReturnUrl`, Discord callback/onboarding
state, login throttling and generic disclosure, password/remember-me semantics,
signup question and account binding, Wise Old Man fetch behavior, response
version, validation/error focus, capacity and queue outcome, edit/withdraw/rejoin
permissions, confirmation projection, and ordinary route/form fallback. Dynamic
event names, descriptions, dates, questions, answers, status, queue positions,
and account facts remain authoritative. Static copy may change only where the
reference composition needs it, without changing meaning, claims, or actions.

**Localization and states.** Every new public string is localized through the
existing English-default/Danish system. Any visible JavaScript value is passed
through rendered localized markup/data. Cover create/edit, unavailable and
cancelled signup, validation and lookup failure, confirmed/waiting/read-only
confirmation, Login validation, onboarding validation/lookup/expiry, 403, 404,
and 500/request-ID states. Light and dark use
identical geometry. Desktop, narrow/mobile, zoom/translation, keyboard/focus,
reduced motion, empty/error/permission states, and disabled controls must remain
usable.

**Reachability.** Development reset TEST 16 (`test-16-signup-lookup`) contains
six confirmed and three waiting participants at capacity six; `SeedAdminTwo`
provides the deterministic new waiting signup, edit, and Wise Old Man path. The
same account owns a confirmed read-only TEST 13 signup reachable through Account
→ My events. Signed-out landing/signup entry exercises Login with the real local
return path. Onboarding validation/expiry needs a real configured Discord
callback state; 403/404 use existing authorization/missing-route handling; 500
is an exception-handler state outside Development rather than a reset journey.
Do not add demonstration data solely to make these visual states convenient.
The landing must list TEST 16 as a public signup-open event even though it has no
published roster or board; its destination remains the existing Signup route.

**Complexity budget.** Zero new tables, migrations, services, routes, policies,
jobs, JavaScript frameworks, stylesheets, navigation systems, or generalized UI
abstractions. Use the existing pages, shared shell/dialog, localization, scripts,
and `site.public-ui.css`. The smallest directly affected resources and focused
tests may change. A PageModel/backend change, new reusable framework, or fourth
stylesheet exceeds this budget and requires a new user decision.

The user-authorized TEST 16 correction is the sole PageModel exception: adjust
only the landing discovery predicate in `Index.cshtml.cs` so a public
signup-open event (`FirstPublicAt` set) is eligible without a roster or board,
and retain the existing focused integration coverage. Do not change landing
markup, CSS, ordering, destinations, or private-event fail-closed behavior.

**Explicit non-goals.** Do not touch Admin, the accepted Landing composition,
Board, Captain, the public
Signups table, Settings/Setup, password recovery pages, My Accounts/My Events,
Notifications, Privacy/HowTo, global business/data rules, or later public
families. Do not resolve F-04 or F-06. Do not redesign the global header,
navigation, dialog loading protocol, or authentication workflow.

**Readiness decisions — 2026-08-22.** The user approved preserving the existing
immediate `/Account/DiscordComplete` processing redirect. Its progress panel in
PUB-REF-09 is not a runtime acceptance state, and `DiscordComplete.cshtml` plus
its handler remain outside the presentation rewrite. Update `SignupUiTests` to
retain semantic field names, validation, lookup hooks, and fallback-form
assertions while removing assertions for the generic composition this pass
replaces; keep public Signups-table assertions and that page untouched.

**Verification and approval gate.** The implementer performs one deliberate
first-pass comparison of each reference family at its intended desktop viewport,
then checks dark desktop and narrow/mobile recomposition; small corrections do
not require repeated screenshot forensics unless explicitly requested or a
material visual uncertainty remains. Run `git diff --check`, the focused Web
Release build, the smallest affected signup/dialog/onboarding tests, an EN/DA
visible-string scan, and a scoped Admin/later-family leak check. One fresh Terra
High independent reviewer then compares the complete scoped result and visual
evidence with this final contract. Only concrete findings receive a fresh,
bounded Luna High remediation and focused verification. Stop for the user's
manual visual approval after review/remediation and before Pass 2.

**Implementation stop rule.** Stop for user direction before changing product
behavior, backend authority, a route or handler, the complexity budget, the
accepted landing, the shared dialog protocol, or an out-of-scope family; also
stop if the references cannot resolve a materially different composition.
Record unrelated defects separately unless they prevent safe Pass 1B work.

**Pass 1B review/remediation checkpoint — 2026-08-22.** The fresh independent
review found three bounded presentation defects: shared controls/actions still
depended on rejected `body.public-ui-pass1` styling, two Signup section numbers
were blank, and Onboarding displayed an empty validation panel. A fresh focused
remediation made the family tokens/controls/actions/dialog styling independent,
restored `01`/`02`, and hides only the empty validation target while preserving
populated/focusable errors. Release Web build, focused Signup UI, public-dialog
and onboarding-WOM tests, diff/conflict, and scope checks pass. The user manually
accepted Signup and Confirmation on 2026-08-22 and Login, Onboarding,
AccessDenied, StatusCode, and Error on 2026-08-23 after bounded visual
remediation. Pass 1B is complete. Do not begin Pass 2 until its separate
user-authorized gate.

### Pass 2 implementation contract — user authorized 2026-08-23

**Scope and order.** Pass 2 structurally rebuilds the presentation of
`/Account/Settings`, `/Account/ChangePassword`, `/Account/ForgotPassword`,
`/Account/ResetPassword`, `/Account/Setup`, `/Account/MyAccounts`,
`/Account/MyEvents`, `/Notifications`, and `/Privacy`. Use PUB-REF-07 and
PUB-REF-08 for the account-form families, PUB-REF-10 for account overview,
PUB-REF-11 for Notifications, and the real-content Privacy composition in
  PUB-REF-12. The approved `/HowTo` guide resolves F-06 and remains outside this
  pass. The accepted shared public header and every accepted Pass 1 page are
  frozen. Admin, Board, Captain, event
roster pages, dashboard/Admin-actions specimens, and later families remain out
of scope.

**Presentation replacement.** Preserve PageModels, handlers, routes, field
names, validation, password and recovery rules, safe-return behavior, account
identity and character-management semantics, Wise Old Man fetch hooks, event
facts and destinations, notification read/unread behavior and destinations,
and authoritative Privacy meaning. Replace page-specific legacy/generic Razor
containers, surface stacks, mastheads, row/card geometry, form groups, and
responsive flow wherever they conflict with the applicable reference. A new
wrapper around the old composition or a page-scoped override layer is not a
Pass 2 implementation. Static explanatory copy may be shortened, reordered, or
rewritten in natural English and Danish to serve the reference hierarchy, but
must not invent capabilities or alter dynamic, account, event, notification,
security, or legal facts.

**Shared UI and dark theme.** Reuse the accepted shared shell, typography roles,
control/action semantics, focus treatment, and `site.public-ui.css`; do not add
a stylesheet, navigation framework, or generalized component system. Light
references own geometry and responsive composition. Dark mode changes tokens
only: charcoal fields/canvas, cream primary text, warm-gray secondary text and
neutral dividers remain dominant. Smoky indigo/violet is a restrained identity
accent for selected links, focus, utility accents, or deliberate large display
details; it must not take over headings, body copy, form labels, borders, rules,
or whole page regions. Coral, sage, and bronze retain their semantic roles.

**Reference and screenshot workflow.** The implementer inspects the current
bindings and each applicable reference once before its first implementation
pass, then builds the reference-owned content structure directly. It does not
enter repeated screenshot/render investigations for small corrections. After
the implementation handoff, the user supplies actual-route screenshots at the
requested desktop and narrow widths. A fresh independent review compares the
code and those screenshots with the references; only concrete findings receive
bounded remediation. Source checks and tests protect behavior but cannot approve
visual fidelity. On 2026-08-24 the user deferred final manual approval and
authorized the remaining Public UI passes to proceed sequentially in the current
dirty tree. Each pass still completes implementation, current screenshots,
independent reference/screenshot review, and bounded remediation. It is then
recorded as `awaiting manual approval`, never approved, before the next pass
begins. The user may supply screenshots and corrections during the sequence.
The accumulated manual walkthrough occurs after the implementation sequence,
including a shared-shell/CSS regression check.
Continue without a page-by-page manual stop unless a real product decision,
security/authorization/privacy/data-integrity issue, environment blocker, or
scope conflict requires the user.

**Complexity budget and stop rule.** Add no table, migration, service, route,
policy, job, JavaScript framework, stylesheet, navigation system, or speculative
UI abstraction. Expected changes are the scoped Razor pages,
`site.public-ui.css`, and the smallest affected resources, existing-script
hooks, selectors, and focused tests. Stop for a user decision before changing
backend behavior, security or password policy, routes/handlers, notification
semantics, authoritative Privacy meaning, the shared header, an accepted page,
or this complexity budget. A missing reference is blocking only when no approved
sibling reference resolves the required composition.

**Verification limits.** Run bounded source/diff/localization/scope checks and
the smallest relevant tests that the environment supports. The existing
MSBuild named-pipe sandbox denial is an environment limitation: after one clear
failure, do not repeat the unchanged command or its dependent build/test path.
Record the unrun gate and continue with independent checks.

**My Accounts review/remediation checkpoint — 2026-08-23.** The independent
PUB-REF-10 review requires one bounded My Accounts remediation: make the Add row
three equal field tracks plus a separate aligned Add action; keep both Saved EHB
and Fetch-from-WOM pairs as one horizontal composite at every viewport; remove
the redundant section-heading rules while retaining the masthead divider; and
align the linked-character action band with reorder controls immediately after
the editable fields. The user superseded the visible registered-character
warning/confirmation checkbox with a short semantic label above the row and the
existing localized native confirmation-prompt pattern on Unlink. Accepting that
prompt may submit the existing confirmation value; dismissal, unavailable
JavaScript, or bypass remains fail-closed on the server, and unlinking never
changes the event registration. One fresh Luna High remediator may change only
`MyAccounts.cshtml`, `site.public-ui.css`, the smallest existing public-script
hook, directly affected EN/DA resources, and one focused selector/behavior test.
It must not change the PageModel, service, route, handler, authorization, or
registration semantics. The bounded Luna High remediation is complete in the
three permitted Razor/CSS/resource files; focused structure, responsive-cascade,
localization XML, fail-closed confirmation, and diff-hygiene checks pass. No
MSBuild/test command was run because the established host blocker is unchanged.
The user manually approved the My Accounts page on 2026-08-24 after its bounded
manual corrections.
The reviewer separately found that the current pass appears to change Fetch-WOM
persistence, fold/remove the Correct handler, and rename other account actions;
reconcile that protected behavior against the functional contract separately
before final My Accounts functional acceptance.

**My Accounts manual correction — 2026-08-23.** During manual acceptance the
user replaced the separate preferred-character action with one order-derived
rule: the active character in position 01 is always the sole preferred
character, and reordering transfers preference to the new first character. The
same correction shortens linked-row Fetch-from-WOM to the existing sync icon
plus `Fetch`, changes visible `Saved EHB` labels to `EHB`, and changes the short
warning to `Registered for an event`. This is an approved behavior correction,
not visual-only remediation; one bounded remediator may remove the obsolete
Preferred handler/control/service entry point, make normalization follow the
first active ordered link, update directly affected tests and localization, and
touch no unrelated account or event behavior.

The next manual correction gives the Add-character form an intermediate two-by-
two layout at the established `1200px` family breakpoint before its existing
single-column mobile stack. It also limits My Accounts saved-EHB entry, fetched
values, persistence, and rendering to two decimal places using standard decimal
rounding (`3000.09582` becomes `3000.10`) without
changing historical event snapshots or the wider EHB-calculation contract.
The bounded remediation is complete: the Add form now uses the approved
intermediate grid, My Accounts EHB mutation/fetch/rendering applies standard
two-decimal rounding, and reorder clears the old persisted preferred flag before
assigning position 01 inside the same transaction. Focused Release builds and
source/diff checks pass; the PostgreSQL integration scenario remains unrun
because this worker could not access the Docker socket.
The user subsequently approved the corrected My Accounts page. Stop here; do
not begin My Events or another Pass 2 page without the user's next authorization.

**My Events review checkpoint — 2026-08-24.** The user next authorized My
Events by supplying light desktop, dark desktop, and 390px narrow current-route
screenshots. One fresh Terra High reviewer compares that evidence and the
complete scoped My Events diff with PUB-REF-10. The reviewer remains read-only;
only concrete findings may receive a fresh bounded Luna High remediation. Do
not begin another Pass 2 page.

The user stopped that reviewer after its concrete row-composition finding.
PUB-REF-10 remains the sole visual target: current semantic backgrounds form
full-width bands where the reference uses a compact dot/label status followed by
a neutral vertical divider. One fresh bounded Luna High remediator may correct
only that finding and its necessary responsive composition. Existing Landing
assets or selectors may be reused where the similar structure genuinely matches,
but Landing is not a reference and its date gutter, columns, or composition must
not be copied. Preserve all current classification, ordering, privacy, destination
behavior, and approved Landing code; do not modify another family.

The bounded My Events correction is complete in its Razor/CSS selectors and its
focused Release build passed with zero warnings/errors. The user manually
accepted the resulting page on 2026-08-24 despite a remaining non-blocking visual
imperfection. Stop here; do not begin another Pass 2 page without the user's next
authorization.

The user then authorized one shared secondary-navigation correction. Existing
event-view and account-view links currently read as an extension of the colored
header; move that shared navigation below the masthead and apply PUB-REF-02's
content-level tab treatment. Preserve destinations, visibility, active state,
localization, focus, and narrow access. Do not change the approved account page
bodies, Board behavior, link inventory, or another page family. One fresh Luna
High implementer performs this bounded shared-shell/CSS correction and stops for
user screenshots and manual acceptance.

During that manual check, the user named one direct My Events correction: keep
row dividers between event entries but remove the bottom divider from the final
row in each Current Events or History list. The first selector-only correction
left the terminal bottom padding behind; the bounded continuation removed that
bottom padding while preserving top/inter-row spacing. No further My Events row
work is authorized before the same manual acceptance gate.

The shared-navigation manual check also requires one fresh bounded Luna High
remediator after the My Events selector task completes. Reduce the excessive top
padding only on pages that render secondary navigation; make both primary and
secondary active underlines thicker; and position the primary underline beneath
the text with the same internal padding as the secondary tabs rather than at the
masthead bottom. Preserve header height, alignment, mobile menu behavior, routes,
active-state semantics, page bodies, and all unrelated shell styling.

That bounded shared-navigation remediation is complete. It uses one layout-owned
body context for pages that actually render the secondary row, reduces Account
and Board top spacing at desktop and narrow widths, uses matched 3px active
underlines, and keeps the primary rule beneath its label without affecting mobile
menu dividers. The focused navigation test, scoped diff check, and Release Web
build pass. Stop for user visual acceptance.

The next manual finding is one shared navigation-state correction: give the
content-level account/event tabs the primary header's text-color-only hover cue,
and restore a distinct dark-mode resting color so primary and secondary hover
states remain visible. One fresh Luna High remediator may touch only the shared
navigation CSS and its focused assertion; do not change geometry, active
underlines, links, routes, page bodies, or other states.

That hover-state correction is complete in shared CSS and its focused assertion.
The focused public-dialog navigation test and scoped diff checks pass. No Release
build was repeated for this CSS/test-only follow-up; retain the preceding passing
shared-navigation Release build and stop for user visual acceptance.

The user rejected the hover color direction. One fresh Luna High remediator must
reverse only that state mapping across primary and secondary navigation in light
and dark: inactive and selected labels share the same full-strength resting
color; hovering an inactive label uses the faded color. Selected labels remain
full-strength and underlined. Preserve focus visibility, geometry, spacing,
mobile menu rows, links, routes, and all page content.

That reversed color-state mapping is complete in shared CSS and its focused
assertions. The focused bundled-Node navigation test and scoped diff checks pass;
no Release build was repeated for this CSS/test-only correction. Stop for user
visual acceptance.

The user accepted the corrected shared navigation by moving to the next-page
gate. Resume the declared Pass 2 order with `/Account/ChangePassword` under
PUB-REF-08. Do not begin it until the user authorizes that page task; Forgot and
Reset Password follow as siblings in the same narrow-form family.

The user authorized the Change Password manual correction and identified its
only current visual blocker. One fresh Luna High remediator changes only its
dark-mode presentation: `Account security`, `Current password`, and `New password`
use cream rather than violet, and resting inputs use the approved neutral dark
border. Preserve repeated named heading/label instances, focus/error states,
light mode, bindings, handlers, password policy, safe return, shared navigation,
and every sibling page. Stop for user acceptance; do not begin Forgot/Reset.

That Change Password dark-mode correction is complete in one page marker and
page-isolated dark selectors. Scoped selector/isolation and diff checks pass;
the Release build was not repeated because of the documented MSBuild sandbox
limitation. Stop for user light/dark acceptance.

**Manual rejection and replacement baseline — 2026-08-22.** The user rejected
the complete rendered Pass 1B result after the checkpoint above. Passing tests
and the prior review remain behavior evidence only. The rejected pages retained
too much of the legacy DOM, width constraints, containers, information flow,
form geometry, and generic surface/action composition, then applied the new
identity through page selectors. This is the same prohibited hybrid failure as
the original rejected landing attempt; it is not eligible for narrow CSS
remediation or incremental preservation.

The next work is one fresh Pass 1B presentation implementer, not the previous
implementer or a small remediator. Substantial structural replacement of the
scoped Razor pages is required. Preserve only functional bindings, form names,
validation, routes, handlers, authorization, ReturnUrl safety, WOM and dialog
hooks, localization, and progressive-enhancement semantics. Replace the
page-specific legacy/generic containers and rejected Pass 1B composition with
the structures owned by PUB-REF-05, PUB-REF-06, and PUB-REF-09. Signup must
visibly implement the large event masthead, capacity/status composition,
numbered `01`/`02`/`03` ruled sections, open form layout, summary rail, and
bottom action row. Confirmation must implement the reference-owned outcome
hierarchy for confirmed, waiting, withdrawn/rejoin, and read-only states.
Login, Onboarding, AccessDenied, Error, and StatusCode must each be editorial
route compositions rather than restyled generic panels. Static explanatory copy
may be shortened, reordered, or replaced when meaning and localization remain
correct.

The shared public header rendered by `_Layout.cshtml` and its existing approved
CSS are frozen. Reference headers provide surrounding context only. The rewrite
must render beneath that header and must not duplicate, replace, restyle, or add
a page-local header/navigation system. Preserve its logo, navigation,
account/notification area, responsive behavior, localization, focus behavior,
and login/navigation protocol exactly. Remove only rejected page-specific
selectors after their markup no longer uses them; do not alter shared header or
accepted landing selectors.

Acceptance requires fresh actual-route screenshots at the canonical desktop
viewport and narrow/mobile width for Signup create/edit/unavailable,
Confirmation confirmed/waiting/read-only, standalone Login, Onboarding,
AccessDenied, 403, 404, and 500/request-ID where the environment can render it.
Compare those directly with the applicable approved reference and explicitly
show that the one existing shared header remains unchanged. Source checks,
builds, and focused behavior tests cannot substitute for visual fidelity. Stop
after fresh independent review and any bounded correction for the user's manual
approval; do not begin Pass 2.

**Second rejected rewrite and atomic recovery — 2026-08-22.** The fresh Pass 1B
rewrite above also failed manual visual review. Evidence must not be inverted:
`/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/TEST 16 — Signup lookup - OSRS Community Bingo.pdf`
is the current rejected implementation; PUB-REF-05
(`/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-12716d59-b32e-4027-9b5c-7947c885c2db.png`)
is the approved Signup target. The rejected render remains a tall, narrow legacy
form flow with an oversized wrapped title, stacked generic fields, and a
detached/redundant summary. The target is a wide horizontal editorial worksheet:
a compact event masthead with title, capacity and lifecycle status sharing the
top band; three compact ruled form rows with number/section label, controls, and
one persistent right summary rail; then a deliberate bottom action row. Dynamic
TEST 16 values replace mock values, but its silhouette, column relationships,
density, hierarchy, and responsive recomposition must come from the target, not
from the rejected DOM.

Stop broad Pass 1B implementation. Recover atomically: rebuild only Signup and
standalone Login first, render their actual routes at 1586×992 plus narrow/mobile
and dark, and return them for user inspection before Confirmation, Onboarding,
or any status page continues. The Signup page-specific Razor body must be
replaced from a clean target-owned skeleton; do not preserve or restyle the
rejected `signup-page`/hero/form/summary structure merely because it contains
working bindings. Reattach the existing bindings and hooks to the new skeleton.

The accepted Landing header is the one global public header implementation
owned by `_Layout.cshtml`; do not duplicate it or add page-local navigation.
Remove the layout branch that emits `public-live-header-*` markup/classes for
non-Landing public pages and use the accepted `landing-shell-*` composition on
every public route. Header Sign in navigates normally to standalone
`/Account/Login` without dialog-open attributes. Preserve dynamic navigation,
the DK mark, mobile menu, notifications, authenticated account/settings
behavior, popovers, localization, focus, event/account context navigation, and
explicit event/signup-entry dialog launch points. Admin keeps its separate
layout and is out of scope. Standalone Login must itself be rebuilt from the
Login panel in PUB-REF-09, not from the rejected login body or dialog
composition.

**Atomic screenshot-review correction — 2026-08-22.** User-supplied actual-route
screenshots at 1586×992 and 390×844, compared with PUB-REF-05 and the Login panel
of PUB-REF-09, block both atomic pages. Signup must be compacted into the target's
horizontal worksheet so its action row is visible at the desktop reference
viewport, and its masthead must show real confirmed/capacity and waiting values.
The complexity budget therefore permits one read-only `SignupModel` projection
addition for those existing facts; it does not authorize persistence, workflow,
capacity-rule, or handler changes. Login must own the available desktop width,
use a broad form column with an unbroken desktop headline, and carry its
diagonal DK artwork field through the right side rather than leaving a narrow
central island. Static Signup/Login copy is composition-flexible: it may be
shortened, reordered, or replaced in natural English and Danish while dynamic
event/account/capacity/date data, action meaning, destinations, validation, and
authentication/signup behavior remain authoritative. The next worker is one
fresh bounded remediator; it performs source/build checks without another live
browser-forensics loop, then stops for user replacement screenshots.

**Rejected implementation record — 2026-08-22:** the first Pass 1 result is a
failed visual approach, not a partially accepted baseline. It retained generic
classes such as `public-ui-component-header`, `public-ui-action-list`,
`public-ui-data-group`, `public-ui-page-masthead`, `public-ui-surface`,
`public-ui-sectioned-surface`, and `public-ui-event-directory-row`, then layered
`body.public-ui-pass1` overrides above the legacy composition. The user rejected
the complete result. Do not dispatch the reviewer's narrow signup remediation,
reuse the failed implementer/reviewer chats, or treat passing source/tests as
visual acceptance.

Each public pass uses one bounded Luna Max implementation, one fresh Sol High
independent review, focused remediation/verification only when a named
finding requires it, and user manual visual acceptance before the next pass.
After acceptance, remove only that family's superseded public residue from the
transitional files; preserve all unrelated and Admin rules.

The approved Admin baseline remains the shell, Event Create, Identity, Schedule,
Manage/Overview, Events directory, Participants with accepted detail-dialog
states, Catalogue, Accounts/Roles, Board, and Teams/Draft. No Admin page is
implicitly UI-approved by the launch decision; this UI-approval boundary does
not change the recorded production launch.

## 3. Production readiness and release gates

Historical launch evidence (2026-09-01, not a current deployed-image check):
production launched from PR #5 at source SHA
`1f893133edc26455c41535807633225fdee36292` with immutable image digest
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
CI, deployment, and focused production smoke passed for that launch. See the
[release-readiness gate](#release-readiness-gate-3-october-2026) for the current
PRE-01 evidence, release checks, and verification limits. The remaining operational
stage is the production Admin test event; it has not run. Launch-critical
release work takes precedence over deferred UI polish.

### Release-readiness gate (3 October 2026)

This is the current PRE-01 release gate. The records below preserve sanitized
user/operator evidence from 3 October; the agent did not access production or
run these queries. Before a release, the operator records the candidate SHA,
exact deployed migration history, and the check timestamp, then runs every
read-only check with separately authorized operator access and preserves the
baseline on an isolated restored copy. Mutating migration/conversion/Down checks
require the approved isolated procedure; never infer live database authority.
Aggregate counts do not establish the migration baseline.

| Check | Query or required step | Recorded evidence / current status | If the result differs |
| --- | --- | --- | --- |
| Banner cleanup (X-1/F1) | Verification only: exact migration history, zero `event_banner_assets`, zero `event_banner_cleanups`, zero events referencing `banner_asset_id` before retirement; if already retired, verify history and absent schema. See [runbook checklist](docs/PRODUCTION_RUNBOOK.md#release-readiness-checklist--3-october-2026). | One-time targeted cleanup/object deletion reported complete by the user on 3 October; not agent-verified. | Stop on unexpected rows/schema; seek an operator decision. Do not repeat deletion or contact object storage from rehearsal. |
| Luck v1 conversion (LK-2/R-1) | Exact candidate `--convert-luck-checkpoints` stage after `--migrate`, before preflight/web, under approved isolated harness; require exit 0 and `Could not convert=0`. | R-1 remains blocking; a passing final-candidate restored-data conversion is not established. | Preserve v1 rows/failure reason; stop before preflight/web. No newer provider-data shortcut. |
| All retained completion corrections (BR-7) | `SELECT COUNT(*) FROM team_completion_corrections;` No global cycle-time parameter. | User's earlier 3 October zero is not a verified result for this corrected all-rows query; re-run required. | Any row stops release for investigation; readiness/ranking ignore retained corrections. Do not rewrite history/snapshots. |
| Published board without active finalized roster publication (BR-11) | `SELECT COUNT(*) FROM boards b WHERE b.state = 'Published'   AND NOT EXISTS (     SELECT 1     FROM draft_publication_cycles c     JOIN draft_sessions d ON d.id = c.draft_session_id     WHERE d.event_id = b.event_id       AND d.state = 'Finalized'       AND c.superseded_at IS NULL       AND EXISTS (         SELECT 1 FROM draft_publication_rosters r         WHERE r.draft_publication_cycle_id = c.id       )   );` | The user's earlier 3 October zero must be re-run with this corrected query; not a verified pass. | Any result stops release. Reconcile the existing publication lifecycle; event boolean alone is insufficient. |
| Future-effective account switches | At recorded check time, `SELECT COUNT(*) FROM event_participant_character_swaps WHERE effective_at_utc > :check_time_utc;`; inspect timestamps/attribution privately. | Unknown/unverified. | Stop for deterministic operator decision preserving submitted attribution/history; no silent cancellation/backdating. |
| G4 `20261003184632_AllowCancelledDraftRestart` | Before migration on restored baseline: count `draft_sessions WHERE state = 'Setup' AND first_pick_recorded_at IS NOT NULL`; after, same eligible set/count must have `requires_fresh_order = true`, ineligible rows retain default false. Separately verify Down removes column/history entry without draft/first-pick loss and re-Up backfills again. | Count and Down/Up unexecuted. If already applied in backup, report that instead of claiming a fresh backfill. | Stop on mismatch; use separate disposable clone and approved predecessor/tooling. No production downgrade. |
| R-3 final-candidate rehearsal | **Procedure and coverage limits approved by the user on 4 October 2026 after Claude’s review, as recorded in the planner decisions; attribution corrected by the quoted Step 0 user assignment.** See [Step 0 approval provenance](docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md). Follow the [isolated runbook procedure](docs/PRODUCTION_RUNBOOK.md#r-3-isolated-rehearsal-procedure--approved-4-october-2026): disposable VM/restored DB, denied external access, local WOM/HTTPS S3 fixtures, exact candidate restore/history → migrate → Luck conversion → preflight → web/health. Never host `bingo-deploy` as rehearsal. | Tooling not built/tested; R3 unexecuted; release blocked. Accepted limits: no production wrapper, real provider/restic/GHCR integration, public DNS/TLS or production-key recovery verification. Approval is documentation-only; Claude source recheck accepted with Step 0 attribution corrections. | Obtain separate tooling/backup-transfer/execution authority. Preserve R-1 failure blocking and every final-candidate check. Stop on any failure; no deployment until R3 passes and production deployment is separately approved. |

The gate is complete only when each release-time query/step has a recorded
candidate identity and passing result. The runbook owns the operator sequence;
this section owns the release decision and the explicit stop conditions.

Keep the complete infrastructure and operational checklist through release,
including optional but prudent safety items. Evaluate each item when its
deployment step approaches and present provider/tier options, current costs,
tradeoffs, a hobby-project recommendation, and the consequence of deferring or
omitting it. Optional does not mean silently removed. Do not create an external
account, purchase a service, enable a paid tier, accept a credential, change DNS,
or mutate production without the user's explicit approval. Keep repository-side
automation provider-portable where practical until a choice is required.

The release checklist below is retained as the operational contract; its launch
steps are now recorded as completed evidence, with the Admin test event the
next stage:

- Whole-application desktop/mobile, keyboard, permission, error, accessibility,
  and functional regression was accepted at baseline `c3e43bb` and carried into
  the deployed PR #5 candidate.
- Production image/Compose, CI, controlled deployment, restricted storage,
  health reporting, backup/restore, monitoring, and operator runbook work was
  completed and exercised as recorded below.
- The production historical import and its public landing/Board/Teams smoke
  checks succeeded on 2026-09-01; the rehearsal event remains Hidden and
  separate.
- The production Admin test event is the next operational stage and has not
  run.

**Production Release Pass 1 — provider-neutral topology, complete 2026-08-27.**
The single-VPS Compose contract now defines Caddy, one ASP.NET Core web replica,
and private PostgreSQL networking; persistent PostgreSQL, data-protection,
catalogue-cache, and Caddy state/config volumes; immutable image input; the
R2/Discord/Wise Old Man/bootstrap configuration names; and clean-start/operator
assumptions. CI/image publication, application operations and health components,
deployment automation, provider setup, backup/restore, rehearsal, and release
were completed in the later passes recorded below.

**Production Release Pass 2 — application operations, complete and
independently cleared 2026-08-27.** Persist and startup-validate the configured Production
data-protection key ring; retain the private-Caddy forwarded-header model; use
the built-in Production JSON console logger; keep public `/health/live` cheap;
make container-internal `/health/ready` gate PostgreSQL, configured R2 bucket
reachability, and timely heartbeats from both existing hosted workers; keep Wise
Old Man non-blocking; add explicit migration and read-only production-preflight
commands without normal-startup migration; and initialize non-root ownership of
the writable data-protection and catalogue-cache volumes. Clean setup restores
the reviewed database backup after migration, then runs owner bootstrap,
preflight, and web/Caddy. Retained data runs the legacy Slice 1 preflight only
when crossing that boundary, then migrate, production preflight, and replacement.
The catalogue snapshot loader is reserved for CI, Development, and manual-test
data and is never applied by production deployment.

The Pass 2 complexity budget is zero tables, schema migrations, product routes,
policies, jobs, NuGet dependencies, CI/deployment/provider work, or generalized
frameworks. Extend existing startup, health, worker, storage, command, and
Compose code; add only the smallest validator, heartbeat state, R2 availability
probe, and volume-permission/health-probe wiring demonstrated necessary by the
readiness review. Preserve Development and all product/UI/domain behavior. CI,
deployment automation, provider setup, backup/restore/rollback runbooks,
rehearsal, final release, asset work, and monitoring-vendor selection were
completed in the later passes recorded below.

Release Web and IntegrationTests builds, Production Compose rendering, scoped
diff/secret checks, and the built-in .NET readiness-probe command pass. Focused
test execution and immutable-image execution remain unverified because the host
denies the test runner listener and local Docker API respectively; neither is a
known failure. No production or provider resource was changed.

**Production Release Pass 3 — release-candidate publication and non-mutating
promotion, implementation-ready 2026-08-27.** Preserve the existing PR/main CI
job and check name. After that job succeeds on a `main` push, publish one
`linux/amd64` image from the current Dockerfile to a fixed GHCR package with a
trace-only full-commit tag, while making the immutable digest authoritative.
Record image name, digest, source SHA, platform, workflow run identity/URL, and
timestamp in a small candidate artifact. Use job-scoped least privilege, no PAT
or PR secrets, and full-commit pins for trusted actions.

Add one manual `production-promotion.yml` workflow that runs only from `main`,
accepts the source SHA, digest, and CI run ID, validates their syntax, proves the
successful main run's candidate artifact binds the exact values, and emits a
promotion receipt. Its explicit manual `workflow_dispatch` with `mode: deploy`
is the user's production approval; no GitHub Environment, required reviewer, or
environment secret is used. It performs no SSH, image pull, migration, Compose
operation, or other production mutation in `promote` mode.

Actual VPS deployment belongs to Pass 4 after a pre-migration backup, tested
restore, and rollback contract exist. Pass 3's complexity budget is one extended
CI workflow, one new promotion workflow, and the two small JSON receipts. Add no
deployment scripts, third-party deploy action, application/schema change,
provider account, secret, SSH code, SBOM/signing/provenance framework, build
cache, multi-architecture image, or production resource. GHCR remains private
by default, the repository remains private on GitHub Free, and no Environment
reviewer or environment secret is required.

**Production Release Pass 4 — repository-side deployment and recovery contract,
committed through `329e04adaa98a444f69d20aa41385b4ca7426bd3` on 2026-08-28.**
The existing promotion workflow has explicit `promote` and `deploy` modes.
Candidate validation runs before either action; the explicit manual
`workflow_dispatch` selecting `mode: deploy` is the user's production approval.
The workflow uses non-cancelling `concurrency: production` and transports only
SSH data plus validated release metadata. Native OpenSSH invokes the narrowly
sudoable root-owned host command.

Add only the minimal host deploy, encrypted restic backup, isolated restore
verification, and database-stored evidence-integrity scripts; root-only host
configuration examples; one systemd backup service/timer; and the deployment
runbook. Deploys back up before every replacement, use the exact image digest,
preserve Caddy/PostgreSQL, run the existing clean/retained migration and
preflight commands, verify internal and public health, and emit a secret-free
receipt. Changed migration history never permits automatic image rollback.
No application behavior, schema, provider, account, DNS, secret, or production
resource was changed. The exact operator procedure is in
`docs/PRODUCTION_RUNBOOK.md`.

Pass 4 complexity budget is one modified promotion workflow, minimal host
scripts/config examples, one systemd backup service/timer, and concise
deployment/recovery/evidence documentation. Focused checks are limited to
script syntax, input rejection, workflow/action pin and permission inspection,
Compose rendering, receipt/secret-leak checks, documentation links, and a
disposable restore exercise only if Docker is available.

The user-approved Sol High integrated review identified six Pass 4 cross-pass
blockers: self-contained host-loss recovery, write quiescence/migration safety,
single database authority, physical Compose volume identity, clean-vs-retained
bootstrap state, and clean-host Caddy startup. That bounded correction is now
followed by a four-finding release-blocker remediation: exact PostgreSQL
database restore and migration-history verification, `none`/`none` baseline
recovery, post-backup failure classification, and `new` marker/history
contradiction rejection. The separate bootstrap-password correction is also
complete: the password is bootstrap-only and root-file supplied, absent from
the long-running web container and durable backup/config payloads, retained on
failed initialization, and removed after successful initialization or safe
resume. Passes 2–3 otherwise cleared, and no P0, secret, or unapproved-scope
issue was found. The correction is committed and pushed through `aa1af77`. Later
application corrections are committed locally on the release branch: `182b84f`
fixed production routing/live controls, `cd83c36` polished submission drawer
controls, `88cd8f8` consolidated the canonical submission workspace, and
  `e75ec57` polished notification-popup and Board-family progress notices. The
accepted final regression/package work is included in the deployed PR #5
candidate recorded below.

**Production launch evidence — complete 2026-09-01.** PR #5 was merged and
deployed at source SHA
`1f893133edc26455c41535807633225fdee36292` and immutable image digest
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
CI, deployment, and focused production smoke passed. The production rehearsal
event remains Hidden, not deleted, and is reachable only to SuperAdmin through
`/Admin/Events?filter=hidden`.

Better Stack production monitoring is active: public `/health/live`, quarter-
hour disk heartbeat, and nightly backup heartbeat, with both timers active.
Disk success/failure/recovery was verified. A scheduled encrypted backup
succeeded with snapshot
`715e2ee745e1fb51f10c510b6b2995aefb5109ea9703dd7f349e8fa37aac70d9`; retention
was applied. Heartbeat URLs and credentials remain outside Git.

The production import of **Det Store Danske Sommerbingo 2026** succeeded with
slug `det-store-danske-sommerbingo-2026`. Preflight found 6 teams, 90
participants, 93 WOM accounts, 25 tiles, and 150 counters. The reviewed
combined hash is
`c8ef4ec0a01ef1779ec3ea358100b91968c942d6f875ec15a7504014b2684df8`.
Production landing, Board, and Teams returned 200, and the user manually
accepted the imported event. Private host/staging input copies were removed;
ignored local operator input remains outside Git.

Known non-blocking Admin defect, explicitly deferred by the user: selecting
Hidden (and potentially other server-filtered states) in the Events dropdown
performs client-only filtering/history replacement, so rows absent from the
normal DOM do not appear. Directly loading `?filter=hidden` works; there is no
data loss.

- **Capacity sub-gate complete (2026-08-30):** the production rehearsal held
  100/100 concurrent SignalR viewers and completed 400/400 public Board, team,
  tile, and evidence requests with zero failures. Full-run HTTP latency was p50
  702.4 ms, p95 2516.4 ms, p99 3066.3 ms, and max 3749.8 ms; SignalR connection
  latency was p50 250.5 ms, p95 344.1 ms, p99 377.5 ms, and max 410 ms. A short
  repeat produced HTTP p50 266.0 ms, p95 1027.9 ms, p99 1278.3 ms, and max
  1451.1 ms. Brief 2-vCPU saturation during synchronized arrival caused no
  request failures, swap pressure, or persistent health issue. This clears the
  documented 100-connected-viewer launch target for the 2-vCPU/4-GB tier. Do
  not repeat this capacity run unless infrastructure or performance-sensitive
  behavior changes materially. This evidence covers anonymous public reads and SignalR
  subscriptions; it does not replace the authenticated application-journey
  evidence required elsewhere in this gate.

The current production/release order is frozen:

1. Run the production Admin test event. It has not run.

The R2 deletion/versioning or accepted-recovery decision remains an explicit
known operational risk; existing integrity evidence does not silently solve it.
The deferred Events dropdown defect is non-blocking and does not cause data loss.

### Approved historical-event import — complete in production 2026-09-01

The product/data slice is the one-time operator-controlled import of **Det Store
Danske Sommerbingo 2026**. Its approved behavior, frozen source inputs,
deterministic allocation, privacy boundary, and exact historical disclosure are
owned by `PRODUCT_REQUIREMENTS.md`,
`FUNCTIONAL_CONTRACTS.md`, and `DATA_MODEL.md`. The event is imported directly
as `Archived` for `Europe/Copenhagen`, from `2026-07-14 18:00 CEST` through
`2026-07-19 18:00 CEST`, with `ArchivedAt` equal to the end; no temporary Live
state or ongoing synchronization is allowed.

The implementation is one narrow service plus explicit CLI preflight and apply
control, with zero new tables, pages, routes, policies, jobs, dependencies, or
generalized frameworks. Versioned metadata may include the public board
definition, exact corrected 402 counter units across 150 team/tile cells,
source identifiers, and hashes, but never the private 90-participant/93-account
mapping. The reviewed public manifest SHA-256 is
`e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`.

Preflight is mandatory, apply is audited and transactional, and an already-
applied exact import hash is the only no-op case; a divergent hash fails closed
and any apply failure rolls back without partial state. Apply requires explicit CLI
invocation, exact event-name confirmation, and an active SuperAdmin actor
validated inside the locked serializable transaction; it is a separate user
authorization from implementation or release packaging. Failed preflight and
divergence identify the affected input or retained record and the operator
correction required before retrying.

The initial implementation review identified six concrete blockers. The final
independent Sol High closure review identified three additional blockers:
Production CLI reachability, the reviewed public manifest pin, and active-
SuperAdmin authorization inside the locked serializable apply transaction.
All nine were remediated before local commit
`c4130f437b82cf5ceb5f130cf8a77a3a05ae2079` (`Add historical 2026 event import`).
Focused verification passed: `HistoricalBoardReferenceTests` 25/25;
`Bingo.IntegrationTests` Release compilation with 0 warnings/errors;
`HistoricalImportIntegrationTests.AppliesFictionalOperatorInputAndExactRerunIsNoOp`
1/1 using isolated Docker/Testcontainers; and
`Slice10Pass103ActivityProjectionTests.DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly`
1/1 using isolated Docker/Testcontainers. The user applied the corrected import
only to the local Development database, manually inspected it, and reported
that everything looks good.

The local Development inspection preserved the exact approved disclosure,
itemless reconstructed approved rows in Recent Drops without fabricated drops
or evidence, deterministic within-team EHB-weighted attribution/timing, the
complete source WoM snapshot without normal refresh, and the approved Maggot
King and Superior Slayer rules. Production preflight then found 6 teams, 90
participants, 93 WOM accounts, 25 tiles, and 150 counters; the reviewed
combined hash was
`c8ef4ec0a01ef1779ec3ea358100b91968c942d6f875ec15a7504014b2684df8`.
The imported production event was manually accepted after landing, Board, and
Teams returned 200. Private host/staging input copies were removed; ignored
local operator input remains outside Git.

## 4. Dependencies, approvals, and stop rules

- Use `UI_SYSTEM.md` for global UI rules and `UI_PAGE_MATRIX.md` for page
  family/reference/exception/approval decisions. Do not recover authority from
  archived documents or root tombstones.
- The public Board/evidence family's behavior remains protected except for the
  approved regular-page navigation correction. Its old appearance is superseded;
  structurally rewrite it only in Pass 4 from the reactivated references while
  preserving the current near-target team-overview grid, routes, browser history, team/tile relationships, focus,
  tile-sidebar behavior, shared-drawer submission-result acknowledgement,
  evidence viewing, and realtime non-interruption. Preserving the rejected
  legacy masthead or team-workspace composition and changing only its skin is
  an explicit failure. The overview grid itself needs only corrected masthead
  integration and the missing Recent Activity footer.
- The rebuild complexity budget is zero new tables/migrations, services,
  policies, jobs, product routes, generalized frameworks, stylesheet files, or
  navigation systems. Reuse `_Layout.cshtml`, `site.public-ui.css`, the current
  transitional owners, existing page markup/partials and JavaScript modules,
  and the localization pipeline. A licensed local font or supplied artwork is
  allowed only when actually needed by the accepted identity; add no theme
  persistence service.
- No font metadata was recovered from the AI-generated landing reference, so no
  candidate may be called the original/reference font. Pass 1A may compare the
  current Barlow Condensed, Bebas Neue, and at most one genuinely closer
  license-safe condensed display face using the exact headline, event-title,
  and numeral specimen at a common crop/scale. Choose by silhouette, cap height,
  width, stroke density, punctuation, numeral shapes, and legibility. If no
  candidate is clearly closer, stop with a compact A/B/C specimen for user
  choice. A selected face is an implementation approximation for hero, section
  headings, event names, feature numbers, and event-date numerals; Barlow
  Condensed SemiBold remains utility/navigation/status and the bundled Geist
  variable face remains body/control only while it continues to match. Do not
  synthesize proportions with `scaleX` or another transform. Bundle only the
  selected license-safe face and its license, and verify browser-resolved faces.
  The current bounded comparison selected Bebas Neue Regular over the locally
  available Barlow candidate as the implementation approximation; no third local
  candidate was available. This provenance record does not identify the unknown
  reference font and does not substitute for user visual approval.
- User-authorized typography-only A/B (2026-08-22): compare the current Bebas
  Neue 400 landing display roles with a genuine locally bundled Barlow Condensed
  800 face, untransformed and at the same composition/viewport. Preserve Geist
  body copy, utility roles, colors, copy, spacing, layout, responsive geometry,
  and behavior. Bundle only the OFL-licensed static face and license inventory
  needed for the experiment; do not synthesize 800 from the current 600 file.
  The result remains subject to user visual choice and does not reopen other
  landing findings or another page family.
- User manual typography/ledger/icon correction (2026-08-22): retain Barlow
  Condensed ExtraBold 800, especially for major numerals and headings, while
  reducing current-event name size to match the target. Current-event vertical
  date dividers become more vertically inset; neutral row hairlines extend to
  the target junction near/slightly before that divider and are absent after the
  final current row. Previous events use the target's compact separate archive
  composition—name/state left, history action right—rather than the full
  date/status/details grid. Enlarge and normalize all three feature SVGs to the
  target's optical size/mass, with a full sheet plus distinct sage approval
  badge and a fuller four-bar bronze chart. Preserve dynamic facts, routes,
  semantics, themes, responsive behavior, and all other landing composition.
  Reduce the Current events and Previous events Barlow 800 heading size slightly
  without changing their strong-rule relationship.
- Render-effort rule (2026-08-22): perform detailed implementer visual
  inspection for a page family's first coherent implementation, an explicit
  user request, or a concrete uncertainty whose result can materially change
  the fix. Do not repeat heavy self-inspection for every small remediation.
  Surgical follow-ups receive focused implementation and technical checks, with
  a render only when immediately inexpensive, then return to the user for visual
  acceptance rather than iterative implementer tuning.
- Final SVG-only correction (2026-08-22): use the newest target/current feature
  crops recorded in `CURRENT_STATUS.md` to reproduce the actual target paths
  for the broadcast, sheet/check badge, and four-bar chart. Preserve the accepted
  rendered icon size and strip layout. Keep full stroke extents inside the 32×32
  viewBox so no cap, join, badge, or baseline clips. Run focused technical checks
  and return directly for user inspection without another heavy render loop.
- The approved landing screenshot is a visual contract. Review must compare
  silhouette/major regions, typeface/weight/scale, header geometry, hero
  diagonal/logo, feature strip, event-ledger density/alignment, links/actions,
  colors, whitespace, and responsive recomposition. Tokens, selectors, tests,
  or source inspection alone cannot approve the pass. Review static editorial
  copy for truthful product meaning, correct action/destination semantics,
  natural English/Danish parity, and composition-appropriate rhythm; exact
  preservation of the rejected implementation's wording is not required.
- Manual visual remediation record (2026-08-22): the current evidence at
  `/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/Here.pdf`
  is much closer but unapproved. The next bounded landing correction must use
  the bounded display-candidate comparison above; unify the optical weight of all blueberry
  numerals; use inset feature dividers and date-gutter-aligned event-row rules;
  remove the duplicate rule below the Current events heading; correct the
  blueberry header, DK mark, coral/sage/bronze state application, and inactive
  Captain navigation color; normalize the three feature SVGs as one restrained
  stroke family; keep the hero ending near y=443 while restoring target headline
  height/vertical rhythm and feature-strip breathing room; and rebalance the
  ledger so details begin near the target position without crude long-name
  truncation. Preserve real event counts/data. This is remediation within the
  approved design, not a new page or direction.
- Dark mode is a P1 manual blocker in the same landing remediation. The evidence
  at `/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-a94f92be-bc92-4b1b-a8b3-65120651581b.png`
  shows content accents inheriting the near-black shell token. Split landing
  tokens into shell background, content accent, primary text, muted text, rule,
  art field, and semantic states. In dark mode keep `#111216` shell,
  `#1B1A1D` canvas, `#242326` field, `#F4EEDF` primary, `#B8B1A8` muted,
  `#4B494B` rules, and an accessibility-safe smoky-indigo content accent instead
  of shell black. Preserve distinct coral/sage/bronze states. Approval requires
  a fresh 1586×992 dark render, computed-color/contrast audit for visible text
  and controls including interaction states, and exact light/dark geometry
  parity; source token declarations alone cannot clear this blocker.
- Latest surgical landing remediation (2026-08-22): evidence at
  `/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/ThisThisThis.pdf`
  remains unapproved only for feature-icon, divider, and date-gutter geometry.
  Feature 01 is the 32×32 optical authority; redraw feature 02 as a clean sheet
  with restrained lines and a non-colliding sage badge, and feature 03 as a
  compact bronze chart/podium with comparable mass. Use exactly three rule
  roles: strong blueberry section rule, neutral 1px inset vertical divider, and
  neutral 1px event hairline. Narrow and optically center the date stack, tighten
  day/month rhythm, and derive the hairline start from the date-divider geometry
  rather than a separate magic offset. Preserve the accepted composition,
  typography direction, palette, content, behavior, responsive/dark geometry,
  and all other families.
  Give each event row one simple semantic tone class so its action label and
  arrow match its status/dot: coral live, sage signup, bronze postponed/upcoming,
  and subdued bronze archived. Do not use generic all-blue or nth-child styling;
  verify accessible hover/focus contrast in both themes.
- Major functional slices still require the formal readiness review in
  section 4.1 below. Ordinary UI passes use the lean sequence: agree page/result,
  bounded implementation, one independent review when appropriate, focused
  remediation, and manual acceptance.
- F-04 is resolved by retaining Live identity and display timezone as read-only.
  The separate Live event-end correction remains a Schedule capability. F-06 is
  resolved by the approved five-step `/HowTo` guide in `50077fd`; no Rules
  editor, sixth step, or in-application HowTo editor is in scope.
- Stop before adding product behavior, changing an approved rule, adding
  unbudgeted persistence/routes/policies/jobs/abstractions, or fixing an
  unrelated defect. Preserve existing routes for deep links, reload/history,
  authorization, audit, concurrency, privacy, and historical records; no
  separate no-JavaScript parity work is planned or gated.
- Stage, commit, push, deploy, or archive additional legacy documents only
  after the relevant acceptance and explicit authorization.

The detailed procedures below are relocated from `AGENTS.md`; this is their active
owner. Use the subsection relevant to the assignment, not the entire checklist for
every correction. Sections 4.1 and 4.5 govern major functional-slice planning and
acceptance; sections 4.2–4.4 govern the changed scope and risk boundaries; section 4.6
retains applicable completion gates and small-UI/documentation exceptions. Explicit
user-approved pass gates, verification substitutes and waivers remain in force.
`UI_SYSTEM.md` owns the UI task/review protocol; the Admin popup source-only reviewer
exception in `AGENTS.md` remains applicable. Model/role defaults stay in `AGENTS.md`.


### 4.1 Functional-slice planning and readiness

Before splitting a functional slice into implementation passes, the planner maps
all approved user outcomes into a compact journey/coverage table in the existing
slice plan. Carry the manual steps into `MANUAL_TEST_CHECKLIST.md`; do not create a
new inventory document or duplicate the full product specification.
For a small correction, update the affected journey or state its outcome and proof
in the task prompt; do not introduce a full slice plan solely for this rule.

For each journey record:

- The actor and effective role, starting lifecycle/data, and actual UI entry point.
- The action sequence, expected persisted result, visible result, and next reachable
  step, including the destination emitted by a notification when applicable.
- Relevant boundary/recovery cases and the invariant each protects.
- The owning pass, planned proof at the failure boundary, and manual-only or blocked
  parts. Track execution against these outcomes, not just counts of passing tests.

Select variations from the affected behavior: zero/one/multiple records and valid
ties; global plus event roles; current plus retained memberships; EN/DA client and
server input; before/at/after time boundaries; stale/repeated/concurrent requests;
partial failure and retry; retained snapshots and changed current data. These are
prompts for relevant risks, not a mandatory Cartesian product or one test per case.
A happy path must reach a usable result. Correct rejection of invalid requests does
not establish that an authorized user can complete the action.

The planner owns coverage across passes and reconciles the final checklist against
every approved outcome. A journey omitted from the checklist is not implicitly
waived. Implementers and reviewers must flag gaps they discover. Trace changed
rules through their entry points and directly affected consumers, including derived
progress, completion, rankings, notifications, and retained-history reads where
applicable; a correct write alone does not prove those results agree.

After the user approves product behavior, run exactly one independent read-only
implementation-readiness review for a major functional slice. It compares the
complete proposed slice with current code and active authorities, challenges missing
journeys and assumptions, and assesses pass ordering and independent deployability.
It must establish:

- Real UI reachability and minimum Development reset accounts, roles, states, and
  records for acceptance. Fixtures must coexist and permit the documented sequence;
  do not bypass the behavior under test or add broad demonstration data.
- For removed/replaced behavior, a bounded inventory of affected domain values,
  persistence, services, routes, controls, notifications, seeds, tests, and authority
  wording so obsolete behavior cannot survive accidentally.
- A complexity budget of concrete new tables, services, pages/routes, policies,
  jobs, dependencies, and abstractions. Each addition needs a specific persistence,
  transaction, authorization, operational, or demonstrated reuse need.
- For fail-closed migrations/preflight, the exact operator diagnosis, safe record
  correction/adjudication, and retry path. Use retained-data rehearsal where required;
  never infer historical identity from mutable current state.
- Approved scope, explicit non-goals, necessary dependencies, optional suggestions,
  verification boundaries, and outstanding product decisions.

Resolve named decisions and update the plan before implementation. Do not repeat
readiness review without a genuine contradiction or missing product decision.
Ordinary implementation defects belong to bounded remediation. Optional suggestions
do not become requirements without approval.

### 4.2 Implementation and change control

Extend existing entities, services, pages, policies, and shared components before
adding abstractions. Preserve required invariants and protected interactions. Do not
add speculative frameworks, compatibility layers, or unrelated cleanup. Existing
route-backed recovery stays protected; separate no-JavaScript parity is not required.

Implementers may resolve ordinary technical details within the approved pass. Stop
for user direction before changing a product rule, broadening a pass, introducing
unbudgeted infrastructure, or fixing an adjacent issue not needed for safe delivery.
Record unrelated defects separately. A missing integration step required for an
approved journey is in scope, even when its owning file is outside the initial diff.
If the plan explicitly excludes a necessary change, report that conflict before
implementing it.

The final approved plan is the review baseline. Before implementing a user-approved
material change, update its affected pass, acceptance criteria, non-goals/complexity
budget, and relevant product/data/UI authority. A change is material when it affects
behavior, migration, authorization, routes, manual acceptance, or review conclusions.
Clarifications with no behavioral effect need no separate paperwork. Remediation
cannot silently revise the baseline or settle an unresolved product decision.

#### 4.2.1 Lean execution and planner handoff

The current roles, models and routing are owned by
[AGENTS.md](AGENTS.md#active-workflow-and-model-defaults--2026-10-05): the Claude
planner chat defines the work, writes the brief and reviews each batch independently;
the Codex planner chat named in the brief dispatches one implementer per batch with
the model the brief names. No orchestrator, Codex reviewer or verifier is used unless
a brief assigns one. Historical assignments elsewhere do not override this policy.

This is the reusable coordination procedure. Read it before dispatching work or
resuming as planner; apply the assigned pass's gates without creating extra stages.

1. Resume from `CURRENT_STATUS.md`, the assigned pass and the smallest relevant
   diff. Confirm the checkout, existing work and next authorized action. Reuse
   recorded checks and findings; changing planners does not restart discovery,
   implementation or review. Resolve only missing, changed or conflicting evidence.
2. Before implementation, turn the user's goal into a short, agreed fix list:
   observed problems, expected results, protected working behavior/composition,
   and the smallest checks that demonstrate the fixes. The planner owns this
   translation; the user need not identify files or technical causes. If problems
   are not yet known, perform one bounded inspection and return the concrete list
   before dispatching implementation. An already explicit user correction needs
   no extra approval round. Name the existing implementation to reuse; do not add
   a preliminary reviewer by default. Report additional discoveries separately
   unless they directly block the agreed fixes.
3. The dispatcher starts the implementer with the approved brief and exact model.
   The implementer completes a connected change and runs its checks. After one
   focused lookup, consequential uncertainty goes to the dispatcher with evidence and
   a recommendation; product and scope questions go on to the planner and the user.
   Continue independent authorized work if useful.
4. The dispatcher resolves ordinary execution questions and checks stalls and
   interrupted work. Repeated reading without progress calls for a narrower next
   step, not another open-ended continuation.
5. The implementer commits each item, runs the batch gate on the final commit and
   stops at the brief's boundary with its report. The planner then reviews the stable
   batch independently; findings return as a remediation brief to the same
   implementer, followed by a named recheck. Honor explicit user review waivers; do
   not claim skipped checks passed. For Admin popups, applicable review is source-only
   and visual acceptance is the user's.
6. At the stop boundary the implementer consolidates the active `CURRENT_STATUS.md`
   entry with the checkout/branch, assigned pass, completed work/checks and durable
   repository evidence locations (not only temporary paths), unresolved findings,
   acceptance state and exact next permitted action. Link this procedure; do not
   copy it into the handoff or rely on chat history. Preserve approval authority in
   `UI_PAGE_MATRIX.md`.

Use this compact worker brief; include only relevant facts and authority sections:

```text
Role and approved model/reasoning:
Planner and dispatcher chat IDs; worker identity if known:
Checkout / branch:
Observed problems and expected results (agreed fix list):
Starting files/helper and established evidence:
Protected behavior / non-goals / relevant authority sections:
Required checks, batch gate and stop boundary:
Escalation: after one focused lookup, send consequential uncertainty to the
dispatcher with evidence and a recommendation; product and scope questions go to
the planner and the user.
Return: commit SHAs, changed files, checks/results, unresolved findings and next
permitted action.
```

### 4.3 Verification at affected boundaries

Derive verification from the journey outcomes and affected risks before minimizing
commands or assertions. A required journey or relevant integration boundary without
proof is a concrete uncertainty. Use existing discriminating tests where they cover
it; one scenario may prove several consecutive steps or related invariants.

Match evidence to the boundary that can fail:

| Risk | Required kind of evidence |
| --- | --- |
| Route, filter, authorization, binding, or navigation | Requests as the intended actor, including anonymous users where applicable, through the real pipeline; follow rendered links/forms and emitted destinations where required. Direct handler calls cannot prove filter or navigation reachability. |
| Client validation, localized form input, or DOM interaction | Execute the affected behavior in a browser. Source/markup and HTTP proof cover only their own layers; they cannot establish client acceptance. |
| Persistence, transaction, concurrency, or retained migration | Exercise the relevant PostgreSQL behavior, failure/retry boundary, or approved copied-data rehearsal. In-memory success cannot establish database guarantees. |
| Derived results or retained reads after mutation | Check the directly affected consumers and usable rendered result, including valid multiple-record cases where applicable. |
| Visual composition and usability | Inspect current rendered evidence under the UI protocol; source checks cannot establish visual acceptance. |

Use controlled/disposable data and existing fixtures without touching user-owned or
production data beyond authorization. If an environment prevents the required proof,
report exactly which outcome remains unverified and the smallest way to execute it.
Alternative source or lower-layer checks may narrow uncertainty but do not turn the
blocked boundary into a pass. The user may explicitly accept a substitute or waive a
named case; preserve that decision and do not revive it without new evidence.

A required test must fail for the plausible defect it claims to prevent. Check that
its setup actually reaches the relevant actor, state, and operation and its assertions
observe the outcome. Avoid assertions that merely mirror implementation, exact counts
unrelated to behavior, or duplicated coverage at every layer. Do not weaken a failed
test as “stale” without establishing the approved contract and replacement proof.
Expand coverage for concrete security, privacy, concurrency, data-integrity, migration,
or escaped-regression risks. Run focused gates per pass and the complete suite at the
documented final gate or when the blast radius warrants it.

### 4.4 Independent review and bounded remediation

Use the existing required readiness/post-implementation review gates; these rules do
not add a separate reviewer per risk or require repeated model agreement. The reviewer
first derives expected journeys, invariants, and plausible failures from the approved
behavior and current authorities, then evaluates the implementation and its evidence.

Review the exact base-to-current diff against the final plan and inspect unchanged
entry points and direct consumers when needed to trace changed behavior. The diff is
the change inventory, not the limit of behavior inspection. Report:

- Required scope delivered and missing, material additions without approved mapping,
  changed non-goals, and unbudgeted tables/services/routes/policies/jobs/abstractions.
- Concrete behavior/security/privacy/authorization/concurrency/data-integrity defects,
  non-discriminating required tests, and required outcomes lacking sufficient proof.
- For each finding, the triggering actor/state/sequence, expected and observed or
  source-inferred result, evidence, and smallest necessary correction.

A plan omission is reportable; distinguish an implementation defect within approved
behavior from an unresolved product decision requiring the user. Do not invent new
requirements or demand stylistic expansion, redundant assertion syntax, or exhaustive
duplicate tests. Incidental supporting code/tests/docs need proportionate scope
justification, not a separate plan bullet for every file.
Missing approved behavior, unapproved material additions, concrete correctness
defects, and inadequate required proof block acceptance until resolved or explicitly
adjudicated by the user. Optional improvements do not block it.

A fresh remediator addresses named findings. Verify the fix at its failure boundary,
rerun the affected functional journey, and check direct consumers where the correction
can change them. Small visual corrections follow the UI exception in `AGENTS.md` and `UI_SYSTEM.md`.
Follow any required fixes-only review gate. Reopen wider review only for a concrete
new implication, contradiction, or material scope change. Do not use repeated broad
reviews as a substitute for executing a missing journey.

### 4.5 Functional-slice preflight and handoff

After required review/remediation clears, perform one bounded preflight against the
exact checklist and authoritative Development reset state before asking the user to
walk the slice. Reuse applicable executed evidence; do not rerun every passing check.
Follow each journey from its documented start through actual rendered navigation,
forms, and role-appropriate notification destinations, verifying a renderable result
and the next step. Use browser execution where client behavior can block the journey.
Do not manufacture destination URLs or ready database states to skip required steps.

If a journey fails, stop that journey, route the smallest authorized remediation to
the appropriate role, rerun it, and resume the remaining preflight. Subjective visual
clarity, responsive composition, and wording remain manual-only unless they prevent
completion. Hand over only when required executable journeys pass or the user has
explicitly accepted their named limitations. A recorded blocker alone is not acceptance.

Handoffs distinguish **implemented**, **source-reviewed**, **execution passed**,
**blocked/unverified**, and **manually accepted or explicitly waived**. State the exact
revision or working-tree scope, commands/scenarios and results, evidence limitations,
and next permitted action. A generic “PASS,” test count, or approved screenshot does
not establish all of these. The planner reconciles coverage before claiming the
slice complete; omitted or blocked required outcomes remain open.

Stop when approved outcomes exist, applicable verification and acceptance gates are
satisfied, and no unresolved finding could materially change correctness, security,
privacy, authorization, concurrency, data integrity, or the requested result. Do not
continue for extra corroboration, reviewer agreement, or speculative improvements.

### 4.6 Completion gates

Before reporting implementation work complete:

- Every approved journey has its required result through the appropriate layers;
  the planner has reconciled coverage and reported any explicit user waiver.
- Relevant tests were added or updated and pass.
- The release build passes with no unexpected warnings.
- Formatting verification passes.
- Authorization, validation, audit, concurrency, privacy, and lifecycle effects were considered where relevant.
- UI changes satisfy the applicable roadmap checks and user approval gate.
- Database changes include consistent migrations and were exercised against PostgreSQL.
- Documentation and `CURRENT_STATUS.md` were updated if behavior, architecture, commands, roadmap position, or known verification state materially changed.
- Required independent review is clear, and executed checks are distinguished from
  source review and manual acceptance. Unrun required checks remain open unless
  explicitly waived; report all limitations using the handoff rules above.

Apply the documented small-UI exception where appropriate. Documentation-only work
uses scoped diff, consistency, and reference checks; it does not require .NET gates.

Milestones are complete only when their documented completion criteria are satisfied—not merely because corresponding files or pages exist.

### 4.7 Branch cleanup and publication

Use concise purpose-based branch names such as `ui-overhaul`, `fix/button-colors`,
or `release/production-prep`; do not add an agent-identifying prefix unless the user
requests it. When practical, settle the branch name before its first push; renaming
is not a code/history change. Preserve an explicitly assigned checkout/branch.

Before deleting old local or remote branches, produce a bounded read-only inventory
separating fully merged branches, branches with unique commits and active branches.
Delete only the exact branches then authorized by the user. Removing a merged branch
pointer does not remove its commits or rename historical merge/PR records. Staging,
committing, merging, pushing and deploying each require applicable authorization.


## 5. Admin event-functionality correction — approved pass plan (2026-09-03)

### 5.1 Scope, baseline, and stop rule

This is one event-scoped functional slice, implemented on
`admin-event-functionality`. It corrects what an Admin can do to one event and
its retained records across the lifecycle. It is not an Admin UI redesign;
markup or interaction changes are permitted only when required to expose an
approved action, confirmation, reason, validation result, or recovery path.

The complete base-to-current implementation is reviewed against this section
and the authoritative contracts. Preserve authorization, audit history,
optimistic concurrency, privacy, transactional mutation, immutable competitive
history, and existing route-backed recovery. Do not change global Admin
behavior, public-page composition, artwork, catalogue/tile behavior, account
management outside an event, event-state vocabulary, or post-draft signup
answer/account editing. Version-one question-visibility toggles remain a
superseded capability and are not implemented, repaired, or removed in this
slice. Admin-created participants remain blocked after draft start.

An implementer may resolve ordinary technical details inside a pass, but must
stop before changing an approved product rule, adding an adjacent fix, or
introducing unbudgeted persistence, routes, policies, jobs, services, or
abstractions. Each pass must remain independently testable and leave the branch
in a coherent state.

### 5.2 Hard schedule requirement

The following matrix is an implementation and final-review requirement, not an
illustrative summary. `SignupClose` reopening follows the same retained-future
boundary rule as resuming an event: reuse an existing future value; require and
confirm a replacement only when the retained value is missing or expired.

| Event lifecycle | Signup opening | Signup closing | Draft time | Event start | Event end | Capacity |
| --- | --- | --- | --- | --- | --- | --- |
| Private Draft | Editable while future | Editable while future | Editable while future | Editable while future | Editable while future | Editable |
| Signup Open | Locked history | Editable while future | Editable while future | Editable while future | Editable while future | Editable |
| Signup Closed, draft not started | Locked history | Reopen action only | Editable while future | Editable while future | Editable while future | Editable |
| Draft Running or Paused | Locked | Locked | Locked | Locked | Locked | Locked |
| Draft Finalized, pre-Live | Locked | Locked | Locked | Editable while future | Editable while future | Locked |
| Live | Locked | Locked | Locked | Locked history | Editable to a future value with confirmation and reason | Locked |
| Awaiting Final Review | Locked | Locked | Locked | Locked history | Resume reuses a retained future end; otherwise requires a confirmed replacement future end | Locked |
| Finalized, Archived, or Cancelled | Locked | Locked | Locked | Locked | Locked | Locked |

Every changed timestamp must be future; passed boundaries cannot be changed or
cleared, while an unchanged historical value remains valid. Signup close must
remain between opening and start, end must follow start, and published start/end
cannot be cleared. Overlap and linked Wise Old Man schedule matching are
revalidated. Changing event end atomically derives the normal submission cutoff
as end plus 30 minutes; the cutoff is never directly edited. Reopening
submissions remains the separate Final Review action.

### 5.3 Pass 1 — schedule and postponed lifecycle recovery

**Outcome:** Schedule editing and start/resume recovery exactly match section
5.2 and `FUNCTIONAL_CONTRACTS.md` section 4.4.

- Enforce the approved Live identity boundary end to end: identity and display
  timezone are read-only in the domain, rendered Admin surface, and direct POST
  handling. Remove the obsolete post-start timezone-reason path and update only
  its directly affected tests and resources. This is not an Identity redesign.
- Replace the broad post-draft schedule lock with lifecycle- and draft-state
  permissions from the matrix. Running and Paused retain the complete lock;
  Draft Finalized pre-Live permits only still-future event start/end changes.
- Preserve the current signup-reopen behavior that reuses a retained future
  close and requires confirmation of a replacement only when it has expired or
  is absent.
- Permit a Live event-end change only to another future time, with server-side
  confirmation and a required reason. Revalidate all schedule invariants and
  atomically derive the ordinary submission cutoff.
- Make Resume reuse a retained future end. Require a replacement future end
  only when the retained value is missing or expired; always require strong
  confirmation and a reason.
- Recover a postponed scheduled start using actual lifecycle facts rather than
  `now < EventStartsAt`: while the event has never actually started and its end
  remains future, allow required roster correction, draft
  finalization/reopening/refinalization, initial board publication, and manual
  start. Preserve the missed scheduled start as history. Reject start when the
  configured end has passed and direct the Admin to cancel or replace the event.
- Make capacity mutation and its audit record one transaction and one audit
  entry; do not retain a service commit followed by a separate fallible audit.
  Every resulting promotion notification must include the promoted participant
  ID so its recipient can follow the durable Confirmation destination without
  transient `TempData`.
- Update only the bounded Development reset fixture needed for acceptance:
  TEST 05 must be genuinely pre-Live and never started, with a configured start
  in the past, an end in the future, and unresolved draft/board blockers. The
  Admin must clear those blockers through existing rendered routes before
  manually starting the event.

Focused proof must discriminate the matrix boundaries, delayed-start recovery
through the real Admin handlers, retained-future and expired-boundary reopen and
resume cases, Live end/cutoff behavior, rejection after end, overlap/Wise Old
Man revalidation, Live identity/timezone rejection, capacity rollback/audit
uniqueness, and a promotion notification followed as its recipient. A direct database
fixture that silently creates a ready draft or published board does not prove
the postponed-start recovery path.

### 5.4 Pass 2 — participant metadata and ownership

**Outcome:** Administrative corrections remain available for as long as their
business purpose remains valid without reopening unrelated participant edits.

- Keep private payment status and Admin notes editable in every retained visible
  lifecycle, including Draft Running/Paused, Live, Awaiting Final Review,
  Finalized, Archived, and Cancelled. Hidden and Discarded records remain
  inaccessible.
- Narrowly allow the terminal participant-detail route and only the handlers
  needed for payment/notes. Continue hiding and server-blocking answer, account,
  status, roster, and other lifecycle-inappropriate controls.
- Allow participant ownership transfer through Awaiting Final Review with
  strong UI and server confirmation, concurrency protection, atomic history,
  access revocation, and reachable notifications. The new owner retains the
  participant Confirmation destination; the former owner receives the existing
  account-owned My Events destination because participant access has already
  been revoked. Reject transfer in Finalized, Archived, Cancelled, Hidden, and
  Discarded.
- Preserve all existing correction, withdrawal, restoration, internal-add, and
  account/answer freeze rules not explicitly changed above.

Focused proof covers each newly reachable lifecycle, direct-POST rejection for
unapproved controls/states, concurrency, atomic audit, preserved record history,
immediate authority transfer, and both notifications followed as their intended
recipients. No new participant table, service, page,
route, or generalized terminal-event framework is budgeted.

### 5.5 Pass 3 — operational integrations, evidence authority, and atomic audit

**Outcome:** Event-scoped operational settings remain editable only while their
effects can still be used, and every accepted mutation is atomic with history.

- Preserve and regression-check the existing reachable Live Wise Old Man
  competition replacement rather than reimplementing it. It must reference an
  existing competition whose boundaries match exactly as UTC instants, cannot clear
  the link, cannot synchronize the schedule, invalidates the prior displayed
  cache only after success, and leaves state unchanged on failure. Change it
  only if focused proof exposes a concrete contract gap.
- Permit evidence-code configuration in Private Draft, Signup Open, Signup
  Closed, and Live. In Awaiting Final Review, permit it only while the active
  submission window accepts uploads. Freeze it after upload closure and in
  terminal, cancelled, hidden, and discarded states; retain interval snapshots.
- Permit captain/co-captain assignment, promotion, demotion, and revocation
  after draft and through Live. In Awaiting Final Review, permit changes only
  while the active submission window accepts uploads. Freeze them afterward,
  while preserving history, notifications, and withdrawn-member behavior.
- Make reopen-submissions, evidence-code mode/value changes, and evidence-code
  creation atomic with their audit entries. An audit failure must roll back the
  business mutation, and one successful action must produce one history record.

Focused proof covers each positive and negative lifecycle/window boundary, Wise
Old Man failure-without-mutation, interval preservation, captain authorization,
and mutation/audit rollback. Reuse the existing event policy and services; no
new table, page, route, policy, job, service, or abstraction is budgeted.

### 5.6 Pass 4 — board publication and correction safeguards

**Outcome:** Publishing public competitive state is deliberate and cannot be
continued from an obsolete or terminal Admin workspace.

- Require an explicit popup confirmation and an independently validated
  server-side confirmation value for both initial publication and publication
  of a corrected replacement board. A missing or stale confirmation produces
  no mutation.
- Retain the existing confirmation/reason boundary for starting board
  correction.
- Permit corrected publication only in Signup Closed, Live, and Awaiting Final
  Review. Reject Cancelled, Finalized, Archived, Hidden, and Discarded events at
  the handler/service boundary and prevent an already-open correction workspace
  from bypassing the current state.
- Keep the previously published snapshot authoritative until the replacement
  publication succeeds, and preserve every superseded snapshot and audit record.
- Use Pass 1's postponed-start recovery for late initial publication; do not
  duplicate schedule policy here.

Focused proof covers no-confirmation/no-mutation, allowed lifecycle states,
direct POST and stale-workspace rejection, snapshot continuity, and history.
No board-editor redesign, catalogue behavior, tile semantics, artwork, new page,
or new route is in scope.

### 5.7 Execution, review, and release gates

1. The one independent Sol High implementation-readiness review completed on
   2026-09-03. It checked real UI reachability, Development reset states and
   accounts, obsolete behavior inventory, pass ordering, independent
   deployability, and the complexity budget. Its five required corrections are
   incorporated in Passes 1–3 and the active question-visibility wording; no
   unresolved product decision or architecture blocker remains.
2. Do not repeat the readiness review unless implementation exposes a genuine
   contradiction or missing product decision.
3. Implement passes sequentially with bounded Luna Max implementer tasks. Each
   pass stops after its scoped implementation and focused tests; it does not
   begin the next pass, review itself, remediate unrelated defects, package, or
   publish. The planner checks scope and test evidence before requesting the
   user's next-pass authorization.
4. Because Pass 1 materially crossed lifecycle, authorization, transaction,
   notification, Admin-handler, and recovery-test boundaries, the user approved
   one risk-based independent Sol High review of the complete Pass 1 diff before
   Pass 2. The review completed on 2026-09-04 and found two blockers: Wise Old
   Man schedule synchronization could bypass the draft lock, and the promotion
   notification test did not follow its stored destination. Bounded Luna Max
   remediation restored the aggregate guard and added discriminating locked-
   draft synchronization plus authenticated notification-destination coverage.
   The affected Release build, 11 Wise Old Man tests, one promotion-destination
   test, and `git diff --check` pass. A fixes-only Sol High re-review then passed
   with both findings resolved and no remediation-local defect or scope
   expansion. This does not establish an automatic per-pass review requirement.
5. After all four passes, run one independent Sol High base-to-current review
   against the final updated plan. Any remediation uses a fresh bounded Luna
   Max task and changes only named findings.
6. Run one manual-acceptance preflight through the real rendered Admin links and
   forms using authoritative Development reset data. Every required account,
   role, event state, record, control, notification destination, and next step
   must be reachable without constructing hidden destination URLs.
7. Per-pass verification is focused and non-duplicative. Run the complete test
   suite once, after all passes and remediation, before packaging. Staging,
   committing, pushing, opening/updating a PR, merging, and production promotion
   each require their own later user authorization.

**Complexity budget:** zero new database tables or migrations; zero new pages or
routes; zero new authorization policies, background jobs, services, compatibility
layers, or generalized state/control frameworks. Extend existing event entities,
domain/application services, state policy, Admin pages/handlers, dialogs, audit
transactions, and focused tests. Any discovered need to exceed this budget is a
stop condition for user review, not an implementation detail.

## 6. Admin-test follow-up corrections — approved implementation contract (2026-09-05)

The read-only investigation is complete. It source-traced the complete
submission/review lifecycle, actor, state, duplicate, progress, privacy, audit,
notification, and realtime paths plus every recorded Admin-test observation. One
disposable Testcontainers reproduction stopped after Docker could not access the
Docker socket; no user-owned database was touched. The user approved the product
decisions below. Implementation remains unauthorized until the required single
independent readiness review clears this final contract.

### 6.1 Final authority and investigation findings

- Global Admin and SuperAdmin roles are additive to genuine event roles. An
  Admin/SuperAdmin with an ordinary Participant, Captain, or Co-captain role has
  exactly that role's team visibility and submission authority, subject to the
  ordinary lifecycle and cutoff. Global role alone never grants team access,
  submission creation/replacement/withdrawal/resubmission, focus mutation, or
  another team's private evidence. Admin review remains a separate capability.
  Automatic Captain status and any new general cross-team submission inspection
  or correction mode are explicitly deferred; the existing explicit, read-only
  SuperAdmin team-focus inspection remains unchanged.
- A Reversed submission remains immutable and cannot be directly re-approved.
  While the ordinary or explicitly reopened upload window is open, it may have
  exactly one linked corrected child with a new image. The child is Pending and
  follows ordinary review. Its later approval creates a new contribution while
  the reversed contribution stays inactive; predecessor, approval, reversal,
  replacement, and review history remain linked. A closed upload window requires
  the existing reasoned Admin reopen action.
- Duplicate-disabled drop requirements are keyed by immutable shared catalogue-item
  identity within the requirement, not by boss/source-specific `SourceDropId`,
  screenshot checksum, or submission ID. The same item may legitimately satisfy
  separate sibling requirements. Multiple eligible source rows for one item remain
  valid alternatives within one requirement. Allocation groups by `(TeamId,
  RequirementId, ItemIdSnapshot)`: a missing explicit cap has effective value `1`,
  every alias must have the same effective cap, and inconsistent aliases block
  board approval. Duplicate-enabled requirements continue to count eligible copies
  until the requirement target or source-specific explicit cap is reached.
- One approved migration is budgeted to freeze catalogue-item identity into the
  event and approval drop snapshots. Existing rows are backfilled only when frozen
  snapshot facts identify one safe item. A preflight must fail closed on ambiguous
  or mismatched rows and report event, requirement, source-drop ID, frozen item
  name, and current mapping so an operator can correct the record before retrying.
  Reading mutable current catalogue identity as historical truth is forbidden.
  Extend the existing operator preflight/migrate surfaces rather than add a service:
  preflight emits a deterministic external mapping template and database
  fingerprint; the operator supplies an adjudicated catalogue-item ID for each
  flagged row and confirms the mapping-file hash. The migration command validates
  the fingerprint, row IDs, and item IDs, loads them into a connection-scoped
  temporary table, and runs the one EF migration on that same open connection. The
  migration auto-fills only unambiguous rows, consumes required staged mappings,
  verifies both snapshot families are complete, and then makes the columns
  non-null. The temporary table disappears with the session; neither frozen names,
  current `SourceDrop` mappings, nor retained schema are altered by adjudication.
- Submission creation/editing and every review, correction, rejection, approval,
  reversal, and linked-resubmission action require the main immutable `AuditEntry`
  in the same transaction as the existing submission-local `ReviewAction` and any
  required notification.
- Each objective on a tile remains isolated by `RequirementId`. Completing,
  approving, reversing, or rebalancing one requirement cannot contribute to or
  close a sibling. Retargeting a Pending submission must recompute and store the
  selected requirement/drop's authoritative weight; it must never carry the old
  target's weight into the sibling objective.
- Wise Old Man HTTP/JSON parsing and typed decimal caching are already invariant.
  Signup and the complete My Accounts Add/Edit/fetch journey must use invariant
  machine transport with localized display and form input. Onboarding remains in
  Danish/English regression proof even though source inspection found its current
  path culture-consistent. Admin competition synchronization is not part of this
  browser form-culture defect.
- Draft finalization must use the existing system-owned primary Playing-character
  authority. A valid primary plus another regular Playing character is not
  ambiguous and must not block finalization.
- The existing evidence popup remains. Board/Team Board and Admin Review evidence
  images gain click-focused toggle magnifier zoom plus pan inside that popup, including
  keyboard, pointer/trackpad, and practical touch support. Do not render a separate
  zoom-control bar. Focus, Escape/backdrop close, responsive sizing, themes, and
  reset-on-close remain accessible. No media dependency or generalized viewer
  framework is permitted.

The authoritative lifecycle matrix is:

| Boundary | Participant/Captain mutation | Admin review | Retained history |
| --- | --- | --- | --- |
| Pre-Live | Closed | Closed | Preserved when present |
| Live before official end | Open for role-authorized current-team evidence | Open | Preserved |
| After end through upload cutoff | Only in-window drops may be uploaded/edited | Open | Preserved |
| After cutoff | Closed | Open while Live/Awaiting Final Review | Preserved |
| Explicitly reopened uploads | Open only until the new cutoff; official drop-acquisition end is unchanged | Open | Preserved |
| Awaiting Final Review | Controlled only by the active/reopened upload window | Open | Preserved |
| Finalized | Closed | Closed | Read-only |
| Unfinalized | Returns to Awaiting Final Review; uploads remain closed until separately reopened | Open | Read-only except approved review actions |
| Archived/Cancelled/Hidden/Discarded | Closed | Closed through ordinary review routes | Retained under lifecycle/privacy rules |

Only Pending evidence may be edited, corrected, approved, or rejected; only
Approved evidence may be reversed. Withdrawn and Replaced predecessors are
read-only. Rejected and Reversed predecessors may each have at most one direct
linked Pending child while uploads are open. Stale or repeated review requests
must not create a second contribution, child, audit, notification, or progress
effect. An archived former credited owner may see only their own retained
Rejected/Withdrawn history; former teammates and cross-team viewers remain denied.

### 6.2 Implementation passes

1. **Shared culture and additive-role boundaries.** Correct Signup and My Accounts
   decimal transport once at the owning rendered/postback boundaries, preserving
   localized editing and the invariant WOM client. Make `EvidenceAuthority`, the
   shared header, Team Board, canonical `/Submissions` routes, and submission
   handlers compose genuine Participant/Captain/Co-captain capability with global
   Admin/SuperAdmin capability. Prove Admin-only, Admin-plus-Participant,
   Admin-plus-Captain, and Admin-plus-Co-captain behavior without cross-team access
   or lifecycle bypass. Extend Development reset with only the stable global-only
   and additive-role accounts needed for the route-ordered manual journey.
2. **Immutable item identity and duplicate enforcement.** Add the one approved
   snapshot migration and fail-closed operator preflight. Carry immutable item
   identity through Board editing, event publication, corrected publication,
   approval snapshots, submission validation, approval caps, reversal/rebalancing,
   public progress, and rankings. Preserve legitimate source alternatives, validate
   one consistent effective item cap before approval, and prevent excess
   contribution at the server boundary while preserving duplicate-enabled
   requirements and the same item in separate sibling objectives. Exercise the
   operator preflight/adjudicated-map/migration/retry path against a copied retained
   database, never the user's working database.
3. **Submission and review correctness.** Add atomic main-audit entries; recompute
   authoritative weight on participant/Captain and Admin retargeting; implement the
   approved one-child Reversed correction path; preserve Pending/Approved review
   boundaries, immutable attempts/assets, stale/concurrent idempotency, rejection
   notifications, progress and focus invalidation, and predecessor history. Add the
   narrow archived credited-owner read path and hide lifecycle-invalid Admin Review
   controls while retaining service rejection. Include `/Evidence/{assetId}` in the
   same status-aware authorization boundary. Seed only the Reversed predecessor and
   genuine archived former credited-owner records needed for manual acceptance.
   This is not a submission/review UI redesign.
4. **Draft and bounded presentation corrections.** Reuse the primary-character
   query in finalization and add the minimal not-yet-finalized Development reset
   state for a valid primary plus secondary Playing character. Suppress orphaned
   Signup `03`; on a fresh signup default only the required system primary account
   to the preferred character while every later account question starts at
   None/unselected. Route the Admin logo to public home. Keep the existing public
   `/Events/{slug}/Teams` roster as the pre-board landing destination, then, only
   after Board publication, add it as the localized Teams/Hold sibling of
   Boards/Drops/Leaderboards without copying the Board masthead or otherwise
   changing the approved Teams page. Use the shared reduced navigation-to-masthead
   spacing and keep Board routes unavailable before publication. Align Onboarding's
   joined EHB/Wise Old Man control with My Accounts' text-color-only fetch hover and
   apply invalid styling to the complete joined control. Enhance the existing
   evidence dialogs with accessible zoom/pan.

Each pass stops at its named boundary, receives focused discriminating tests, an
independent review, and fixes-only remediation for concrete findings. Tests must
cover English/Danish round trips; additive versus implicit authority; shared-item
duplicates under both flag values; migration preflight; one tile with fulfilled
and incomplete sibling objectives; retargeted weight; lifecycle/state/actor
submission and review boundaries; audit atomicity; reversal/resubmission history;
primary-character finalization; and the exact markup/navigation/zoom behavior.

### 6.3 Complexity budget, gates, and stop rules

- Budget: zero new tables, pages/routes, services, authorization policies, jobs,
  dependencies, compatibility layers, or generalized frameworks; exactly one EF
  migration plus its designer/model snapshot for immutable catalogue-item identity.
- Reuse current role facts, routes, submission/review service, audit domain,
  notification path, progress calculators, primary-character query, Development
  reset, native evidence dialogs, existing operator preflight, and `--migrate`
  surface. A connection-scoped temporary mapping table is migration mechanics and
  leaves no retained table; no new operator service is introduced.
- No general identity/authorization redesign, Admin/SuperAdmin implicit team access,
  new SuperAdmin submission-inspection mode, Board redesign, submission/review UI
  redesign, new image storage, translation overhaul, OCR, or evidence-report system
  is in scope.
- Before implementation, run exactly one independent read-only readiness review of
  this final contract against current code, real route reachability, Development
  reset, migration/preflight operability, pass ordering, and complexity budget.
- Implementation stops for user direction before changing these product rules,
  deriving historical item identity from mutable catalogue state, adding an
  unbudgeted artifact, or correcting an unrelated issue. Packaging, commit, push,
  merge, deployment, and release each remain separately unauthorized.

### 6.4 Independent readiness review — corrections resolved (2026-09-05)

The single required independent read-only review initially returned **NOT READY**
on four bounded contract gaps. The user then approved objective-scoped shared-item
caps, and the final contract now resolves every named blocker without repeating the
one-time review:

1. Duplicate-disabled caps are frozen per `(team, requirement, item snapshot)`;
   alternative sources remain valid, null means `1`, inconsistent effective alias
   caps block board approval, and sibling objectives remain independent.
2. The existing preflight and migrate surfaces own deterministic external mapping,
   hash/fingerprint confirmation, same-connection temporary staging, migration
   consumption, and post-backfill verification without altering frozen/current
   catalogue facts or leaving a table.
3. `MANUAL_TEST_CHECKLIST.md` now owns one compact pending Section 6 journey, and
   each implementation pass owns only its minimum missing reset fixtures.
4. Active product/data/architecture authority now includes the narrow archived
   former-owner Rejected/Withdrawn detail and asset exception, Reversed predecessor,
   and status-aware `/Evidence/{assetId}` compatibility boundary.

The planning gate is therefore **READY**. Implementation still requires the user's
separate authorization and begins only with Pass 1.

### 6.5 Pass 1 implementation/review checkpoint — 2026-09-05

The user authorized Pass 1. The bounded implementation now uses request-culture
display/binding with invariant machine transport for Signup and My Accounts,
resolves genuine event membership before the global Admin fallback in the existing
evidence authority, preserves global-only denial and separate Admin/SuperAdmin
capabilities, and adds only the minimum deterministic additive-role Development
fixtures. No new table, migration, service, route, policy, job, dependency, binder,
framework, or generalized abstraction was added.

The independent reviewer accepted the role/navigation/privacy/lifecycle scope and
found two culture-proof defects: missing Danish messages for the invariant Signup
parser and a markup-only Onboarding assertion. Fixes-only remediation added the two
shared Danish resources and a real English/Danish Onboarding fetch/render/postback
test. After replacing a contaminated long-test validation sequence with the simpler
focused boundary, both Signup/My Accounts culture cases and both Onboarding cases
pass. Earlier Pass 1 verification also passed the Web, BrowserTests, and
IntegrationTests Release builds, three focused browser tests, six focused
integration cases, and `git diff --check`. A fresh fixes-only closure review found
one remaining test-only gap: the two Danish parser resources lacked direct
localization assertions. The existing Danish localizer test now asserts both exact
keys and values; that focused test passes 1/1 and `git diff --check` passes.

Pass 1 implementation/review/remediation closure is complete. AF-01's manual-
acceptance preflight and user walkthrough remain outstanding. Stop here; do not
begin Pass 2 or package/commit/push/merge/deploy without the next authorized gate.

Manual AF-01 subsequently exposed two browser/reachability gaps that direct HTTP
coverage had missed. My Accounts and Onboarding now use the same localized
text/decimal boundary without the invariant client number validator; Signup retains
its separate visible-localized/hidden-invariant transport. General Participant and
Captain header navigation each prefer the sole Live team, otherwise the sole
Awaiting Final Review team; Captain includes co-captain and valid enabled Emergency
Captain access. Retained Finalized/Archived memberships no longer hide a current
link, while genuinely ambiguous eligible teams expose no shortcut. The four EN/DA
culture cases, focused navigation integration test, and `git diff --check` pass.

The user manually accepted the reachable Signup, My Accounts, additive-role, and
header journeys on 2026-09-05. Local Discord OAuth cannot reach Onboarding because
its callback is not localhost; the user accepted the discriminating EN/DA rendered
control and persisted-comma integration proof in place of that manual step. These
were fixes to approved Pass 1 behavior, not added scope. **Pass 1 is complete.**
Stop here; Pass 2 and package/commit/push/merge/deploy remain separately gated.


## 7. Admin consistency — approved signup-question popup pilot (2026-09-07)

The user prioritizes Admin UI consistency and actual reuse of components and
behavior. Preserve the accepted public UI, except focused regression where an
Admin-owned change has public consumers. Discovery is complete; the user approved the popup behavior, confirmation and
typography contract and this first implementation pilot. Work in the isolated checkout on
`codex/admin-consistency`, based on merged/deployed `42b2a7d`.

Use the existing UI_SYSTEM primitive registry and UI_PAGE_MATRIX family map;
do not replace them with a new inventory/specimen system. A bounded route scan
found 23 Admin Razor page entries excluding the two historical reference/specimen
routes; this includes the existing WIP dashboard and compatibility/preview entries,
not 23 independently verified workflows. Existing page approvals remain intact.

| Pattern / area | Source evidence | Next relevant coverage |
| --- | --- | --- |
| Shell, tokens and feedback | Physical shared Admin layouts, Admin CSS tokens, shared toast partial/site.js already exist | Preserve ownership; inspect affected rendering/focus only when changed |
| Directory controls / tables | Events and Accounts reuse CSS but render their own toolbars/table markup; Audit and Review compose filters separately | Compare realistic populated/empty/filter states; choose actual repeated subcontrols before extracting; keep page-specific columns |
| Fields / action groups | `.admin-field` and button CSS reused across Identity, Schedule and Questions; page composition owns markup | Validate consistent grouping, validation placement, pending/locked controls on the selected pilot |
| Route dialogs | Three lifecycle owners: Questions, Catalogue, Accounts | Shared close/focus/scroll/history behavior; preserve route/host distinctions and desktop-to-narrow navigation |
| Save / recovery | site.js has existing enhanced POST machinery; Catalogue uses it, Accounts/Questions implement transport separately | Preserve entered values on failure and usable retry; prevent repeated submission; keep cross-path routing guard |
| Specialized workspaces / review / closeout | Board, Draft, Catalogue, Review, Finalize have distinct tasks; existing matrix owns their geometry/status | Later workflow-specific passes; preserve workspace spatial context, role/state locks, evidence/history and public result consumers |

Read-only independent behavior findings:
- Catalogue close callback reads `opener` after it is cleared (`catalogue-admin.js`
  284–285); source defect verified, actual focus loss may be masked by native dialog
  restoration and requires rendered verification.
- Questions POST transport failure replaces the editor with an alert, removing
  entered controls and the visible close/retry controls (`signup-questions-overlay.js`
  138–155). DOM loss is source-confirmed; no live fault was injected.
- Shared details-confirmation mechanics are copied across three modules; Accounts
  moves focus to Cancel while Catalogue/Questions do not.
- Generic POST helper already handles submitter values, duplicate-submit guards,
  control restoration and content updates. Questions responses have a different
  path from their host, so blindly applying `data-update-targets` would conflict
  with the existing same-path guard in site.js. Preserve that boundary.

The user approved a single Signup Questions pilot after agreeing intended behavior;
existing pages are evidence, not automatic design targets. UI_SYSTEM owns the
accepted reusable popup rules. Later rollout and shared behavior extraction await
manual acceptance of this pilot.

### Approved pilot scope and outcomes

Actor/entry: authorized event Admin, existing editable Development event, navigate
through Admin Events -> event Participants -> Signup form. Preserve actual route,
authorization, handler names, stored question/answer semantics and locked states.

| Journey | Required result and proof |
| --- | --- |
| Open, close, reopen; direct route and reload | Predictable dialog/fallback, correct URL, focus and body scroll; browser proof |
| Add/edit/save valid question | Keep this multi-question editor open, update list/count, one result notice, next edit reachable |
| Invalid input or failed request -> correct/retry | Preserve all unsaved editor values, usable localized feedback and retry, no duplicate save |
| Close/Cancel/Escape/Back/outside click with unsaved edits | Compact inline discard choice; cancel retains edits and open URL, discard leaves expected parent; unchanged close has no prompt |
| Save pending -> attempted dismissal | Keep editor present until request settles; do not imply aborting a request cancels a server write |
| Delete question -> cancel/confirm | Compact inline confirmation, Cancel before Delete question, consequence copy specific to Account vs other types; Escape dismisses only the active confirmation |
| Other dirty question forms -> save/reorder/delete | Updating one question must not silently erase unrelated unsaved edits; preserve them or obtain the same discard decision before destructive replacement |
| Narrow screen, keyboard, light/dark, long content | Same modal at every width, full-screen at <=900; resize preserves editor/URL/values/pending state, meaningful focus, visible close, no clipping; direct standalone URLs retained |

Typography: shared Admin family/scale, popup title above section headings, normal
body/control text and smaller muted help. Confirmation title is emphasized body
text, never a competing page heading. Fix the current oversized warning and use
Delete for permanent question deletion. EN/DA wording preserves actual consequences.
Unsaved discard protection is approved; no prompt when unchanged. Reload/navigation
away may use the browser's native unsaved warning; do not build custom infrastructure.

Files: signup-questions-overlay.js, Questions.cshtml, its existing Admin CSS owners,
necessary scoped layout/localization bindings, and focused existing test surfaces.
Do not modify Accounts/Catalogue lifecycles, public UI behavior, backend deletion
semantics, migrations or protected Board/Draft workspaces. Zero new tables, services,
routes, dependencies, jobs or generic overlay/dirty-state frameworks. Reuse current
owners; extraction across pages follows acceptance, not this pilot.

Verification: execute focused browser journeys including save failure, validation,
dirty dismissal/history, duplicate-save prevention and scoped rendered theme/narrow
checks. Use disposable local data or a controlled browser harness with real rendered
markup; distinguish harness behavior from authenticated server proof. Run compilation
if Razor changes, affected existing tests where assertions cover changed bindings,
and diff checks. No full suite or broad Admin audit. One independent scoped review
then bounded remediation; user performs final visual acceptance. No commit/push,
production mutation, further page family or rollout is authorized by this pilot.

### Approved pilot follow-up — 2026-09-08

User accepts the current visual direction and authorizes two final corrections:
1. Signup code flow: toggle controls code-field visibility without saving. Enabling
   without an existing code requires a new code; with an existing code show
   Replacement code and Leave blank to keep the current code. Save code settings
   explicitly persists both toggle and code. Disabling hides the irrelevant input;
   preserve server hashing, validation, permission and disable/clear semantics.
2. Responsive popup: remove viewport-dependent closing/navigation for this editor.
   Opening from Participants at any width uses the same route-backed modal; <=900
   becomes full-screen through CSS. Resizing either way never closes/reloads it or
   discards values, alters URL or cancels a pending write. Keep standalone direct
   routes for deliberate navigation/recovery. Existing Close/Back/discard/focus and
   pending guards apply at every width.

Extend only Questions markup/model non-secret presentation state if needed, its
existing JS/CSS/localization and focused tests. No database schema/rule changes,
no other popup rollout. Verify code off/on/existing-code blank-retain/failed save,
small-screen open/reload, resize clean/dirty/pending, close/Back/discard and direct
route retention. Prior unrelated passing pilot checks remain applicable.

Pilot and follow-up manually accepted by the user on 2026-09-08 after local use.
The accepted target can guide the next separately authorized Admin popup pass;
this acceptance does not authorize rollout, extraction, packaging or push.

## 8. Participant-management popup — approved next pass (2026-09-08)

User approves continuing the Admin popup work and supplies current Participant
management screenshots showing a spread-out summary and disconnected lower actions.
This pass targets the pictured Participant editor, reached from Participants via
Edit; website Accounts Create/Manage is not the pictured surface and remains later.
Use the accepted Signup Questions pilot as the behavior/typography target.

### Scope, composition and protected behavior

- Compact signup summary: keep all current event/status/date/order/source/team/
  owner/payment/status-note information, with labels near their values and a
  readable responsive grouping. Preserve payment's actual toggle form/handler.
- Signup answers follow the summary, then private Admin notes with their own clear
  save action. Remove duplicated static help/placeholder wording and excess space.
  Preserve all question types, optional Regular/Alt distinctions, EHB, validation,
  previous/readonly answers and optimistic version inputs.
- User refinement (2026-09-08): place Save changes beside the final answer
  when width and field shape allow; wrap naturally for full-width answers/mobile.
  Right-align the lower action triggers on one wrapping row, with expanded
  confirmations retaining readable available width.
- Group ownership transfer and withdrawal/restoration into a named participant
  actions area. Keep confirmations compact and inline, Cancel before commit action.
  Retain the exact conditional availability and bindings for owner search, transfer,
  remove/restore, vacancy replacement and promotion follow-up. Do not collapse these
  distinct authority/lifecycle operations into one generic confirmation configuration.
- Use the same modal at every width with full-screen presentation at <=900.
  Resizing preserves URL/content/dirty/pending state. Deliberate standalone/direct
  routes and reload remain usable; preserve Participants filters/scroll on close.
- Apply accepted close/Back/discard/focus/scroll rules, pending duplicate protection,
  input retention on failed/invalid saves and safeguards against losing another
  dirty form when payment/notes/details/actions replace content. Successful independent
  saves stay in this participant workspace with accurate updated state/feedback;
  existing authoritative redirects after lifecycle actions remain meaningful.
  After a successful mutation, closing must return to refreshed Participants data,
  preserving filters and scroll; refresh failure must not silently present stale
  data as current.
  User follow-up explicitly includes the accepted Signup Questions editor: its
  question-count update alone leaves parent table columns/answers stale. Apply
  the same freshness rule there after question changes/deletion, with focused
  regression; no unrelated Questions layout changes.

Preserve authorization, privacy, concurrency tokens, audit, ownership handoff,
retained signup history, lifecycle and capacity rules exactly. No backend rule,
route, schema, data migration, public-page or Board/Draft change. Directory table
composition stays intact apart from necessary trigger/refresh bindings.

### Ownership and complexity budget

Primary files: Participant.cshtml, participant dialog section of event-manage.js,
existing Admin CSS/localization/layout script bindings and focused existing tests.
Make common editor guard/lifecycle/feedback behavior physically shared with Signup
Questions where both need it; retain explicit page-specific URL/response adapters.
Budget at most one small Admin-only shared JS owner and, only if useful to eliminate
actual repeated discard/feedback markup, one shared Razor partial. Prefer existing
owners where they already fit. No new dependency, service, route, table, policy,
job, generic overlay framework or declarative action system. Touch the accepted
Questions consumer only to adopt identical shared behavior; protect its accepted
composition/code settings with a focused regression. Avoid changing public site.js
transport unless a concrete integration requirement proves it necessary.

### Journeys and proof

| Journey | Required outcome / focused proof |
| --- | --- |
| Admin Participants -> Edit, close/reopen/reload/direct URL | Same participant/context and correct URL/focus; real rendered navigation/browser |
| Compact populated summary, long names, read-only/withdrawn states | All facts/allowed actions still visible, readable at desktop and narrow; scoped source + current rendering |
| Edit answers or notes -> invalid/failing save -> correct/retry | Values remain, validation/feedback usable, successful state updated; browser intercepted responses + existing binding tests |
| Dirty answers -> payment/notes/transfer/lifecycle action | No silent loss of other unsaved forms; pending duplicate guard and explicit discard decision; browser guard proof |
| Close/Back/Escape with dirty state or nested confirmation | Only relevant confirmation dismissed; cancel preserves content, discard leaves expected context |
| Resize clean/dirty/pending; narrow open and scrolling | Modal stays present; visible Close, focus, scroll containment and readable action layout |
| Shared Questions consumer | Accepted code visibility/save and modal/dirty/pending behavior unaffected; focused existing Node regression and one browser smoke |

Use controlled local read-only preview and mocked writes for UI proof, explicitly
separating that from persisted lifecycle verification. Backend unchanged means no
full integration-suite rerun. Compile Razor; run relevant existing Node regressions,
scoped diff checks and one independent source/visual review, then bounded named
remediation. Final user acceptance is page-specific. Stop before any next family,
commit, push, deployment or unrelated cleanup. A concrete product contradiction or
unbudgeted owner requires planner/user direction before implementation.

## 9. Accounts Create/Manage popup behavior — approved 2026-09-08

User authorizes the next Accounts/Roles pass and considers its general appearance
already good. Preserve directory/detail composition; focus on behavior and the
content revealed by action buttons. Create is the existing emergency-credential
creation route; Manage includes website accounts and emergency credentials.

- Reuse the accepted Admin editor guard; same modal at all widths (fullscreen
  <=900), clean close without prompting, dirty Close/Back/cancel protection,
  pending duplicate/dismissal guard, failures retaining inputs and clear retry.
- Replace confirmation-modal-inside-editor with compact inline confirmation,
  one visible confirmation at a time, Cancel before action, body-sized wording,
  Escape/cancel returning focus to the trigger. Preserve required reason fields,
  every action's real handler/data/availability, and one-time link disclosure.
- Preserve create scope/event/team semantics and deliberate directory success
  navigation. Scope changes must not silently lose typed credential details.
  Manage saves retain meaningful current state/feedback. Both paths refresh the
  affected Accounts directory including filters/pagination/scroll; failure after
  successful save reports that truth and reloads instead of exposing stale data.
- Reload/direct overlay restoration must restore all new trigger bindings;
  deliberate standalone routes/recovery remain usable. Preserve action-specific
  transfer/navigation destinations rather than forcing all actions into a popup.
- Protect role gating, website/emergency distinction, authorization, concurrency,
  disable/reset/restore history, credential/token handling and cutoff rules.
  No backend/product/route/schema/security changes or unrelated page rollout.

Files: Accounts Create/Manage Razor, account-manage-dialog.js, scoped existing
Admin CSS/localization if needed, existing account dialog tests. Shared guard may
be extended only for an actual common need, with focused consumer regression.
No new generic framework, dependency, service, database object or route.

Implementation Astra Low, independent source-only review Astra High (user-approved
lean rollout policy). Implementer performs focused existing Node checks and Razor
build only for compiled markup changes. One short real-browser check by root for
nested confirmation, dirty/pending/failure, parent refresh and reload handoff;
intercept mutations, protect local data, explicitly separate UI proof from persisted
account/security behavior. User provides final visual acceptance. No independent
reviewer browser pass or broad audit/full suite. Stop before next family or package.

Accounts Create/Manage manual acceptance complete on 2026-09-08 after the bounded
Disable/Generate-link confirmation visibility correction. No next-family or
packaging authorization is implied.

## 10. Catalogue Add/Edit popup behavior — approved 2026-09-08

User accepts the general Catalogue appearance and authorizes behavior-first Add/Edit
rollout, including action-revealed text/font/confirmation defects and suspected stale
parent data. Preserve its specialized activity/drop composition and existing forms.

- Adopt the accepted shared Admin dirty/pending/discard/failure safeguards and same
  modal across widths (fullscreen <=900). Clean close is immediate; dirty Close,
  Back and cross-form actions require an explicit decision; failed/invalid saves
  retain inputs and usable retry. Resize never changes route or abandons edits.
- Keep confirmations compact and inline, only one decision visible, Cancel first,
  normal Admin body typography, meaningful focus and revealed content scrolled into
  view. Cancelling discard restores the same typed confirmation/visible editor.
  Preserve typed DELETE requirements and duplicate-item choice semantics exactly.
- Successful Add/Edit/drop/state/delete operations refresh affected parent catalogue
  data before return, preserving search/filter/pagination/scroll and native modal
  connection. Refresh failure after success is truthful and offers actual reload;
  deleted activity/create redirects remain meaningful. No stale previous editor.
- User clarification: include correct/missing toast messages across Add/Edit/drop,
  state/deletion and failure/recovery paths. Preserve one truthful outcome message
  with correct severity, visible during the modal; do not confuse save failure with
  saved-but-parent-refresh failure or show duplicate success notices.
- Preserve expanded drop editing, item-name duplicate selection, probability/rate
  parsing and labels, image cache actions, readonly/server error states, every
  hidden ID/version binding and existing action-specific redirects. Direct/reload
  overlay restoration rebinds new triggers; explicit standalone routes remain usable.
- Protect Admin/SuperAdmin gates, audit/concurrency, dependency-safe delete versus
  deactivate, immutable catalogue identity, board snapshots/history and image source
  security. No backend/domain/schema/import/Board/public behavior changes.

Primary files Catalogue/Index.cshtml, catalogue-admin.js, scoped existing Admin CSS
and localization, existing catalogue-admin.test.js. Reuse admin-editor-guard.js;
extend it only for a demonstrated common need and check affected consumers. No new
framework, dependency, route, service, persistence or generalized configuration.

Astra Low implementation; Astra High source-only independent review, no reviewer
browser inspection. Focused existing Node tests and Razor build only as appropriate.
Root executes one bounded browser chain for changed interactions: dirty/failure and
confirmation recovery, native modal/parent refresh, Add/Edit reload handoff and
narrow resize; use mocked writes/read-only local fixtures, no catalogue mutations.
User final visual/action-message acceptance remains separate. No broad audit/full
suite, next-family rollout, packaging, commit or push is authorized.

### Current-popup confirmation trigger correction — approved 2026-09-08

Apply to the existing Questions, Participant Manage, Accounts Create/Manage and
Catalogue Add/Edit adapters: hide the initiating action button while its inline
confirmation is visible; restore it before Cancel/Escape focus return and when
switching/dismissing that confirmation. Preserve typed values, discard recovery,
pending/duplicate protection and existing layouts/handlers. Adopt the general rule
in UI_SYSTEM; no other family rollout or whole-site audit is included.
Use Astra Low for the bounded correction and Astra High for one source-only review
of this correction, without reopening the completed popup passes. Verify the
changed visibility/focus paths and affected guards with focused existing checks;
no repeat build or browser harness work unless a concrete changed risk requires it.
Prior page approvals remain page-specific; the user manually approves this
correction on 2026-09-08. Catalogue's broader approval remains separately recorded.
Now agree the leaner
workflow before further rollout; no packaging, commit, push or deployment.

## 11. Add Participant popup behavior — authorized 2026-09-08

User authorizes the next popup pass after agreeing Luna Max implementation,
focused implementer verification and read-only correctness review without reviewer
visual inspection. Next bounded surface: Participants -> Add participant, including
its existing `addParticipant=1` reload/direct entry. Preserve the existing form
composition, owner picker, question/account inputs, lifecycle availability and
CreateInternalParticipant handler/capacity/waiting-list rules (FUNCTIONAL_CONTRACTS
5.5). Participant Manage and Catalogue approval states remain separately owned.

- Same modal at every width, fullscreen <=900; resize preserves values, URL and
  pending work. Retain existing deliberate route/recovery behavior.
- Reuse shared editor guard for dirty Close/Cancel/Escape/Back and pending duplicate/
  dismissal protection; discard Cancel preserves values/focus. Inline confirmations
  follow the accepted trigger hiding, focus and visibility rule where applicable.
- Failed load has usable close/retry; validation/transport failure retains submitted
  values and usable localized recovery. Preserve owner-picker and form bindings.
- Successful creation returns to fresh Participants data with retained filter/sort
  context and appropriate scroll, one truthful success notice and no false dirty
  prompt. Existing route-backed navigation is acceptable; no new partial-refresh
  infrastructure. Saved-but-refresh-failed feedback must not imply creation failed.
- Check correct/missing toast outcome, severity, duplication and modal visibility
  within this pass. No separate toast audit.

Files: Add-dialog region and necessary initialization hooks in event-manage.js,
_InternalParticipantForm.cshtml, necessary Participants.cshtml/layout/localization
bindings, existing Admin CSS and focused Participants tests. Shared guard changes
only for a demonstrated common need. No new backend rule, domain/schema/service,
route, dependency or generic framework. No Board/Draft or unrelated family work.

Luna Max implementer owns changes and focused runnable checks for all-width open/
resize, dirty dismissal, pending duplicate/dismissal, retained failure input and
successful fresh return/reopen. Reuse existing test infrastructure; compile only
when Razor/resources require it. Use controlled mocked writes, never mutate/reset
the user's database or restart their 7131 app. A browser check is only justified
by a named changed risk not covered by existing executable checks; no repeated
harness debugging. One Astra High source-only correctness review of this exact
pass; no reviewer browser/screenshots or duplicate passing checks. User supplies
visual acceptance. Stop after this pass for feedback; no packaging/commit/push/
deployment or automatic next family.

Add Participant pass complete 2026-09-08: user visual approval and source-only
correctness review clear after fixing current filter/sort context and successful
creation/failed-refresh reload recovery. Add/Participants/Participant Manage Node
regressions, syntax and diff checks pass; Razor build passed with 0 warnings/errors
before JS-only corrections. No backend changes or persisted creation tests; client
responses were mocked in focused tests. Stop before another popup family.

## 12. Teams/Draft Add team popup — authorized 2026-09-08

User says Add team's appearance is already good; preserve the current native modal,
field order, typography and Draft workspace geometry. Scope is only the Add team
trigger/dialog/form (`#add-team`) on Admin -> event -> Teams/Draft, in Setup and
the permitted finalized pre-formed correction state. Team roster dialogs, Draft
controller/turns, Board, imports and other popup families are excluded.

General-look approval covers composition, not blanket approval of revealed
confirmations/content, typography or feedback states. Inspect applicable details
against UI_SYSTEM; the user notes no extra revealed surfaces requiring an added
render for this popup. Future passes may use targeted renders for concrete gaps,
without duplicate reviewer visual inspection.

- Reuse admin-editor-guard in the existing Add team adapter: clean dismissal,
  dirty Close/Escape/outside-click with compact discard/keep choice, native
  navigation/Back unsaved protection where applicable, pending duplicate/dismissal
  protection. Keep this modal at all widths; resizing never abandons entered data.
- Preserve name/formation/affiliation and all existing AddTeam bindings, permissions,
  state/confirmation rules, audit and transaction semantics. Finalized inline
  confirmation follows Cancel-first, hidden trigger, reveal/focus and typed-value
  restoration rules. No new route marker or generic modal framework is required.
- Intercept only this form as necessary to retain typed inputs on failed POST;
  distinguish rendered success/error outcomes (both currently redirect). Success
  returns to fresh Draft state with one accurate toast and retained useful context;
  no false dirty prompt or completed-operation resubmission. Reuse existing shared
  toast and navigation owners; do not rewrite global site.js POST transport.
- Check Add outcome text/severity/recovery/visibility while modal. Correct the
  demonstrated blank-name error currently mislabeled as locked draft, by separating
  presentation messages only; validation/lifecycle behavior stays unchanged.

Files: Add-specific Draft.cshtml markup, event-manage.js Add-only initialization,
existing scoped CSS/layout/localization if required, focused Add team test using
existing popup test infrastructure. Draft.cshtml.cs may change only AddTeam status
wording/selection for blank name; no other handler/backend-rule change. Zero new
route/service/schema/dependency/abstraction. Snapshot only edited files for review.

Luna Max implementer: one focused runnable Add test covering dirty/confirmation,
pending/failure and success feedback/reopen, plus directly affected existing
event-manage consumer regression and syntax/diff checks. Compile once for Razor/
resource changes; add only a focused check if server-message branch changes.
No browser harness, DB mutation/reset, app restart or repeated verification.
Astra High read-only source correctness review; user provides visual acceptance.
Stop after Add team for feedback; no roster pass, packaging, commit/push or deploy.

Named review remediation stays within this pass: preserve the consumed success
notice through the existing pending-toast owner; make Discard reset fields/baseline;
fix confirmation hidden/inline CSS, order, keyboard/focus/reveal and resize scroll
locking; add the missing Danish blank-name resource. The shared site.js toast-host
selector may receive a minimal Add-native-dialog opt-in to keep toasts in the
modal without adding layout classes; its POST transport stays unchanged. Correct
the test that fabricated a success toast on navigation so it checks actual notice
handoff instead. No backend/lifecycle expansion.

Implementation/remediation and source-only review complete 2026-09-08. All named
findings corrected; focused Add team/affected Participant checks, syntax/diff and
Razor/resource build pass (0 warnings/errors). No browser or persisted team writes.
User manually approves the completed Add team pass, accepting minor behavior
differences from other popups without further remediation. No next pass authorized.


## 13. Teams/Draft roster corrections — authorized 2026-09-08

User protects the roster popup's general composition and requests two changes:
show truthful Captain readiness after a Captain is assigned, and choose a role
while manually adding a participant. This bounded correction does not reopen the
whole roster workspace or import workflow.

- Add Participant (default), Captain and Co-captain selection to existing manual
  AddMember and AddExternalMember forms, including permitted pre-formed correction
  variants. Existing post-add role editing remains. CSV role semantics are unchanged.
- Persist membership creation and selected role atomically. Invalid roles or failed
  assignment leave no partial membership/participant/character changes. Reuse the
  existing Captain authority rules, role history, audit and owned-account
  notification behavior; do not implement two client requests or duplicate those
  rules in JavaScript. Preserve lifecycle, permissions, version/concurrency,
  source/capacity/character-reservation rules and finalized correction publication.
- Distinguish a current Captain role (draft-start requirement) from usable owned
  website-account access (event-start requirement, with existing emergency option).
  Once a Captain exists, do not still claim that a Captain must be assigned. If
  website access is missing, name that issue accurately; Co-captain alone does not
  satisfy the Captain requirement. Keep the warning synchronized with server state.
- Preserve composition, subsequent role changes, existing popup handling and other
  completed passes. New controls/feedback follow UI_SYSTEM localization, typography
  and toast rules. No generalized popup rewrite, CSV expansion, new schema, route,
  service, dependency or standalone framework. A small extension to the existing
  authority service for atomic caller-owned transactions is allowed if needed.

Luna Max implementation with a self-contained bounded prompt; Astra High independent
source-only correctness review; user visual acceptance. Focused isolated PostgreSQL
integration tests prove selected/default roles, relevant rollback/history/authority
and truthful readiness; use existing suites rather than a browser harness. Compile
Razor/resources and run only directly affected checks. Docker is available through
approved sandbox escalation; never use or reset the user's application database.
Stop after these corrections and review, without another family or packaging.

Section 13 implementation and bounded remediation complete 2026-09-08. Independent
source-only review clears all findings: truthful badge, malformed-role model-binding
rejection, external selected-role finalized publication/rollback and rendered
readiness proof. Focused isolated PostgreSQL checks and Razor builds pass; final
binding test fixes the lifecycle clock and asserts role-specific rejection. No
application database mutation or browser harness. Await user visual acceptance of
the changed states; general composition approval and other page statuses remain.

User manually accepts section 13 on 2026-09-08 with one small visual correction:
reduce the roster participant remove SVG glyph size while preserving its clickable
area, focus, accessible label, confirmation and existing composition. This authorizes
only a scoped CSS correction with focused cascade/whitespace checks, not another
behavior pass, backend test/build cycle or popup family.

Further section 13 manual correction: user explicitly retains the existing small
positioned roster-member removal confirmation (no move inline) and requests compact
Remove member action typography consistent with Cancel. Match its scoped button typography, padding and height to Cancel; the follow-up
screenshot confirms font-only correction leaves an undersized button. Confirmation
placement, behavior and other controls are protected.
Focused cascade/whitespace inspection suffices for this CSS-only correction.

User manually approves the completed Teams/Draft roster popup on 2026-09-08,
including the remove glyph and confirmation button typography/padding corrections.
Section 13 accepted; no next popup/family or packaging authorized.

Section 13 dismissal correction: user reports main roster popup fails to close on
outside click. Add bounded backdrop handling through the existing close/history/
focus path, preserving entered values and applicable pending protection; clicks
inside the dialog or its small removal confirmation must not dismiss it. Add a
focused executable regression for that missing event path, without another popup
family, layout/backend rewrite, full suite or browser harness. Prior approval
remains for visuals and role/readiness corrections; dismissal awaits correction.

### Section 13 guard completion — authorized 2026-09-08

User explicitly requires completing the missing roster dirty/pending safeguards
before moving on. Reuse admin-editor-guard and existing roster/add-team transport
patterns; preserve all approved composition, role/backend behavior and the small
positioned removal confirmation. Cover all editable forms within this roster.

- Dirty X, Escape, outside dismissal, Back/navigation and switching team/editor
  require Discard/Keep. Keep restores focus and entered values; Discard actually
  restores baseline fields before completion. Other-form submission cannot silently
  lose unrelated edits. No false dirty warning after a completed mutation.
- Pending saves block duplicates and dismissal/navigation until resolved, while
  retaining submitted values and correct submitter/form data. Failure restores
  usable controls and retains inputs with truthful visible feedback. Successful
  saves refresh authoritative parent/roster content, preserve useful route/scroll
  context and produce one accurate outcome toast. Completed mutation with failed
  refresh must be distinguished from failed saving and must not be resubmitted.
- Guards remain effective across widths/resizing; use existing all-width modal
  treatment as needed, without abandoning inputs or redesigning roster layout.
  Existing form routes, authorization, versions, role history, CSV preview/apply
  semantics and finalized publication remain unchanged. No new backend/route/schema
  or generalized popup framework; extend only existing page adapter/guard as needed.

Luna Max implementation, smallest direct runnable guard/response regressions plus
directly affected consumers, one Razor build only if markup/resources change;
Astra High source-only review. No browser harness, app restart, application database
mutation, full suite or repeated passing checks. Planner identifies the next popup
from bounded repository evidence in parallel, but does not start it.

Guard completion implemented and source-reviewed 2026-09-08. All five findings
resolved: preserve submitted dirty baseline through cross-form discard/failure;
require explicit success evidence; refresh sibling participant data; guard native
modal/application navigation across widths; retain visible connected toast host.
File-only changes are serialized with file metadata and covered by the roster
regression. Focused roster and five affected consumer checks pass; Razor build
0 warnings/errors. User check of new guard states remains; no next pass started.


## 14. Board tile popup behavior — authorized 2026-09-08

User accepts current Create/Edit tile composition and flow as shown in the two
current screenshots. Proposed spacing changes, sticky footer, optional-detail or
counting-option collapse, picker search, objective summaries/collapse and wizard
work are NOT authorized. Preserve layout, field order, density and information.
Apply the usual Admin popup behavior and inspect leftover/revealed UI (including
confirmations, expanded controls, type hierarchy, focus and outcome feedback).

Scope: Board Create/Edit tile shared editor, its objective-removal confirmation,
and existing tile-detail/removal popup states affected by the same dialog owner.
Preserve Board canvas/sidebar/toolbar/drag-drop/collaboration, objective semantics,
weights/counting/source selection, image behavior, EHB, versions/permissions,
lifecycle and publication rules. No backend/domain/schema redesign or other family.

- Adopt shared admin-editor-guard with page-specific adapter. Clean Close/Cancel/
  Escape/outside dismiss; dirty in-page dismissal, application navigation and
  switching tile/editor require Discard/Keep. Browser Back/reload/leave use native
  unload protection; do not introduce Back-closes-popup or a new history machine. Keep retains typed state/focus; Discard resets original fields,
  dynamically added/removed objectives and file selection. Initial Edit is clean.
- Same modal across widths, fullscreen on narrow screens; resize preserves typed
  or pending state. Outside-click uses actual bounds/gesture checks and never
  dismisses from inside padding or child confirmations. Restore opener focus.
- Pending mutation blocks duplicates/dismissal/navigation and conflicting actions.
  Capture FormData, submitter and contiguous requirement names before disabling
  controls. Retain all submitted fields/objectives/files on validation, transport,
  permission or concurrency failure; show truthful localized recovery and restore
  controls. Stale board versions require explicit recovery, not silent data loss.
- Objective-removal uses compact local confirmation rather than a nested editor
  modal; preserve local-only removal semantics. Existing tile-removal confirmation
  uses the shared compact pattern while keeping its handler/version semantics.
  Cancel first, hide initiating action while shown, restore trigger before focus,
  Escape cancels only the topmost confirmation, and reveal scrolls into view.
  Use existing complete button primitives (type, padding/height, focus and severity),
  not isolated typography overrides that leave undersized controls.
- Successful mutations return to freshly authoritative Board context: canvas,
  tile/editor data, statistics, version/actions and related summaries all refresh,
  retaining useful scroll/context. Use one correctly severe visible toast; shared
  native-modal toast host must stay connected while needed. Success is explicit,
  not inferred from HTTP200/no errors. Distinguish committed save with image/other
  warning and failed post-save refresh from failed saving; completed work cannot
  be submitted twice. Do not let a background refresh discard active edits/results.

Implementer may extend Board.cshtml scoped markup/inline adapter (or extract only
that existing adapter to a focused JS file if necessary for maintainability and
execution), existing shared guard/layout/CSS/localization and focused tests. Keep
shared changes minimal with existing callers' defaults unchanged. PageModel changes
are limited to minimal presentation/outcome evidence if necessary to distinguish
committed-with-warning from failed POST; no business-rule or persistence change.
No new route, service, schema, dependency or generic modal framework.

Luna Max implementer; Astra High independent source-only reviewer; user final visual
acceptance of changed/revealed states. Reuse board-dialog.test.js and current popup
Node infrastructure. Direct regressions cover dynamic/file dirty state, every
close/navigation path, topmost confirmation/focus/trigger restoration, pending
serialization/blocking, retained failures, explicit success/warning distinction,
fresh parent and failed-refresh recovery. Run affected consumers only if shared
code changes and one Razor/resource build where required. No browser harness,
application database mutation/reset, app restart, full suite, packaging/commit/push
or deployment. Stop after this family for acceptance. The supplied screenshots
are accepted current composition evidence, not authorization to redesign it.

### Section 14 implementation safety boundary

User-approved completion assignment: Astra Low replaces Luna Max only for the
remaining Board save recovery, navigation guards and compact confirmations after
prolonged turnaround. Keep tested close/fullscreen/collaboration corrections; one
completion assignment with focused checks, then Astra High source-only review and
manual acceptance. No new preliminary review or broad discovery.

Lean resumption — user authorized continuation after accepting the gap-first
workflow. Existing source assessment supplies four concrete gaps; do not repeat
discovery: unconditional dismissal bypasses dirty/pending protection; native POST
loses the current form/files on failure; objective/tile confirmations need the
compact local behavior; narrow fullscreen and collaboration's dialog-owner query
need focused correction. Existing editor population, Board data/rules and layout
remain the starting point. Implement these gaps, then one focused verification and
source review; no new preliminary review.

Automatic edit review rejected the initial broad navigation/history/submission and
generic baseline-restoration rewrite as excessive regression/data-loss risk. The
incomplete Board-only edits were restored to their canonical pre-pass snapshots;
all earlier working changes are preserved. The rejected attempt remains evidence,
not an implementation baseline. Do not reconstruct it through incremental patches.

Independent read-only assessment identifies a materially smaller implementation:
reuse existing resetTileForm/addRequirement/populateRequirement/initializeRequirements
and cached tileEditorData to reconstruct the original selected tile/create position
on Discard. Extract only existing open/populate blocks as needed; initialize the
shared guard after population. Replace unconditional close listeners in place with
one guarded close/switch/application-navigation path. No generic DOM snapshot/state
framework and no new history machine. Native browser-owned departure protection is
the existing UI_SYSTEM exception; within-page guards remain required.

Retain form DOM during a narrow async POST adapter (including File inputs on error),
use minimal consumed outcome markers to distinguish commit/warnings/stale recovery,
and reuse existing full-response navigation installation/context preservation on
success. Do not add a Board fragment refresh framework or shared-guard rewrite.
Add only local confirmation wiring and scoped fullscreen/feedback CSS, and correct
collaboration's open-dialog query to the owning Board page. If automatic review
rejects this different smaller architecture too, report the exact rejection and
stop edits immediately; do not retry with smaller pieces or alternate edit tools.

### Section 14 completion checkpoint — manually approved 2026-09-08

Accepted layout preserved. Existing close/fullscreen/collaboration corrections are
retained; user-approved Astra Low completed save recovery, navigation and local
confirmations. Astra High source-only review cleared five focused corrections:
preserve new-tab/modifier links, clean committed recovery, genuine backdrop gesture,
reuse failure feedback/reload controls and truthful stale-response wording. Focused
Board Node tests and syntax/diff checks pass; final Razor build had 0 warnings and
0 errors before JS-only fixes. No browser/DB or persisted concurrency claim. The
user manually approved the popup after a typography-only correction to the discard
confirmation; focused CSS source review and diff checks passed. Approval is recorded
in UI_PAGE_MATRIX. No next-family rollout without user authorization.


### Bundled Board/Teams manual corrections — authorized 2026-09-08

Preserve approved Board tile details/toolbar and inspected Teams/Draft popup
composition. Fix only the false dirty/discard prompt when untouched roster/tile
editors close and the oversized page-level Remove team X. Reuse shared guard and
existing compact removal glyph styling; preserve genuine input/file changes,
pending protection, focus and click targets. Verify the demonstrated clean/dirty
boundary and affected shared consumers, then one focused source review. No backend
or layout redesign. Review and Finalize are deferred for separately scoped page
overhauls; stop after these named corrections for user acceptance.

Correction implemented: empty upload placeholder timestamp normalization and compact
page-level team-removal glyph. Board/roster regressions and eight direct consumer
checks pass; Participant Manage fixture failure reproduces with the previous guard
and is outside this correction. Focused independent source review clears; user
manually approves both corrections 2026-09-08. No backend or markup change, browser
harness, build or DB work. Stop before further implementation without authorization.


### Participant confirmation reveal correction — authorized 2026-09-08

User approves Catalogue Add/Edit. Participant Manage Remove, Restore and Transfer
ownership reveal inline confirmations out of view. Bring newly opened confirmations
into view using the existing shared Participant binding; retain focus restoration,
input state, guards and composition. Scope is this defect, its focused executable
check (including directly required test fixture correction), and one source review.
Stop for user acceptance; Review/Finalize overhauls remain deferred.

Implemented with one scroll call in the existing shared toggle handler. All three
confirmation reveals/Cancel paths pass the focused Participant regression; its
missing document.removeEventListener mock was corrected. Syntax/diff and independent
source review clear. User manually approves Participant Manage including the reveal
correction, 2026-09-08; no build/browser/DB work.


### Event-settings confirmation follow-up — authorized 2026-09-08

User approves Schedule change/warning and Accounts ownership-transfer confirmations.
Overview signup/start/end/resume/cancel/discard confirmations receive one bounded
read-only source review because manual lifecycle-state walkthrough is impractical.
Report concrete rendered-flow/handler/recovery/feedback findings; do not mutate data
or equate source review with execution/manual acceptance. Identity timezone review
receives only auto-scroll using existing reveal wiring and compact confirmation
fonts; preserve preview values, button padding, routes and other page composition.
Use focused checks and source review for that correction. Review and Finalize stay
deferred; no further rollout or backend redesign authorized.

Source review completed with six bounded Overview lifecycle findings, recorded in
CURRENT_STATUS and the linked source report; remediation not yet authorized.
Identity correction uses existing reveal marker/helper script plus scoped typography;
helper execution/wiring/cascade/diff checks and independent source review clear.
User check pending. No broader initializer or backend changes were made.


### Overview lifecycle confirmation remediation — authorized 2026-09-08

Fix the six named source findings as one bounded pass:

- Close signup must not require opening-only warning/proposed-close acknowledgements;
  preserve existing server confirmation, permission, state and version checks.
- Failed Start/End/Resume/Cancel (and directly related confirmation failure paths)
  retain entered reasons/replacement end and confirmation context. Do not expose
  reasons in query strings or erase concurrency protection on retry.
- Pending lifecycle mutation blocks duplicate submission and dismissal/navigation
  through its confirmation controls until the result is known. Reuse existing post
  navigation state; scope changes to Overview confirmations instead of rewriting
  global navigation or adding a history framework.
- All lifecycle confirmation branches reveal/scroll and focus consistently. Cancel
  or Escape returns to the initiating control and follows the existing route;
  pending cannot be bypassed. Cancel precedes the semantic/destructive action.
- Signup outcomes set explicit correct severity. Intermediate acknowledgement steps
  are informative, concurrency/rejection is an error, successful mutation is success.

Preserve Overview composition and backend lifecycle/domain/service rules, routes,
permissions, versions, audit and persistence. Prefer local handler/markup/adapter
fixes; no new service/schema/route or general framework. Identity user acceptance
remains separate; Review/Finalize stay deferred. One focused handler/interaction
verification set plus affected compilation, then one source review against this
scope; do not run a full suite or manipulate user data/lifecycle states. Stop after
named corrections and checks, without packaging, commit, push or deployment.

Completion: all six named corrections implemented; scoped source review and three
named fixes cleared. Concurrency failure now refreshes only version binding, retains
other posted fields and explicitly requests review of fresh details before retry.
Cancel-first/focus-return completed for every scoped branch. Focused JS and compiled
Web/Razor checks pass. EventCreationUiTests has 26 passes and one unrelated stale
Participant source assertion (also absent before this pass). Final Danish notice
entry passes XML/diff checks. No real lifecycle/database handler execution or
browser proof claimed; see CURRENT_STATUS/evidence. User manually approves these
corrections. Further work is paused for budget/workflow discussion; no new dispatch.


## 15. Admin Review queue — agreed scope 2026-09-09

- Reuse established Admin implementation/styles (including Participants search/status
  controls), not screenshot approximation. Replace Review queue legacy typography,
  outlines and dividers; preserve overall table composition and information.
- User visual correction: Review table text uses weight 400, including remaining
  explicit bold table descendants; retain the separate heading/toolbar hierarchy.
- Remove the queue masthead and Administration button; shared header retains Review.
  Capitalize Submissions and use established Admin table-heading typography.
- Replace Event/Team/Tile dropdowns and Apply filters with search on the left and
  Status on the right. Search team name, credited player name and tile name;
  combine search/status and update results without page reloads.
- User follow-up 2026-09-09: Pending submissions always sort before other statuses,
  newest first within Pending and within the remaining rows. Search/Status may hide
  rows but preserve this order among visible results. Verify ordering with mixed
  statuses/timestamps using the existing focused queue test.
- Review navigation always targets the current selected event, including events
  with no evidence or unable to receive evidence. Preserve authorization and
  hidden-event boundaries. No cross-event queue through the removed filter.
- Protect status semantics, table data, Details destinations, evidence integrity,
  review actions and linked-resubmission behavior. Details presentation/workflows,
  Finalize, global Admin redesign and unrelated cleanup are outside this pass.
- Focused checks: event-scoped navigation/queue including empty/ineligible states,
  combined search/status across the three fields, no-reload updates and empty
  results; applicable Razor compilation and scoped diff checks. Reuse current tests.
- User-approved task exception: bounded source inspection and fresh independent
  review use GPT-6 Astra Low. Independent review covers backend/functionality only,
  with no UI/visual review; user owns visual acceptance. Routine UI implementation
  uses the AGENTS default GPT-5.6 Luna Max. No new dependencies, tables, services or
  generalized frameworks are planned. Stop for consequential scope uncertainty.
- No packaging, stage/commit/push, deployment, user database mutation or restart of
  the user's HTTPS 7131 application. Stop at user visual acceptance.

Section 15 checkpoint: implemented and backend/functionality-only source-reviewed
2026-09-09. Release Web build, focused authenticated HTTP event-scope test, existing
queue binding test, Node live-filter test, XML and scoped diff checks passed. One
fixture expectation was corrected without production changes. User then rejected the
visual styling: the named correction adds Review queue to the existing Admin font/token
scope and fixes desktop Status alignment; scoped cascade/diff checks pass. Pending-first
ordering is implemented with a passing mixed-row HTTP test and clear Astra Low functional
delta review. User manually approves the Review queue on 2026-09-09 after the final
table-weight-400 correction. Details and Finalize remain deferred; no user-app restart,
packaging or deployment performed.


## 16. Admin Review details — approved 2026-09-09

- Purpose: lean evidence inspection and approve/reject flow. Desktop: large uncropped
  image left with existing enlarged viewer; compact facts and decision controls right.
  Narrow screens: facts, image, then actions. Remove masthead, oversized headings,
  repeated explanatory copy and padding; compact Back to review link alone. User
  follow-up places the status badge at the right of the tile heading in the facts card.
- Facts: credited player, team as secondary context, tile, drop, submission time in
  chosen timezone and UTC, verification code or explicit disabled state. Keep tile
  requirement, submission note and eligibility warnings visible. User correction:
  remove Claimed and Contribution display only; calculations and stored values unchanged.
- Approve/Reject together below facts. Final user approval selects leaf green
  `#78B86A` for both Approve text and border, with unfilled background.
  Rejection mode hides the normal action row and approval explanation while showing
  the full-width reason and Reject/Cancel; Cancel restores normal content and focus.
  Reject reveals required reason and confirmation;
  preserve validation and recovery. Retain existing approval/rejection/reversal rules,
  confirmation protections, action eligibility and post-decision navigation.
- User visual correction 2026-09-09: remove the duplicate player/team sentence under
  the tile heading; label local submission time simply Submitted (retain local and
  UTC values). Fix effective Admin font/weights/colours and neutral borders/dividers.
  Tighten facts/actions gap to established spacing, align Approve/Reject on one line,
  and make collapsed secondary sections content-height. User rejected stretched image
  and increased sidebar gap: eliminate image/grid-driven excess height, keep facts and
  actions together with a normal fixed gap, and retain uncropped/natural responsive media.
  User follow-up: desktop image card must exactly match the combined right-card
  height including gap; the right column sets row height, the contained image must
  not enlarge it. Rejection expansion/cancel resizes naturally; mobile stays stacked.
  User rejected the taller desktop minimum experiment: restore pre-trial content-driven
  sizing without stretched facts, preserving equal-height contained image/right column
  and compact gap. Mobile remains natural.
  Team-name value uses the same value colour as other facts. Rejection reason expands
  full-width below the action row; explanatory content also spans full width, with
  only the Approve/Reject buttons sharing a row. Cancel restores compact geometry with no retained
  expansion. Verify real rendered dimensions at baseline/reveal/cancel and narrow
  width in an isolated fixture using actual styles; this is implementation regression
  verification, not independent UI review or replacement for user visual acceptance.
- Metadata correction remains collapsed. Prior evidence, evidence versions and review
  history become compact expandable sections, preserving contents and access.
- Use existing Admin font/neutral tokens and control styling, normal-weight values,
  and queue styling lessons; no blue legacy borders or page-local theme. Preserve
  approved queue layout, live filtering and Pending-first order.
- Resolve event context from the authorized submission on Details, including direct
  loads, so event selector and event sidebar remain present. Back to review targets
  that event and preserves search/status when entered from a filtered queue.
- Preserve routes, bindings, evidence viewer, immutable timestamps/assets/history,
  authorization/hidden-event boundaries, concurrency, correction/reversal/audit and
  contribution semantics. Missing image in supplied screenshot is local fixture data,
  explicitly outside scope. Finalize, unrelated pages and new review behavior deferred.
- Existing-model session choice continues: bounded source inspection and independent
  backend/functionality-only review Astra Low; routine UI implementer Luna Max. User
  owns visual acceptance; no separate UI review. No new tables/services/dependencies
  or general frameworks budgeted. One bounded source inspection before assignment.
- Focused checks: Web/Razor build; authenticated route navigation Details event/sidebar
  and queue return (direct and filtered entry, protected boundaries); required reason
  reveal/validation/recovery and retained review bindings using existing tests. Check
  localized strings, scoped cascade/diff. Do not repeat unaffected queue tests or full
  suite; add executable proof only for changed functional boundaries.
- No user database mutation, HTTPS 7131 restart, packaging, stage/commit/push or deployment.
  Stop for consequential scope uncertainty; otherwise finish checks and one functional
  review, then hand to user for visual acceptance.

Section16 checkpoint 2026-09-09: implemented; Astra Low backend/functionality source
review found no production defect. Missing authenticated journey proof was supplied
with a passing controlled PostgreSQL rendered queue/Details/rejection/Back scenario,
including direct/mismatched context and hidden/unauthorized boundaries. Release build,
focused binding tests2/2, Node queue-link/filter and mocked-DOM rejection checks, scoped
source/cascade/diff checks pass. No actual browser visual verdict; awaiting user manual
acceptance. User app was not restarted; no packaging/deployment/user database mutation.

Section16 completion: user manually approves Review Details on 2026-09-09 after named
corrections. Desktop evidence card matches the combined facts/actions height through a
contained three-row header/image/footer layout; mobile stays natural. Fact values/UTC use
heading colour; labels stay muted. Status sits beside tile heading. Approve uses the final page-approved
`#78B86A` for text/border and remains unfilled. Full-width action
copy/rejection and compact Cancel recovery retained. Later fixes were source-scoped and
manually accepted; earlier isolated geometry fixtures did not establish final correctness.
No further Review or Finalize work, packaging or deployment authorized by this approval.

Final follow-up manually approved: taller-height experiment removed; pre-trial content-driven
facts/image sizing restored. Rejection mode hides normal approval copy/buttons, retaining
reason/hint/Reject/Cancel; Cancel restores them. Final leaf-green Approve `#78B86A` accepted.
Source/diff checks and user acceptance close the pass; no further work authorized.


## Co-captain signup request — approved 2026-09-10

Isolated checkout `/private/tmp/BingoWebpage-admin-co-captain-20260910`, branch
`codex/admin-co-captain`, starting at committed `admin-consistency` / `86e7dc3`.
Any eventual user-authorized PR to `main` includes that existing committed Admin work
plus this request.
Do not import uncommitted sweep documentation, T05 or C05/C09 from the recovery checkout.
This request is not an application-sweep ticket and adds no TICKETS.md entry.

Approved scope:

- Admin Participants / Signups gains a Captain volunteer column showing Yes/No,
  with Co-captain: Name on a second line when supplied. Remove the existing
  volunteered-to-captain note from Team. Captain volunteer appears before Team and is
  sortable in both the route-backed and enhanced table; Team remains sortable. Preserve
  filters, editing, table enhancement, permission states and responsive composition.
- Co-captain (optional) is a permanent optional default text question for existing
  and new event forms, separately identified/stored through the existing question
  and answer system. Existing responses remain blank; no account lookup or automatic
  Captain/co-captain role assignment. No new table/service/route/policy/job or generic
  question-dependency framework is budgeted.
- Only show the input when Captain volunteer is checked. On uncheck, hide AND disable
  it so its value is omitted from submission. Preserve the unsaved typed value if
  toggled back on before submit. Server-side enforce the same rule: when volunteering
  is false, ignore supplied co-captain text and clear any previously saved answer.
- Participant and Admin viewing/editing use existing flows and permissions. Do not
  expose the new answer on public signup tables or through non-owner/non-Admin draft
  projections. This scope grants no new editing windows or permissions.
- Keep optional status, English/Danish labels, validation/error redisplay and existing
  form/response concurrency semantics consistent. Update the smallest existing
  authorities to capture this accepted behavior. Preserve historical answers and
  schema migration integrity; any required default-question backfill must preserve
  existing records, be safe to apply once, and not require a live production mutation
  during development.

Manual-review refinements approved for implementation on 2026-09-10:

- Co-captain uses a single-line text input and renders immediately after Captain
  volunteer, ahead of custom text questions even when the existing-form backfill was
  appended later. This is presentation ordering only; stored question positions and
  historical custom-question order remain unchanged.
- Empty co-captain validation output must not reserve an extra row, while a real
  validation error remains visible. The opened signup-question deletion confirmation
  spans the available question row. Review queue secondary UTC text inherits the Admin
  table font.
- Remove the redundant Live-only `Manage live participant` text action; the existing
  pencil remains the participant editor entry point at every applicable lifecycle state.

Execution: one separate Sol Medium manager owns direct collaboration-worker dispatch
and all handoffs. Luna Max implementation/remediation, fresh Astra Medium independent
review. Root planner does not shadow routine execution; ask it only for decisions or
help. If manager lacks worker-dispatch capability, report the tooling blocker instead
of silently falling back to root-assisted spawning. Workers receive bounded assignments
and may not independently advance scope. Use existing plans/authority and focused reads;
no new readiness review for this bounded change unless a real product contradiction
appears. Review/remediate named defects only; optional cleanup remains excluded.

Verify signup/edit with and without a co-captain; checkbox hide/disable/re-enable and
malicious unchecked submissions; persisted clearing; Admin column/real enhancement;
public/non-owner privacy; existing/new form defaults and retained-response compatibility.
Use smallest executable checks for these boundaries, isolated DB/HTTP fixtures, scoped
format/diff checks and required packaging gates. UI acceptance remains user-owned: do
not silently mark a page approved; request current screenshot/visual acceptance only
when required, and record any pending manual acceptance in the draft PR.

Local test remediation on 2026-09-10 corrected only the four contracts reported by the
user's full .NET run: three BrowserTests source contracts and the migration rehearsal.
Each exact isolated filter passed and fresh independent review cleared the correction
diff. The user accepted that focused proof together with their existing full-run evidence
and declined a redundant second full-suite execution; do not represent a second full
suite as run. No production behavior changed in this remediation.

After completing the current manual-review corrections, the user explicitly authorized
packaging on 2026-09-10. This supersedes the earlier packaging prohibition for this
isolated branch only. One Luna Max packager may stage and commit the exact accepted
checkout inventory, push `codex/admin-co-captain` and open one ready-for-review PR to
`main`. Verify that the full PR scope includes the intended committed Admin base and this
request, excludes recovery-checkout C05/C09, sweep/T05 and untracked tickets, and reports
the actual validation and limitations without claiming an unrun second full suite or JS
suite. No merge/deployment, app HTTPS7131 restart, production DB scan/repair, live
provider calls or other external messages are authorized.

## Ticket manual acceptance deferral — approved 2026-09-13

The user defers the combined manual walkthrough until all ticket implementation
and independent review work is complete. Mark cleared tickets Awaiting manual
acceptance, explicitly retaining any separate blocker or data obligation. TICKETS
owns outcomes and the deferral policy; CURRENT_STATUS owns the current handoff.
This does not waive combined release gates or authorize new batches/publication.


## 17. Stats production integration — proposed implementation plan (2026-09-15)

### 17.1 Authorization, outcome and protected scope

This pass prepares the implementation plan only. The user has approved the standalone
Stats UI and the data decisions in PRODUCT_REQUIREMENTS.md §15.1 and catalogue API
mapping scope in §9.4. Production implementation, migrations against user databases,
packaging and publication are not authorized by this planning pass. UI_PAGE_MATRIX.md
owns prototype approval; integrating it into Razor still requires production acceptance.

Work from `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`.
Preserve the existing dirty checkout. Port the actual approved implementation under
`prototypes/stats/outputs/`: Stats section markup and embedded styles from
`stats-page-prototype.html`, `stats-density.css`, the rendering/interaction/animation
code in `stats-prototype.js`, and required assets. These are implementation inputs,
not inspiration for a replacement. UI_PAGE_MATRIX.md's Stats production port contract
owns this explicit user requirement. Preserve sizing, overflow, typography, drilldowns,
animations/timing, responsive behavior and artwork composition.

Exclude the prototype header and masthead; use the app's existing shell. Remove the demo
footer strip, sample labels/dates, event-size, milestone-stage, drop-preview and standalone
theme controls. Adjust artwork is the only existing bottom control that stays, authorized
for Super Admins. Keep the approved controls inside the Stats sections. Do not redesign
or independently reimplement the UI from reference screenshots.

Outcome: an event Stats page showing Drop value, Luck, Event milestones, Board progress
and the blue summary cards using authoritative event data, plus the agreed catalogue
mapping controls, account-saved Stats guidance preference and Super Admin artwork edits.
No percentile/dry-streak feature, event price editor, quantity workflow, catalogue import
UI, scoring change, extra diagonal lines or post-completion Luck cutoff.

### 17.2 Existing owners and verified evidence

- `Submission` and `SubmissionContribution` retain credited identity, submission time,
  approval/reversal and weighted progress. Count an eligible approved submission once
  for drops/GP; do not count its contribution amount as item quantity.
- `PublicBoardService` and `PublicProgressCalculator` already project approved progress,
  cap allocation and derive tile/row/column/board completion in submission order. Reuse
  that calculation rather than creating a competing completion algorithm. Preserve
  retained approval identities and the existing finalization/correction boundaries.
- `CatalogueItem.ExternalIdentifier` and `BossActivity.ExternalIdentifier` already exist.
  Item prices and event price snapshots do not. Catalogue editing is in the existing
  Admin Catalogue page; extend its editor and `CatalogueSnapshotService` round-trip.
- `WiseOldManClient` requests singular `metric=ehb` and extracts EHB only. The existing
  synchronization service owns provider throttling, leases, generations and assignment
  fingerprints; extend it rather than adding a second competition synchronization job.
- A user-supplied browser response from competition 145197 verified plural requests
  for EHB, Vorkath and Zulrah: all 93 participants had all three deltas. It also verified
  the -1 sentinel shape. This does not verify every catalogue metric, mode overlap,
  request-size limits or the legacy singular parameter. Do not commit participant data;
  use synthetic contract fixtures. Terminal access encountered Cloudflare restrictions.
- The current activity projection aggregates current playing-character assignments and
  memberships. FUNCTIONAL_CONTRACTS.md §9.6 explicitly allows full competition deltas
  from every regular PLAYING assignment, excluding informational alts. Preserve this
  supplemental-activity scope; evidence still uses its separately retained active-account
  credit. Do not invent a per-swap WOM interval reconstruction requirement.
- Both manual and scheduled starts live in `EventLifecycleService`; each writes within
  a serializable transaction and records `ActualStartedAt`. Both need the same snapshot
  integration, including rollback/retry behavior.

### 17.3 Ordered implementation passes

**Pass 1 — catalogue mappings and usable item prices.**

Extend existing source/item editors with the approved expandable API section: suggested,
editable WOM metric / exact Wiki item ID, explicit validation and distinct unconfigured,
verified, unsupported and temporarily unavailable states. Editing an identifier invalidates
its prior verification. A provider outage does not block an otherwise valid save.
Show the matched item identity so variants can be checked. Preserve shared-item semantics,
authorization, audit and personal drop-rate assumptions.

Add catalogue GP value, source/manual provenance and observation timestamps; zero remains
a real value. Refresh API-backed values through a bounded bulk Wiki operation with a
descriptive User-Agent on every Wiki API request, including mapping/validation and price
fetches: `DKLegacy - Community bingo item pricing - Discord: @chrisschmidt` (user-selected value and contact). Do not use the
HTTP library's default agent. The Wiki requires the descriptive agent; contact information
is optional and must only use an operator-provided public contact. Include a request-header
assertion in the HTTP contract tests. Apply bounded timeout/retry behavior. Preserve
explicit manual values; default untradeables to zero. Extend existing operator snapshot tooling without adding an import
page. Produce a coverage report for existing items and sources; never mutate a user-owned
catalogue simply to validate the plan. New items require a fetched or entered value.

Proof: synthetic HTTP success/partial/outage/invalid-ID cases, exact variant matching,
manual-versus-API precedence, shared-item editor persistence/audit and snapshot round-trip.
Rehearse additive migrations on an isolated PostgreSQL fixture. No live catalogue edit
or deployment is part of this pass's checks.

**Pass 2 — freeze event prices at start.**

Implementation authorized by the user on 2026-09-15. Assigned to a separate GPT-6 Astra
xhigh implementer; Pass 1 API-panel visual approval remains pending independently.

User clarification, 2026-09-15: catch missing catalogue prices earlier, when adding a
tile with an unpriced eligible drop. Explicit zero is valid. Objective tiles with no
drops remain valid and must not be blocked by this rule. Preserve this prerequisite
through board approval/publication validation, with focused mutation-boundary tests.
Do not interpret this decision as approval to freeze permanently unavailable values.

Store one immutable item-value snapshot per event/item with amount, selected hour and
fallback/source provenance. Use the last completed UTC hour before actual start, midpoint
of its buy/sell averages rounded to GP, one side when only one is present, otherwise the
stored catalogue value. Zero must never be treated as missing. Prepare/cache provider
responses outside lifecycle transactions; commit the selected snapshot atomically with
both manual and scheduled starts. API failure uses the agreed catalogue fallback and
must not introduce a new provider-dependent event-start blocker.

Additional user-approved price protection: operator-checked initial prices establish
trusted catalogue values. Later suspicious API candidates retain the last trusted value
and are flagged, including at start-time selection. User approved the 0.5×–2× guard:
strictly outside that positive-baseline band is rejected, exact boundaries accepted;
positive↔zero changes are flagged. Preserve manual/untradeable precedence. Record candidate
and time on the existing catalogue item and report through existing feedback/operator
paths, separate from mapping validation. Rejected start candidates use trusted fallback
with explicit rejection provenance. Test boundaries, zero transitions, missing baseline,
trusted-value preservation and flag lifecycle. No manual frozen-event correction flow;
volume/history sophistication is deferred. Snapshot immutability remains unchanged.

Capture available values across existing catalogue items at start so later board
corrections can reuse those frozen values. Unused legacy items with neither a usable API
price nor a catalogue value do not block start and receive no invented price row.
User clarification: any item without a frozen value introduced after start must have a
stored catalogue value; freeze that value at introduction, including explicit zero.
This applies equally to new identities and previously unused/unpriced identities.
Do not recover an original-hour price for late introduction or retain a separate set of
originally unpriced IDs. Record accurate source/introduction time. Retrying/resuming or
catalogue changes must never replace a snapshot. No historical backfill during migration.

Proof: UTC hour boundaries, delayed scheduled start, manual early start, rounding, partial
prices, fallback, concurrent/repeated starts, transaction rollback and preservation after
catalogue changes/resume. Extend lifecycle PostgreSQL tests, not just calculator tests.

**Pass 2 implementation/review checkpoint — 2026-09-15.** Implementation and one
independent Astra High source review plus the same-reviewer F1/F2 recheck passed.
The named corrections clear stale rejection metadata on mapping changes and localize
the missing-price readiness/failed-start outcome. Executed evidence: 75 focused integration
and 28 pure pricing cases, followed by 12 named correction checks; Web Debug/Release and
EF consistency pass. CURRENT_STATUS.md records exact evidence and the existing integration
CA1310 exception. Catalogue outcome visual acceptance and initial operator-checked price
population remain outstanding. No frozen Stats UI change, user DB apply, deployment or
Pass 3 is included in this completed implementation/review boundary.

**User visual-acceptance timing, 2026-09-15:** Consolidate a single user-facing checklist
once all five passes are implemented, including earlier catalogue controls and price/action
outcomes alongside the production Stats page. Do not require intermediate visual sign-off
to continue authorized implementation. UI_PAGE_MATRIX.md owns deferred approval, and the
existing Stats section of MANUAL_TEST_CHECKLIST.md owns the final checklist; automated
checks and independent source review remain required per pass.

**Pass 3 — cached WOM boss activity.**

Implementation authorized on 2026-09-15 after Pass 2 independent review passed.
A separate GPT-6 Astra xhigh implementer owns this pass. Full requested-metric coverage
and exact mode-overlap verification are its initial execution gate; unverified sources
remain unavailable until supported by evidence. No renewed broad readiness review is needed.

Request EHB plus the distinct mapped metrics required by approved event objectives, using
plural `metrics` parameters. Preserve existing EHB behavior and synchronization fencing.
Retain each character/metric's start, end and gained values, upstream/fetch times and
coverage status; ignore the response's heterogeneous `total`. Missing boss metrics must
not discard usable EHB. Read from persisted cache, never fetch WOM on each Stats visit.

Validate source mappings against supported metrics. In particular, establish whether
raid normal/mode counts overlap and map them without counting the same completions twice.
The saved catalogue currently lacks identifiers for Maggot King and Zalcano; validate
those candidates rather than treating missing configuration as unsupported content.
Respect configured personal probabilities and rolls: no second team-size division.

Implement §15.1's ranked/unranked, zero, missing, estimated and stale-result rules exactly.
Aggregate each participant's regular PLAYING accounts under FUNCTIONAL_CONTRACTS.md
§9.6, excluding informational alts; retain the current assignment fingerprint and team
membership safeguards. Evidence remains credited through the existing active-account
rules. Full competition deltas are the established supplemental activity approximation;
do not introduce per-swap snapshot requests or change evidence eligibility. Cover multiple
regular accounts, swaps and assignment replacement in the focused regression fixtures.

Proof: synthetic multi-metric response fixtures, absent metrics, every agreed -1 case,
partial/failing provider, stale lease/generation, changed assignments and EHB regression.
Use PostgreSQL synchronization tests for cache replacement and concurrent fencing.

**Pass 3 implementation/source-review checkpoint — 2026-09-15.** Cached boss activity,
first-approved basis/binding, source/assignment/generation fences and direct integration
corrections are implemented. One independent Astra High source review passed with no
scoped findings. Evidence: 69 unique integration/HTTP cases across the focused passing
runs, 13 pure rules tests, Release Web and EF consistency; CURRENT_STATUS.md records the
run union, corrected fixture failures and existing CA1310 exception. Full live requested-
metric coverage and semantics for CoX/CM, ToB/HM, ToA/Expert, Gauntlet/Corrupted and
Nightmare/Phosani remain unverified. These mode sources remain unavailable; this checkpoint
is not full provider sign-off, visual acceptance or authorization to start Pass 4.

**Pass 4 — Stats read model and calculations.**

Authorized 2026-09-15, assigned to a separate GPT-6 Astra xhigh implementer. The user
accepts deferring the outstanding real WOM request/mode-semantic checks until final
acceptance; these do not block implementing/testing this pass against controlled contracts.
Keep unverified source results unavailable and actual-batch coverage checks enforced.
Provider verification remains required before enabling affected real Luck results; do not
mark the deferred gates passed or treat synthetic fixtures as provider proof.

Add a focused application Stats query contract and infrastructure projection. Reuse
existing public event access and progress calculation; keep business calculations out
of Razor/JavaScript. Read a consistent evidence/data version per response.

- Drop value: one frozen price per qualifying submission; cumulative submission-time
  totals, team/player shares and scoped valuable drops. Repeated tile eligibility and
  progress weights must not multiply GP or drop counts.
- Luck: deduplicate eligible item/source outcomes, sum `activity × rolls × probability`,
  and apply the shared bounded probability-ranking score in PRODUCT_REQUIREMENTS.md
  (user replacement, 2026-09-16). Pool count distributions, not scores. Carry estimated,
  incomplete, waiting and update-time information alongside the value. A temporary WOM
  failure retains the last successful calculation with its evidence/activity revisions;
  do not combine a new numerator with an old denominator and label it current. A retained
  result invalidated by an evidence reversal must not masquerade as current authority.
- Board progress: use approved contribution history and existing capped completion rules,
  rows plus columns, correct board dimensions and real totals. Preserve hover-time totals.
- Milestones: derive first submission/tile/line/board, halfway and collective team
  thresholds from those same projections. Start stays first; reached milestones sort
  by submission-derived time with stable ties, remaining milestones keep default order.
  Ended unreached milestones display Not reached. Use actual lifecycle start/end times.
- Blue cards: derive the most frequent eligible item and distinct tiles contributed by
  a player from approved evidence; use deterministic ties and retained artwork identity.

Do not publish Stats as an alternative official placement calculation. Read finalized
history through existing finalization semantics. Resolve unavailable historical data
explicitly rather than fabricating timestamps, rates or prices.

Proof: out-of-order approvals, reversal, duplicate eligibility, weighted submissions,
multiple drops from one completion, multiple personal reward rolls, pooled team results,
zero/missing activity, retained item identity, corrections and finalized history.
Concrete math cases: 100 kills at 1/100 and one drop = 0%; 200 kills with eligible rates
1/100 and 1/200 and four drops = +33.333…%; 100 kills with two 1/100 rolls and two drops
= 0%. Verify the complete query with isolated PostgreSQL evidence, not only pure maths.

**Pass 4 implementation/review checkpoint — 2026-09-15.** Stats projections, evidence
revisions and compatible full Luck checkpoints are implemented. One independent Astra High
source review found three P2 issues; the original implementer corrected tied-timestamp
aggregate ordering, scheduled-end checkpoint capture and additive-approval stale retention.
The SAME reviewer passed their bounded recheck with no remaining findings. Initial evidence
is 55 unique PostgreSQL cases; 23 focused correction/direct-regression cases passed, with
unaffected evidence reused. Release Web, EF consistency, scoped hashes and diff checks pass;
CURRENT_STATUS.md records exact logs and the existing CA1310 integration exception.
Provider/mode verification and consolidated visual acceptance remain deferred. The approved
prototype is unchanged. Pass 5 was subsequently authorized below; packaging and deployment remain unauthorized.

**Pass 5 — connect the approved UI and persistence controls.**

Authorized on 2026-09-15 after Pass 4 independent review and same-reviewer recheck passed.
A separate GPT-6 Astra xhigh implementer must port the actual approved source under the
strict UI_PAGE_MATRIX.md contract. Provider checks and consolidated manual acceptance
remain deferred to the completed implementation; no publication is authorized.

Add the event-scoped Stats Razor route and shared event-navigation entry, following the
existing event route/access conventions. Integrate the actual approved markup/CSS/JS
as specified in §17.1; replace fixture inputs with safe DTOs containing real stable IDs,
labels, dates and states. Necessary Razor, localization, authorization and persistence
adaptations must preserve the approved UI. Record and agree any necessary visible or
interaction deviation before implementing it. Retain team drilldown/back, top-five-plus-comparison,
player search, bounded lists, sticky Luck comparison, scoped valuable drops, milestone
filtering and Board progress inspection. Translate approved guidance to explain the
current bounded probability-based Luck model and honest missing-data states without adding bulky rows.

Approved real-data adaptation: Luck's shared bar denominator may grow from its minimum
60 to the largest absolute score in the full current view (including comparison), so real
outliers remain within the approved half-track. Keep exact labels, proportional lengths,
section/row geometry and animations; no scroll-position-dependent scaling. UI_PAGE_MATRIX.md
records the user's explicit approval of this one integration change.

Save the page-level Hide tooltips preference on the authenticated account. Persist the
approved per-item artwork adjustments with Super Admin-only writes and shared rendering.
Reuse existing account preferences/settings storage where suitable; add narrowly scoped
persistence only where no existing owner fits. Keep prototype-only controls out of the
production page. Preserve reduced motion, interruption handling, keyboard access, both
themes and section geometry; do not reintroduce click outlines rejected in the prototype.

Proof includes an explicit source-to-production mapping of ported markup, styles,
renderers and interactions, with a bounded list of integration changes. Review the actual
port for omissions and rewrites; a reference link or passing data tests alone is not UI
acceptance. Also verify route/navigation/access, scoped queries and search, refresh after
approval or reversal, saved guidance across sessions, Super Admin artwork authorization,
concurrency and persistence. Verify 2/3/4/5 teams, overflow at 8/15, narrow stacked layouts,
empty/waiting states and real long names. User supplies visual acceptance; the current
no-browser-automation restriction remains in force until explicitly changed.

**Pass 5 implementation/review checkpoint — 2026-09-15.** The actual approved
markup/CSS/JS/assets are ported into the production Stats route, with account guidance
and Super Admin artwork persistence. The independent Astra High reviewer confirmed the
source port and found four P2 integration issues; the original implementer corrected
team/player identity, authoritative board completion, time-coordinate cache invalidation
and asynchronous refresh/save fencing. The SAME reviewer passed their bounded recheck
with no remaining named or direct-consequence findings. CURRENT_STATUS.md records exact
source mapping and evidence: initial 20 PG/HTTP and 20 DOM/source cases; corrections
3 PG/HTTP and 36 distinct DOM/source cases across the recorded runs; clean Web Release.
The selected older navigation test's Board 404 remains documented, not conclusively
labelled preexisting. All five implementation/source-review passes are complete.
Consolidated production visual acceptance, live provider/mode verification, catalogue
population and whole-slice release gates remain pending. No deployment is implied.

### 17.4 Complexity budget and delivery gates

Persistence ownership is now concrete in §17.7: extend existing catalogue/account/event
owners and add four narrowly scoped record sets for event prices, event Luck outcome
bases, character metric cache and last-valid event Luck checkpoint. This replaces the
previous candidate-only budget; it does not authorize a generic analytics framework. Extend existing sync/lifecycle/
catalogue owners, add one focused Stats query boundary, and one Stats page. No new queue,
third-party chart framework, generic analytics platform or per-player scraping pipeline.
All schema changes include EF migration, designer and model snapshot.

The user-authorized independent Astra High readiness review is complete. After one
bounded recheck of the three named findings, the same reviewer approved planning readiness
with no residual blocker on 2026-09-15. No further broad readiness review is required.
Pass 1/2 are ready for separately authorized implementation; Pass 3 retains its full-metric/
mode contract gates before enabling affected sources; Pass 4/5 depend on those results.
Keep restrictions on delegation and environment use. Promote approved concrete schema/route
contracts to their existing data/functional/UI owners before their production edits.

Each pass receives a bounded brief and focused executable gates at its affected boundary,
then independent source review under §4.4. Use existing README commands and filtered test
projects during development; run the applicable full solution/release gates before slice
completion. Reuse passing evidence unless relevant code changes. No database reset, seed,
provider write, app restart, packaging or publication is implied. Final acceptance includes
the connected production page and the new catalogue/preference/artwork controls, not just
the earlier prototype approval.

### 17.5 Outstanding boundaries and next permitted action

- **Historical import — resolved:** the user excludes retrospective Stats for the
  reconstructed Sommerbingo import. Preserve its existing archived board/history; do not
  manufacture item identities or derive actual drop counts from its contribution units.
- **Items without a frozen value added after start — resolved:** require and freeze
  their catalogue value at introduction. Zero is valid; missing blocks addition. Record
  source/time accurately and preserve every existing event price snapshot.
- **Technical checks, not another general design round:** verify all required mode
  mappings and a full requested-metric response before enabling those Luck sources.
  Step 11 live verification on 2026-09-16 returned all 71 supported boss metrics plus
  EHB for all 93 participants in competition 145197. The user separately confirmed
  exclusive KC totals for all five normal/alternate-mode pairs: use their own raw
  deltas directly, without subtraction or combining. This resolves the mode decision
  through explicit operator confirmation, not a claim that the API documentation
  specifies those semantics. Removing the temporary mode gate is authorized.
  Active-account attribution is resolved by the existing full-PLAYING-account contract.
  If an actual mapping/request limitation remains, present that concrete limitation and
  the smallest viable options before implementing the dependent behavior.

The user authorized the readiness check with "Go ahead". Source findings and the
first-pass brief are recorded below. Independent planning readiness is approved after the bounded recheck; production
implementation remains pending authorization. The pricing/history choices are resolved. Do not
reopen the approved visual design or the final Luck formula.


### 17.6 Planner readiness findings — 2026-09-15

Status: bounded source/data inspection completed; this is not an independent review or
an all-passes readiness approval. No subagents were dispatched under the current delegation
restriction. No production code, prototype files or user-owned database were modified.

**Resolved and verified locally**

- The source-controlled catalogue contains 68 sources, 311 items and 441 source drops.
  All 311 item external identifiers are empty; 66 source identifiers are populated.
  Consequently none of its items can currently join the numeric price-response keys.
  This is a mapping prerequisite, not evidence that the API lacks those item prices.
  These counts describe the saved JSON, not a scan of the deployed database.
- The supplied hourly price JSON parses successfully: timestamp 1789470000 identifies
  2026-09-15 11:00 UTC, 2,885 entries, 1,765 two-sided and 1,120 one-sided prices. No
  negative/invalid price was found. Both-side, single-side and missing-item handling
  remain necessary. The response proves the hourly shape, not historical retention or
  catalogue coverage. Test fixtures must be small synthetic examples, not a wholesale
  copy of changing market data.
- All 441 saved drop rates have Participant scope; multiple-roll records exist, including
  seven-roll outcomes. Tests must use expected count `n × rolls × p`; existing probability
  of at-least-one helpers cannot substitute for this calculation.
- FUNCTIONAL_CONTRACTS.md §9.6 resolves the account question: full deltas for regular
  playing accounts are already permitted. Preserve that boundary and informational-alt
  exclusion. No new per-swap provider synchronization is needed by this plan.
- Historical import limitations extend beyond GP: reconstructed counters do not identify
  actual drops. Never label synthetic imported timestamps as newly verified milestones
  or derive real item statistics from weighted historical units.
- Existing named `OsrsWiki` HTTP client targets the article API host and has a catalogue
  dry-run User-Agent; `OsrsWikiImages` owns images. Add a specifically named price API
  client for `prices.runescape.wiki/api/v1/osrs/`, using the exact approved header
  `DKLegacy - Community bingo item pricing - Discord: @chrisschmidt`. Keep unrelated
  image/article requests unchanged unless a direct integration requires their modification.
  Verify the raw header value in a request test; .NET structured User-Agent parsing must
  not force changes to the user-selected contact string.

**Provider evidence and remaining executable contract check**

[WOM competition details documentation](https://docs.wiseoldman.net/api/competitions/competition-endpoints)
specifies plural metric parameters and per-metric deltas. The supplied three-metric
response independently supports that shape. A retrieved cached upstream source revision
still showed the older singular parameter; it is not reliable evidence of the currently
deployed implementation or a numeric request limit. Use the documented plural request,
preserve EHB explicitly, and verify every requested metric/participant before accepting
a complete cache. Do not infer unlimited request sizes or automatically split across
responses with incompatible upstream versions.

The catalogue has distinct CoX/CM, ToB/HM, ToA/Expert, Gauntlet/Corrupted and Nightmare/
Phosani identifiers. The current inspection did not establish all providers' mode-count
semantics. This remains a named Pass 3 contract check; do not apply speculative subtraction
or mark all source mappings verified from their names alone. A verified unsupported
source must use the agreed unavailable state, not zero expected drops.

**Concrete first-pass brief, prepared for implementation authorization**

- Outcome: existing Admin Catalogue source/drop editors can store and validate the correct
  provider identifiers and a usable per-item catalogue value, with accurate outage states.
- Owners: `CatalogueItem`, `BossActivity`, their EF configurations, existing Admin Catalogue
  page/model, `CatalogueSnapshotService`, the new price-client registration and focused
  application/infrastructure integration boundary. Extend these owners; no replacement
  editor, catalogue import UI, separate item-management page or pricing correction page.
- Storage: reuse existing ExternalIdentifier fields; add nullable catalogue GP value
  during migration plus price provenance/time and mapping verification metadata. Preserve
  missing legacy prices as missing until populated; never migrate them all to zero. Keep
  explicit-manual and API-backed values distinguishable. Mapping changes invalidate the
  verification and any API-derived price association tied to the old ID; a shared item
  must not retain the wrong variant's value silently.
- Population: use the bulk mapping endpoint to propose exact names/IDs, then price once
  per bulk response. Require explicit handling of ambiguous names/variants; no fuzzy
  automatic match. Identify tradeability before choosing zero. Provide operator-readable
  unresolved records and a retry path. No user-owned data mutation without authorization.
- Minimum controlled fixtures: known mapped tradeable item, both-side and one-side hour,
  absent tradeable price with existing fallback, untradeable zero, explicit manual value,
  missing legacy value, ambiguous variant, unsupported boss, temporary provider failure,
  shared item used by two sources and stale concurrent editor request.
- Proof: exact User-Agent, response and validation status tests; pure midpoint/fallback
  cases; isolated PostgreSQL save/audit/concurrency tests; additive migration and JSON
  snapshot round-trip; existing personal probability/roll fields remain unchanged.
  Existing Admin Catalogue visual composition must survive the bounded API-panel addition.
- Stop: complete this connected catalogue behavior and its assigned checks/review only.
  Do not begin event-start snapshots, Stats markup integration, packaging or deployment.

The user resolved both product questions during readiness: retrospective Stats for the
reconstructed import are excluded; the later user clarification now requires catalogue
value at introduction for any item without a frozen event value, frozen once.
PRODUCT_REQUIREMENTS.md §15.1 now owns these decisions. Add historical-route exclusion
and late item introduction (catalogue value, zero/missing, concurrent retry,
preservation of existing snapshots) to the affected Pass 2/5 checks. Independent readiness
was pending at this planner checkpoint; the subsequently authorized independent review
and bounded recheck approved readiness as recorded in §17.4. These source findings alone
are not an execution pass.


**Pass 1 implementation/review checkpoint — 2026-09-15.** Catalogue mappings,
pricing, existing-editor API controls, additive migration, snapshot compatibility and
operator report/apply support are implemented. One independent Astra xhigh source review
and the same-reviewer F1–F3 recheck passed; the originating planner received and reconciled
the verdict. CURRENT_STATUS.md records focused executable evidence and the unrelated
whole-solution CA1310 limitation. UI_PAGE_MATRIX.md retains pending manual acceptance of
the new API panels/action outcomes. The 311 saved catalogue item IDs remain unpopulated;
no user-owned database apply or deployed coverage is claimed. Pass 2 and publication have
not started and are outside this completed implementation/review assignment.

### 17.7 Named readiness corrections and connected acceptance contract

The one independent Astra High reviewer found three readiness gaps: ambiguous retained
source/rate selection, incomplete cache fencing/checkpoint ownership, and missing connected
fixtures/schema contract. The user subsequently chose **Keep the first approved event
rate**. The same reviewer rechecked this reconciliation and approved readiness with no
residual blocker on 2026-09-15. This is planning approval, not executable/visual acceptance.

**Canonical Luck basis and projected fields (Pass 3/4).**

Persist `EventLuckOutcomeBasis` keyed uniquely by `(EventId, SourceDropId, ItemIdSnapshot)`.
The catalogue already enforces one SourceDrop per boss/item. Retain boss identity, first
approval/drop snapshot identity and timestamp, probability, rolls and other applicable
personal mechanics from that first approved snapshot. Never take a later mutable catalogue
rate or choose max/latest among conflicting copies. Every duplicate reference contributes
one outcome; evidence is deduplicated by submission ID. First eligibility before start is
still the first approval, as selected by the user. A newly eligible outcome captures its
own first approval. Removed/retained outcomes must retain their basis even when no longer
active; use the approved board eligibility for the requested view without rewriting history.

Retain the validated WOM metric with the event basis, not only a pointer to mutable source
configuration. If initially unmapped, keep the outcome unavailable until a mapping is
validated, then bind its first usable metric and advance the source revision. A later
catalogue mapping edit applies to future bindings/events and never silently rewrites an
existing event binding. No event mapping editor is added by this scope. Preserve provenance
for any exceptional existing correction authority. Pass the frozen roll/mechanics fields
through `BoardPublicationQueries` and its application DTO, which currently omit some fields.

Basis creation belongs in the successful approval/publication transaction alongside the
retained approval snapshot. On migration, do not initialize from whichever active duplicate
is encountered first. Only a uniquely established earliest retained approval can initialize
an existing supported event; conflicting earliest snapshots or missing identity produce an
explicit unavailable basis requiring diagnosis, not guessed rates or double-counting.

**Storage and concurrency owners.**

| Owner | Stored state and boundary |
| --- | --- |
| Existing CatalogueItem / BossActivity | Existing external IDs; add price/provenance/time and validation metadata. Reuse existing entity concurrency tokens. CatalogueItem also owns bounded artwork transforms and their version/audit; no separate artwork table. |
| Existing Account | Stats guidance-hidden boolean, default false; owner-only persistence using current account concurrency. No general preferences service/table. |
| EventItemPrice | Unique `(EventId, ItemId)` with integer GP, original hour, observed/fallback source/time; immutable after insertion. Both lifecycle starts and late-item introduction transact against the event boundary and uniqueness constraint. |
| EventLuckOutcomeBasis | Unique event/source/item key described above; retained first approved mechanics, metric binding and provenance. Changes that legitimately initialize an unavailable mapping advance the source revision. |
| EventCompetitionCharacterMetricActivity | Unique `(EventId, Generation, OsrsCharacterId, Metric)` with start/end/gain, coverage/estimate state, activity batch and source-set revision. Existing EHB row/table remains its owner; missing boss metrics must not destroy EHB. |
| EventStatsLuckCheckpoint | One current checkpoint per EventId, bounded structured payload of player/team received/expected/results/statuses plus evidence revision, activity batch/generation, assignment fingerprint, source/basis fingerprint and calculated/upstream times. It preserves a complete prior calculation, not an old denominator alone. |
| Existing BingoEvent / EventCompetitionSynchronization | Add a Stats evidence revision to the event and source-request fingerprint/batch metadata to synchronization. Retain existing competition, generation, lease and assignment fencing. No second WOM job. |

Four new record sets are justified above; no additional record sets or generic abstraction
are implied. Every migration includes designer/model snapshot and isolated PostgreSQL
rehearsal. JSON checkpoint payload schema is versioned and contains only existing public
Stats identities/results; provider missing-account diagnostics remain Admin-only.

A source-request fingerprint covers the active approval/outcome set, retained metric
bindings and mechanics, including unmapped required outcomes. Capture it before a fetch,
then recheck it with competition/generation/lease/assignments when committing and when
reading. A response for an older board must not become a complete newer-board result.
A complete EHB result can coexist with incomplete Stats metrics. A full metric test may
remain a Pass 3 gate; it cannot be replaced by assuming the three-metric sample is complete.

Checkpoint creation occurs after a successful compatible activity batch, or after an evidence
change while that batch is still current and usable. Advance Stats evidence revision
transactionally on relevant approval/reversal/correction writes; update the checkpoint only
if its entire evidence/activity/source/assignment key still matches under the event write
boundary. Use one consistent read snapshot and compare-and-write, preventing an older
calculation overwriting a newer revision. On provider failure retain the checkpoint and
its original times; indicate that it is stale. Reversal, incompatible source/assignment/
competition change or finalization transition invalidates authoritative presentation of an
old checkpoint. Do not show a superseded result as current or as a new calculation. Use the
agreed waiting/incomplete state until a compatible result exists; do not add stale numerators
to current denominators. These checks extend existing mutation/sync owners, not a new queue.

Named PostgreSQL proofs: board/new objective during fetch; mapping binding during fetch;
complete EHB but missing boss metric; stale checkpoint writer; approval then earlier approval;
reversal during outage; account/competition replacement; duplicate outcome with later rate;
finalization/archive and legitimate unfinalization preserving official completion semantics.
Pass 2 additionally tests crossing a UTC hour between HTTP preparation and start commit:
select the actual-start bucket or its catalogue fallback, never the previous prepared bucket
as if it were the required one.

**Route, handler and approved-code ownership (Pass 5).**

`Pages/Events/Stats.cshtml(.cs)` owns `/Events/{slug}/Stats`, its read model and authenticated
preference/artwork POST handlers (anti-forgery, owner/Super Admin authorization). Add Stats
to shared event context/navigation, including `SharedShellService`'s explicit event-route
recognition, without changing approved header/masthead composition. Use the existing public
published-event access predicate; hidden/private/unpublished/unknown and excluded imported
event routes must not leak data or fall back to the preferred event. Reuse the existing
cancelled-event presentation rather than exposing statistics for a cancelled event.

The actual `.gp-panel`, `.luck-panel`, `.repeat-drop-card`, `.timeline-panel`,
`.versatile-card`, `.race-panel` and `#artwork-editor` markup/styles/renderer logic are port
inputs. Source-to-production mapping must identify their destinations. Remove fixture-only
bootstrap and event listeners for omitted demo controls, not the approved section behaviors.
Specifically replace artwork's reads of `#drop-preview` with the real displayed item's
stable ID and move necessary accessible feedback off `#sample-size-status`; remove
unconditional initialization of absent event-size/stage/theme/header controls. Verify the
page and artwork editor initialize with all excluded controls absent. Keep app theme state
as input to the existing Stats colors/geometry. Do not restyle or rewrite interactions.

**Minimum coexisting controlled fixtures and connected journeys.**

Reuse `Vinterbingo 2026` (`test-15-dkl-live`), its existing SeedEvidenceCaptain,
SeedEvidenceCoCaptain and SeedEvidenceParticipant, the bootstrap Super Admin and SeedAdminTwo.
Reuse the private published-board setup `test-62-board-publication-setup` for the manual-start
journey. Add only an isolated scheduled-start counterpart, hidden/unpublished/excluded-import
access fixtures and the synthetic catalogue/price/activity states needed below. Never use
real historical participants or the user's running database. Prepare isolated test stores
or the already authorized Development fixture mechanism; do not run its destructive reset
without separate authorization. Manual and scheduled starts must not depend on resetting
one another; keep terminal finalization checks last, after Live interactions.

| Pass / actor / entry | Connected journey and expected result | Next step / proof |
| --- | --- | --- |
| 1 / Admin / existing Catalogue source/drop editor | Edit mapping → validate → save → reopen shows exact identity/value/provenance. Missing/manual/zero/API/outage cases remain distinct. | Change shared item from a stale second editor: concurrency rejection; retry without losing personal rates. HTTP + PostgreSQL, then manual panel acceptance. |
| 2 / Super Admin / event Manage; scheduler / due-start worker | Manual start and separate scheduled start freeze the actual-start hour atomically; one-sided/missing prices use defined fallback. | Change catalogue prices and introduce a new item: existing values stay fixed; late item freezes its required catalogue value at introduction. Rollback/retry/concurrent start proofs in PostgreSQL. |
| 5 / anonymous, ordinary account / existing event navigation and direct Stats URL | Published supported event shows the approved Stats UI; refresh/deep link resolves same event. Hidden/private/unpublished/unknown and excluded import reveal no Stats. | Anonymous mutation denied; signed-in preference changes affect only owner. Access integration plus user visual walkthrough. |
| 3/4/5 / Captain submission flow → Admin Review → Stats | Submit two eligible items, approve later-time first then earlier-time: GP/progress/milestones use submission chronology; reverse and refresh consistently. Weighted progress does not multiply item count. | Inspect each scope, chart hover-time and valuable drops; compare retained progress calculator, then outage/retry states. PostgreSQL query and manual interaction checks. |
| 3/4 / synthetic provider + Admin board correction | Pending fetch → add objective/rate-duplicate → old response cannot satisfy new source set. Outage retains only a compatible labelled checkpoint; reversal invalidates current presentation. | Retry complete batch: correct unranked/estimated/zero/missing state and pooled Luck. No API write or real participant fixture. |
| 4/5 / Admin lifecycle → archived Stats | Correct approved evidence, finalize then archive in existing supported lifecycle; Stats completion agrees with retained official semantics. Legitimate unfinalization follows existing authority. | Old price/rate identities stay retained; invalidated results refresh without fabricating imported history. Integration test and final manual lifecycle step. |
| 5 / ordinary account / page guidance | Hide guidance → leave/revisit/sign in again: preference retained. Another account is unaffected. | Reopen/show through approved in-section controls; default/missing preference shows guidance. Owner-write and manual persistence checks. |
| 5 / Super Admin / Adjust artwork | Open actual displayed item, drag/scale/rotate; Cancel changes nothing, Save persists, Reset follows existing editor semantics. | Reload and another viewer see saved artwork; ordinary-user forged POST denied, stale saves controlled. PostgreSQL + user visual comparison. |

Catalogue fixtures include one shared item/two sources, exact variant ambiguity, untradeable,
manual/zero/missing values and one-/two-sided/absent prices. Stats fixtures include two regular
accounts for a participant plus an informational alt, duplicate item/source across tiles,
a later conflicting rate copy, two drops from one completion, weighted contribution,
zero and -1 source activity, missing metric, late approval and reversal. Team-count and
long-name visual variants use controlled inputs; do not add production density selectors.
No production acceptance is marked passed by specifying these fixtures. The manual sequence
is mirrored in MANUAL_TEST_CHECKLIST.md's Stats section and remains unexecuted.

## 18. Tile KC/Luck sidebar addition — approved 2026-09-16

### Outcome and boundaries

Add one section to the existing team tile sidebar using TeamBoard's EHB/Drop EHB
markup and scoped shared visual rules. Team summary: Team total, Luck, KC. Expandable
Contributors: Luck, participant name, KC. Label separate boss/mode counts when a tile
has multiple relevant metrics; do not add a combined heterogeneous KC total. The
user chose only drops credited to the selected tile for Luck's received numerator.
PRODUCT_REQUIREMENTS.md section 15.1 and the functional tile contract own calculations.

Reuse existing cached metric/Luck owners in PublicStatsService and the public board
publication projection. Cover initial and enhanced TeamBoard tile rendering plus the
shared inactive Tile scaffold with one presentation. Direct tile URLs are owned by
TeamBoard; Tile.cshtml has no registered route and must not gain one. Starting owners: Application Stats/Boards
contracts, Infrastructure Stats/PublicBoardService/BoardPublicationQueries, Events
TileSidebarView/_TileSidebar/TeamBoard/Tile and site.public-ui.css. Extend only directly
required dependencies; no provider fetch on click, new tables/services/jobs, migrations,
whole-site styling, Stats card tie redesign or unrelated fixes. Existing approved UI,
authorization, event history, prices, snapshots and submission/evidence behavior remain
protected. Current user-supplied AGENTS supersedes historical local ticket trial rules.

Tile/team calculations may extend the existing Luck checkpoint JSON with optional
results so retained tile received/expected/KC values stay from one coherent snapshot.
Existing checkpoints without tile attribution use a currently usable activity batch
or show waiting for activity data; never reconstruct a stale tile numerator from fresh
evidence. This compatibility decision adds no table/migration and must preserve existing
event Luck behavior and read-only page access.

### Execution and checks

One fresh Astra xhigh implementer owns the connected addition and focused executable
checks. A separate fresh Astra high reviewer checks the exact addition against its
pre-change snapshot, including direct consumers. Planner owns scope and reconciliation;
no planner production edits or self-review. No readiness review or repeated walkthrough.

Focused checks must distinguish tile-only received counts, multiple eligible outcomes
sharing one metric, separate boss/mode counts, multiple playing accounts and excluded
informational alts, pooled Luck, missing/unranked/estimated activity, stale evidence
coherence, reversal/correction and no-drop objectives. Reuse existing tests where they
cover unchanged rules. Exercise PostgreSQL-backed query behavior and real HTTP nested,
and enhanced sidebar routes, including direct reload and public visibility. Execute contributor
expansion/keyboard behavior in browser where available. Check scoped CSS/diff and Release
build. Report environmental blockers accurately without repeating failed commands.

User visual acceptance applies only to the new section, using the supplied screenshot
and current EHB/Drop EHB composition; existing page approval remains intact. Stop at
this addition's review/acceptance boundary. No staging, commit, push, deployment,
fixture refresh or other pass. Runtime uses http://127.0.0.1:5189; preserve its private
environment and controlled database. Coordinate any necessary application restart with
the planner after checks, without resetting/reseeding data.

### Section 18 implementation checkpoint — 2026-09-16

The fresh Astra xhigh implementer completed the addition and 16 focused PostgreSQL/HTTP
cases passed. Fresh Astra high source review found one scoped CSS specificity defect;
the implementer corrected it and the same reviewer cleared the named recheck. Planner
browser checks passed contributor click/Enter/Space, enhanced tile switching/Back and
desktop/mobile computed spacing/rendering. UI_PAGE_MATRIX.md retains user visual acceptance
as pending. Evidence/commands and runtime are recorded in CURRENT_STATUS.md's active handoff.
Production Release compiled; strict solution build still hits pre-existing CA1310 in
navigation tests, unchanged by this addition. Focused test builds downgraded only CA1310.
No packaging, deployment or next pass was performed or authorized.

### Tile KC/Luck visual correction — user directed 2026-09-16

The initial new section's appearance was not accepted. Copy the existing Drop EHB/EHB
row typography, spacing, dividers and number styling directly. Summary row is just
TEAM TOTAL / signed Luck percentage / green numeric count; contributor rows replace
01/02 rank with signed Luck percentage, then player name and green +count. No visible
Luck label below the percentage, no KC suffix and no repeated boss name in single-metric
rows. Zero and positive Luck are green; displayed negative values (-1% and below) keep
the existing negative color. Keep a zero count numeric, not missing. Missing/estimated/
stale status remains honest. Multiple boss/mode counts remain separate, with labels
outside the numeric cells only where needed to distinguish the metrics. No calculation,
query, checkpoint, route or account attribution changes. Reuse the existing team rail
classes; only layout accommodation for wider percentage rank is permitted. Scoped
source/cascade/diff verification and Razor build/affected HTTP render suffice; preserve
previous backend evidence. User visual acceptance remains pending after this correction.

The visual correction is implemented in the two scoped UI files. Fresh Astra xhigh
implementation and fresh Astra high focused review completed; no remaining source
findings. Web Release/Razor build, shared-style parity/diff checks and 13 .NET formatting
cases passed. Planner reloaded the preview and confirmed the requested simple rows/colors
and disclosure in a live render. User acceptance remains pending; backend evidence is
retained without another broad pass. Evidence paths are in CURRENT_STATUS.md.

User accepted corrected styling ("Looks great") and requested only section order:
place Eligible drops immediately above KC & Luck without changing either section's
styling. Move the existing `_TileActivity` partial after the eligible-drops block and
before Approved submissions; preserve all other behavior. Scoped diff/order check and
Razor build suffice; no new tests or renewed broad review.

Section reorder completed with exactly one partial relocation. Focused source recheck,
Web Release/Razor build and live route order check passed; styles and calculations are
unchanged. User's preceding styling acceptance remains recorded in UI_PAGE_MATRIX.md.

Tile contributor filtering — user directed 2026-09-16: only show contributors whose
KC is known and > 0 in the displayed boss/mode group. No empty group headings or
empty Contributors disclosure. Apply in presentation only; preserve summary totals,
Luck, uncertainty semantics, accepted styling and section order. Use one focused
executable render check for positive/zero/unknown and multi-metric filtering, Web
Razor build, and bounded functionality/source review; no subjective UI review or broad
backend rerun. User owns appearance acceptance.

User verification direction (2026-09-16): **Do not browser inspect.** For this work,
finish with code/build checks and automated functionality tests. User supplies UI
inspection; no agent browser walkthrough or subjective visual review.

Stale tile-data correction — user directed 2026-09-16: stale activity must retain
last-known compatible KC and contributors, with the stale timestamp. This also applies
to legacy event checkpoints that lack tile projections. Reconstruct a tile Luck score
from retained data only when evidence revision and the existing compatibility checks
prove a coherent numerator/denominator; otherwise retain KC and explicitly mark tile
Luck unavailable while awaiting a coherent update. Do not clear known KC simply because
freshness expired. Preserve reversal/assignment/source/lifecycle invalidation, team and
metric scoping, and read-only page access. No fixture/provider refresh is a substitute
for this correction. No browser inspection; use focused executable functionality tests.


## 19. Boss KC leaderboards and MVP display — approved 2026-09-17

### Scope and sequence

User approved PRODUCT_REQUIREMENTS.md section 16.1 and authorized readiness followed
by implementation if no blocking decision remains. This slice goes first. Objective
breakdown wording and multi-boss tile-sidebar spacing are inventoried but DEFERRED
until this slice receives user approval; no minor-change implementation is authorized
in this pass. No publication, staging, commit, push, merge, deployment or user-owned
runtime/database change is authorized.

Assignment: `/private/tmp/BingoWebpage-fix-wiki-image-fetch`, branch
`codex/fix-wiki-image-fetch`, baseline HEAD `ae1a8605372a613e53d87b73a8e0237c84c1faca`.
Preserve the pre-existing CURRENT_STATUS handoff edit. The user-owned Documents checkout
and its dirty work are out of scope. The current HTTPS7131 preview and both databases
stay untouched. No browser/CUA inspection; the user performs visual acceptance.

One read-only Sol High readiness review precedes production edits. Planner resolves
named findings/decisions and reconciles this plan. Then one bounded Luna Max implementer
owns the connected change and focused checks, followed by one fresh independent Sol High
reviewer after implementation stops. Named remediation returns to that same reviewer.
No routine verifier, second implementation chain, polling or wait loop. Every worker
must send one end-of-turn callback via `mcp__codex_app__send_message_to_thread` to
originating planner task `01a0b085-75ee-79b0-b40b-bba65b669d74`, reporting completed work,
next owner, review status and blockers. User explicitly authorizes these scoped handoffs.

### Reuse and complexity boundary

Extend the existing Board page/model, public board/activity/Stats projections and
existing cached event character metric data as necessary. Starting sources:
`src/Bingo.Web/Pages/Events/Board.cshtml` and its page model;
`src/Bingo.Web/wwwroot/js/public-leaderboards.js`; shared dropdown behavior/styles;
`src/Bingo.Infrastructure/Boards/PublicBoardService.cs`;
`src/Bingo.Infrastructure/WiseOldMan/EventCompetitionActivityProjection.cs`;
`src/Bingo.Domain/Integrations/WiseOldMan/EventCompetitionCharacterMetricActivity.cs`;
existing Stats metric/Luck queries and relevant Application contracts.

Budget: zero new tables/migrations, provider jobs/calls, pages/routes, permissions,
services, dependencies or generic abstractions. Local DTO additions/fields and bounded
helpers inside existing owners are permitted when required. No new table component or
visual system: adapt existing table markup, shared styles, sorting and disclosure.
Reuse the masthead compact dropdown; style its leaderboard trigger like existing tabs.
No changes to Drops search, luck formulas, board progress, attribution/history rules,
catalogue data or stale-data validity safeguards. Readiness must identify any concrete
need that cannot fit this budget before implementation.

### Journeys, boundaries and planned evidence

All public journeys begin at a published event's existing Board navigation and
Leaderboards view, with anonymous/read-authorized access unchanged. These are read-only
journeys: neither selection nor viewing writes event data or fetches provider data.

| Journey / start | Action and expected result | Proof / boundary |
| --- | --- | --- |
| Published event, with/without linked WOM | Enter Leaderboards; default EHB & Drop EHB retains its three views; boss choices are relevant distinct metrics only when linked | Focused real HTTP route/render evidence; old query defaults and hidden/unpublished access preserved |
| Linked event with two boss metrics and repeated tile placement | Select boss, switch Teams/Players, reload and follow existing navigation | Rendered route/query plus affected JS execution; no duplicated KC or new provider requests; selection stays coherent |
| Team with positive/zero/missing values and multiple playing accounts | Read totals, contributor count/average, expand, then Players | Focused PostgreSQL projection and render tests; combine playing accounts, exclude informational accounts, no zero contributors; zero-contribution team remains without empty disclosure |
| Complete, stale, estimated, partially missing or unavailable metric data | Read Start/End/Gained and switch metrics | Existing compatible stale values remain; missing is not zero; preserve assignment/source/lifecycle invalidation and underlying estimates; known values are not hidden merely due to age |
| Player with approved boss drops across tiles | Click coral numeric Drops count | Follow emitted link to existing Drops route with boss search and team filter; whole-team results are intentional. Test link construction/navigation, not a new boss-search investigation |
| One MVP, tied MVPs and no contribution | Read boss, EHB and Drop EHB team MVP cells | Focused projection/render checks: name +value or Multiple MVPs +shared value, never summed; no MVP without contribution |
| Existing EHB/Drop EHB views | Compare baseline after selector use | Regression proof preserves calculations, player visibility, columns and interactions outside MVP cells and approved Rank localization; retain original signed formatting |
| Desktop, narrow width, long label, standings expanded/collapsed | Open selector, choose metric, expand table and change views | Shared dropdown/leaderboard JS tests and source/cascade comparison; actual visual/keyboard/browser acceptance remains with user |
| Retained/finalized event and refresh failure | Read compatible cached metrics where available | Focused existing lifecycle/cache boundaries; no refresh or fabricated historical KC; old events without metric data remain usable |

Use README's existing commands. Readiness identifies the minimum isolated controlled
fixtures and applicable existing tests; no user-owned fixture reset or provider traffic.
Implementation requires affected executable PostgreSQL/HTTP checks, existing affected
Node interaction harnesses, Web Release build and scoped formatting/diff checks. Reuse
unaffected passing evidence; no blanket full-suite reruns for reassurance. Automated
harness/source checks do not constitute browser or visual acceptance. A missing required
execution boundary must be reported with a concrete manual/isolated execution path.

### Review and acceptance

Readiness must confirm data semantics/reuse, entry-point integration, minimal fixtures,
verification coverage and the complexity budget. Report only concrete blocking defects
or decisions; optional improvements do not expand approved scope. Review report lives at
`/private/tmp/bingo-boss-leaderboards-20260917/readiness.md`.

Final reviewer compares the frozen full diff to this plan and explicitly compares boss
and existing tables for unintended UI differences (structure, classes, spacing, type,
colors, number formatting, sorting/disclosure), allowing only the agreed columns/data,
MVP content and selector. Existing EHB/Drop EHB tables may change only in MVP cells and the approved Danish
Rank heading: use Rank in both languages across existing and new leaderboard tables.
Check shared translation direct consumers as needed; no unrelated translation pass.
User visually accepts the selector, standings alignment, wrapping and reused tables;
UI_PAGE_MATRIX owns that approval. MANUAL_TEST_CHECKLIST carries the walk. Neither a
readiness pass nor source review is manual acceptance. After final review/preflight,
stop for the user's visual acceptance; do not start deferred minor changes beforehand.


### Section 19 readiness reconciliation — 2026-09-17

Independent Sol High readiness PASS: no unresolved product decision or budget blocker.
Report `/private/tmp/bingo-boss-leaderboards-20260917/readiness.md`, SHA-256
`aec6d8f9948b314a8e8ccc77a8c9bbaa51858474381236badcc149bf96f93b43`.
Source-only readiness; no tests/provider/browser/runtime/database actions were run.
Planner authorizes the connected Luna Max implementation within this frozen scope.

Required technical integrations from readiness: consume the existing compatible metric
cache/source boundary in a coherent read; never use a direct raw-cache read or mutable
catalogue mapping as historical truth. Deduplicate activity by character/metric and
approved drops by submission. Preserve known partial values with incompleteness metadata,
estimated Start/End/Gained and assignment/source/lifecycle invalidation. Valid selected
metrics with unavailable activity remain selected; invalid/unlinked/non-advertised inputs
fall back to default. Route-backed selection, tabs and Back/Forward must agree.
The existing standings metric display follows the selected boss team totals in boss mode;
official bingo order and default-mode standings remain unchanged. This is a direct
consumer integration, not a new standings ranking rule or table redesign.

Use the readiness report's isolated PostgreSQL fixture and focused route/Node evidence
plan. Scope format verification to changed files rather than expanding the correction.
The implementer records exact isolated manual-preview setup or any execution blocker;
it may not restart the user's current preview or mutate either user database. Explicit
browser/CUA prohibition remains; Node harness evidence is not browser/visual acceptance.
Implementation then goes to one fresh Sol High reviewer; readiness reviewer is not the
final implementation reviewer. Final findings route to implementer, with one planner
callback at each end-of-turn handoff. No new requirements or minor corrections added.


Section 19 technical completion — 2026-09-17: Luna Max implementation and named
remediation complete; same independent Sol High reviewer final PASS, no remaining
technical findings. Report `/private/tmp/bingo-boss-leaderboards-20260917/review.md`;
reviewed patch SHA-256 `8bc192ad83ea79b31c009be18738801933c56016f99a3322a2422810388a902b`.
Evidence `/private/tmp/bingo-boss-leaderboards-20260917/remediation-evidence.md` records
passing focused PostgreSQL/HTTP, affected Node, Web Release, scoped formatting and diff
checks. Whole-solution formatting remains an unrelated baseline failure, not a pass.
Only user visual/keyboard acceptance remains; no additional verifier/suite or deferred
minor-change work is dispatched. This completion record is a documentation-only planner
reconciliation after the frozen technical review; it does not alter reviewed code.


### Section 19 visual-acceptance corrections — user directed 2026-09-17

Prior technical PASS is retained for unchanged backend behavior; user visual acceptance
revealed named corrections. This bounded pass supersedes earlier right-aligned stacked
selector placement and visible freshness-status requirements. Same Luna Max implementer
and same Sol High reviewer handle corrections/recheck. No second readiness review or
broad backend re-review; objective wording/sidebar spacing remain deferred.

1. Fix selector closing: repeated trigger click, outside click, Escape and option
   selection must actually hide the menu. Check both open state and rendered CSS state;
   existing shared dropdown closed-state rules must win the scoped cascade.
2. Keep METRIC: in the trigger only, never in options. Preserve the selected plain-name
   label as the menu and trigger update.
3. All positive gained figures and MVP values use the same existing gain-green token
   and signed + formatting. Include boss Teams/Players/nested accounts and all MVP
   cells; keep raw Start/End, ranks, contributor counts and drop counts unsigned.
4. Remove visible freshness/update/health status labels and timestamps throughout
   leaderboard views, including repeated team/account notices. Keep compatible cached
   numbers visible, unknown values unknown, and preserve all source/assignment/lifecycle
   validity and aggregation semantics. This is a presentation change, not a cache or
   data-quality relaxation. Per-account unknown endpoints must remain explicit dashes;
   do not reintroduce the fixed missing-as-zero/summed-endpoint defect.
5. Remove the boss numeric Drops link arrow; preserve coral count and existing URL.
6. Delay stacking until actual available width requires it. Wide placement is unchanged;
   when stacked, Metric is above the table tabs and BOTH are left-aligned above divider.
   Preserve standings expanded/collapsed behavior and long metric names.
7. User chose muted blue for ALL dark-mode leaderboard column headings, including
   sortable and nested headings. Preserve clear hover/focus and other themes. This is
   the authorized additional existing-table visual change, not a table redesign.
8. Switch metric without full-page reload: use the existing page/render/query ownership
   and a bounded fetch/replace of leaderboard content. Keep scroll position, URL and
   Back/Forward coherent; reinitialize affected dropdown/sort/disclosure handlers once,
   retain rail state, avoid duplicate global listeners, and prevent stale response races.
   Retain usable content/recovery after fetch failure. No provider requests, all-boss
   preloading, new services/tables/routes/dependencies or general navigation framework.

Existing route URLs and direct reload/fallback remain valid. A fallback navigation, if
unavoidable after a request failure, must return to the leaderboard rather than page top.
The user preferred no reload; do not settle for only an anchor jump without a concrete
blocker. Correct directly affected helpers if needed but do not redesign shared controls.

Evidence: focused existing Node interaction tests for actual close/reopen/option labels,
metric fetch/swap/reinitialization/scroll/history/race/failure behavior; focused scoped
markup/CSS assertions for color, signs, no arrows/status labels and stack order. Update
only HTTP/render assertions invalidated by approved presentation changes and run their
focused filter if Razor/route rendering changes; reuse passing backend computation and
cache-boundary evidence unless those owners change. Web Release and scoped format/diff
checks remain. Source/CSS assertions do not establish actual browser/visual acceptance;
no agent browser/CUA inspection. User owns screenshot/layout/browser acceptance.

User screenshots are direct rejection evidence:
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-7140e625-4ba1-40cc-8c89-d74d8224a10e.png`
and
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-bb05b045-d796-4625-a1f9-4fbb3ccb6866.png`.
No automatic runtime restart; user now uses the existing preview launcher updated to
build and run Release in one invocation, with existing private DB settings preserved.


Additional user corrections to the active section 19 visual pass:
9. Boss Teams nested/expanded and standalone Players Gained/Start/End headings must
   translate to Opnået/Start/Slut in Danish; cover all boss occurrences and scoped
   rendered localization assertions.
10. Nested EHB account table only: remove redundant EHB prefix/suffix from these
    headings, using localized Gained/Start/End. Do NOT change Drop EHB table headings
    or unrelated standalone default Players metric labels. Keep sorting behavior and
    accessible sort names consistent. Include in the same worker/reviewer correction
    delta; no additional pass or fresh review chain.

11. User decision 2026-09-18: the light-mode METRIC selector trigger (prefix,
    selected name and chevron) uses existing ink normally, and existing blue when
    open, hovered or keyboard-focused. This replaces the earlier always-blue request.
    Preserve dark-mode styling; use existing tokens and scoped cascade checks. Include
    in the current correction delta and same-reviewer handoff.


Review stop boundary — user directed 2026-09-18: finish this correction pass and its
focused checks, freeze evidence, then stop. Do not automatically dispatch/wake the
reviewer or route a review request when the implementer finishes. User deferred review
until tomorrow; wait for explicit resumption, not an automatic scheduled task. This
supersedes the current correction pass's automatic review-handoff instruction only.
