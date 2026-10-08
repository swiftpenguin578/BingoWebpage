## Luck percentile and KC comparison — queued plan, 2026-10-01

**Status:** product decisions agreed; planning/documentation only. Implementation,
worker dispatch, migrations, runtime/provider actions and packaging are NOT authorized.
This is separate from the ongoing Admin UI/backend work. Complete the normal bounded
readiness check when implementation is authorized; this plan is not a readiness or
independent implementation review pass.

**Source baseline:** inspected `codex/participants-functionality` in
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, including
its existing uncommitted Participants/Dashboard work. The 573d checkout is stale.
Reconfirm the then-current checkout and changed dependencies before execution;
preserve unrelated work and do not assume the implementation must use this branch.

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

### Remaining risks and stop boundary

No outstanding product decisions from this discussion. Readiness must validate the
legacy data sufficient for each converted scope, the archive/provider window boundary,
and existing catalogue independence assumptions. Raise a concrete unsupported mechanic
or required scope expansion rather than silently broadening the probability engine.
Player/team roster histories and rate changes must not accidentally change observation
ownership. Percentiles remain a model of approved eligible drops, not all unseen gameplay.

This section records future approved behavior. Older signed-score, reversal-invalidation,
read-time-rescore and Live-only Luck descriptions document the current implementation;
they must be reconciled in their owning authorities under L0 before code changes. Until
then, this queued plan is the explicit target, not a claim that production already follows it.
**Next permitted action now: stop after saving/checking the plan. Await implementation authorization.**
