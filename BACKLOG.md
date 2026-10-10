# Backlog

Ideas and fixes to consider later. Nothing here is approved or scheduled; an item
becomes work only when the user picks it and it gets a brief. Bugs found in
production are added under "Bug fixes" as they are noticed.

An item is removed from this file once its work is committed and the user has
approved it; Git history keeps the record. Do not mark items "done" here.

Priority: **High** = needed before DKL BINGO OKTOBER 2026 goes Live (draft in about 5 days from 8 October 2026), in the order listed · **Normal** · **Maybe** = decide later.

## Ideas and improvements

| Item | Priority | Notes |
| --- | --- | --- |
| How To: switching playing account | Normal | A short How To section explaining how to switch the playing account from the header (split from "Playing account in the header", user 9 October 2026). |
| Measure the local whole-suite time | Normal | Test speed (merged and approved 10 October 2026) met the CI goal (build-and-test ~7–8 min). The local goal (15–20 min with Docker Desktop at 12 GB) is not measured yet. Run the whole .NET suite once locally and record the time; if it misses the goal, the five long classes left as they are (review 119 F1, `Slice10Pass103ActivityProjectionTests`, `C20ObjectiveIdentityIntegrationTests`, `Slice1IdentityIntegrationTests`, `C11FinalizedRosterIntegrationTests`, `UiReviewScenarioIntegrationTests`) are the next step. |
| Admin pages scale up on large screens (clamp) | Not needed for now | User check on a 1080p monitor (8 October 2026): all pages look fine; Schedule and Identity a bit small but acceptable. Revisit only if that changes. |
| Public page CSS cleanup | Normal | Leftover transitional CSS on the public pages (U10-E2, 8 October 2026). |
| Full R-3 rehearsal harness | Maybe | Not built. The VM-based rehearsal procedure is in Git history (`docs/PRODUCTION_RUNBOOK.md` "R-3 isolated rehearsal procedure" at `e7d143d5`); releases use the lighter local rehearsal (`scripts/rehearsal/README.md`). |
| CI: make JavaScript tests block the release | Normal | The JS job is already sharded (9 shards) but is not a release gate: `continue-on-error: true` and `build-and-test` does not need it. First fix the known JS failures (page-conformance [chromium] and [webkit], board-drawer [webkit], overview [webkit], `admin-design-wom-connection` [webkit] never passing on Linux, `admin-design-destination-header` [webkit] intermittent; latest list in run `38057598431`), then remove `continue-on-error` and add `javascript-tests` to `build-and-test` needs. |
| Browser tests always clean up | Normal | 24 browser test files create their Playwright context outside the `try`, so when the browser is gone (timeout, crash) their `finally` throws before the fixture (dotnet/python helpers) is closed — the same bug that made `admin-design-wom` hang until the CI step limit (fixed there in `fa0b230d`, CI remediation 1, 10 October 2026). The runner now kills the whole process group when a run ends (timeout, failure or pass; review 125 F1), so this only costs each file its own cleanup. |
| Review the test suite | Normal | An outside reviewer's quick look (8 October 2026) found tests that aren't needed. Audit first, change later — see "Test suite review" below. |
| Maintenance page during deploys | Normal | Caddy shows a styled "being updated, back in a few minutes" page (with `Retry-After`) whenever the app is not answering — during a deploy and if a failed deploy leaves the site down. `handle_errors` for 502/503 + a static `maintenance.html` in `deploy/Caddyfile`; Caddy needs one manual reload on the server. Today visitors see Caddy's bare 502. |
| Playing accounts owned by one website account | Normal — foundation for profiles/leaderboards | Today several website accounts may link the same OSRS name (deliberate: borrowed alts; `docs/ARCHITECTURE.md` §4 Signup). Proposal: each name belongs to at most one website account. Investigation: `review-notes/117-investigation-playing-account-ownership.md` (past events unaffected; rule + index small–medium; current events medium). User (9 October 2026): alts as a text + account answer rather than an owned link, if possible; admins resolve ownership conflicts; no duplicates expected in production (verify with the read-only query in 117 first). Long-term purpose: a lookup of a person's accounts and past events (not in scope). Reverses an existing product rule, so requirements change first. |
| Player profiles and all-time leaderboards | Normal | Public player profiles (past events, total EHB/dEHB, other stats, per-OSRS-account breakdown) and all-time leaderboards across events (wins, average placement, EHB, dEHB, boss kills, more to be proposed). Ranks the person (website account); imported events count for team results only, labelled. Needs the one-account-per-OSRS-name binding first (user decisions E1–E4, P1–P3, 10 October 2026). Design: Codex report 155 (planner review-notes); user decisions S1–S8: team result to members at event end, normalized finish (min 5 events), rank all recorded EHB/KC with a coverage label, first link owns the name (admins adjudicate), admin-confirmed links for imported entries, profile name = preferred account, core first (profiles + wins/placement/EHB/dEHB), then records/badges, then teammates/head-to-head; all-time + yearly. Phases: binding (migration) → per-event stats + import links (migration) → public pages → records → optional richer WOM data. |
| Squash old migrations into a baseline | Maybe | Once production and every environment are past them: replace the old migrations with one baseline from the current model and mark it applied (history row) — removes most of the ~320,000 generated lines. Old-schema upgrade/rehearsal tests depend on the old chain and would have to go or change; needs its own rehearsal. |
| Remove Bootstrap/jQuery if unused | Normal | `wwwroot/lib/` (~83,000 lines, template leftovers incl. rtl/slim variants). Check which pages still load them; remove what nothing uses. |
| Audit: filter by affected account | Normal | After the audit overhaul: filter the Audit page by affected account ("everything that happened to this player"). Split from the overhaul (user, 10 October 2026). |
| Audit: show safe values of "code" settings | Normal | Audit hides every field whose name contains "code"/"credential", which also hides non-secret settings (`event.evidence_code_mode`, `evidence_code.created`, `event.signup_code_changed`, competition credential keys). Let these named keys show their non-secret fields; security-sensitive, so its own review (audit inventory A5, user 10 October 2026). |
| WOM: recognise renamed accounts | Maybe | Optionally recognise a renamed OSRS account by its Wise Old Man provider ID instead of treating it as a new name. Never built; moved from the old page matrix (documentation restructure, 10 October 2026). |
| WOM creation status shimmer | Maybe | An uncommitted WOM reference variant: once "Creating on Wise Old Man" appears it stays visible for a minimum time with a subtle shimmer, so a creation confirmed at once doesn't flash; presentation only. The variant (`noteOp`/`opShown`) is saved outside Git in the planner's `review-notes/145-wom-creation-shimmer-variant.dc.html`. (User, 10 October 2026.) |

## Bug fixes

| Bug | Where | Noticed | Notes |
| --- | --- | --- | --- |
| "!" icon off-centre, reads as "l" | Admin Final review and WOM pages | 9 October 2026 (user) | Normal priority. The exclamation-mark icon is not horizontally centred and looks like a lowercase "l". Fix the icon so the mark and dot are centred and read as "!". |

## Test suite review — what to look for

Starting point for the audit (read-only first: measure, classify, propose; no test or code changes until the user approves the plan). Goal is shorter feedback and tests that each earn their cost — not a smaller number.

**Measure before judging.** Per project and per class: cases, wall-clock time (TRX), slowest tests, how many PostgreSQL containers start and how much time goes to container start and migrations. Today (8 October 2026): ~2,250 .NET tests, ~30–45 min locally; 72 of 165 integration test files start their own PostgreSQL container; JS job 54 browser scripts × Chromium/WebKit on one CI runner (~25–40 min); page conformance 85 cases × 2 engines.

**Patterns to find:**
- *Self-confirming tests* — the expected value is computed by the code under test (e.g. `EventLifecycleFoundationTests.TransitionGraphMatchesTheApprovedContract` building its expectations from `EventStatePolicy.CanTransition`). Replace with an explicit table or delete.
- *Echo tests* — set a value, read it back, no rule involved. Keep only where they guard a real invariant.
- *Source-string tests* — `File.ReadAllText` + `Assert.Contains` on Razor/CSS/JS (15 BrowserTests files). Sort each into: a real static rule worth a dedicated check (e.g. resx rules, handler classification), runtime behaviour that belongs in an HTTP/browser test, visual detail that belongs to page conformance or manual review, or implementation trivia to delete. Don't keep old UI structure alive just because a string test encodes it.
- *Shotgun tests* — one method asserting dozens of unrelated things; keep the behavioural core, drop the trivia.
- *Same fact proven at several layers* — prove each invariant at the cheapest layer that can (Domain/Application for rules and calculations; PostgreSQL integration for persistence, transactions, constraints, concurrency; HTTP for routing, authorization, antiforgery, binding; conformance/manual for layout).
- *Retired workflows* — exhaustive success paths for things users can no longer do (old admin pages and modals, banners, retired draft or evidence actions, Live withdrawal/replacement, Emergency Captain, CSV paths) shrink to "rejected / unreachable, nothing changes, history still readable".
- *Slice/pass-named classes* — judge by today's product contract, not the ticket they were written for; merge duplicates.
- *JS browser scripts* — run in both engines only where engine behaviour matters (WebKit-specific layout, focus, dates); fix or remove flaky waits (the Audit "Clear all" WebKit failure, 8 October 2026).

**Infrastructure (likely the biggest win):** one shared PostgreSQL container per collection with a database per class (template clone or reset) instead of a container per class; keep isolated containers only for migration/history tests. Check whether parallel container starts overload a laptop. Shard CI by measured duration, not case count (`.github/scripts/partition-integration-tests.sh`), and shard the JS job.

**Tiers:** a fast inner loop (Domain, Application, static checks, changed area) runnable in minutes; a standard PR set (plus integration and HTTP/security); a slow release set (migration rehearsals, historical compatibility, heavy concurrency); the full suite before deploy.

**Never weaken:** authentication/authorization and privilege changes, evidence authority, ranking/tiebreaks, Luck calculations, event lifecycle, transaction rollback and audit atomicity, concurrency, PostgreSQL constraints, migrations on real data, WOM synchronization boundaries, published/historical snapshots, test #14 (`Slice3DestructiveLifecycleIntegrationTests.TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect`). For every proposed removal, state: what it protects, whether that is still product behaviour, whether it can catch a realistic regression, where else it is covered, and what confidence is lost.
