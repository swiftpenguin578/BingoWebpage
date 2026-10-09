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
| **Draft page uses large screens better** | **High** | Readable at 1080p (user check, 8 October 2026) but much of the screen is empty. Draft only: larger player chips and team cards, or a scale-up between today's size and a max width. |
| **Credited EHB counts only completed tiles** | **High** | Admins' rule (8 October 2026): only EHB from **completed** tiles counts. Today `PublicProgressCalculator` sums every approved contribution, so incomplete tiles add partial EHB (`allocatedContributions.Sum`). Change the ranking EHB to completed tiles only. Decide: for the new placement rule only (`CreditedEhbThenScoreTime`; October was switched to it) — never re-rank past events or official snapshots; whether player/team EHB displays follow the same rule; tests at the PostgreSQL boundary. Must be live before October's results, ideally before it goes Live. |
| **Tile points on the public board** | **High** | Show each tile's EHB to players as **"Points"**, rounded to the nearest whole number, on the public team board tiles in place of the position label ("R1 · C1" etc.), e.g. "Points: 12" (Danish too). Make that text larger so it reads easily. Should use the same EHB the ranking uses (see the row above). |
| **Playing account in the header** | **High** | Move the account switcher from its current place into the site header, shown only while the event is Live. It always names the account you are playing on; players with one account see it without a switch option (today they can see "Not active"). |
| **Public pages: fixes from the last bingo's feedback** | **High** | Players found the text in the left sidebar of a team's board too small (`TeamBoard.cshtml` `.public-team-sidebar`; fonts in `site.public-ui.css`). Proposal (8 October 2026): scale the whole sidebar ~1.15–1.25× so the hierarchy stays, smallest text scaled most, nothing under ~13.5 px — e.g. heading 19→23 px, account name 16→19, subheadings/metric labels 14→17, progress label/value 14→16, supporting 14→16, metric values 12.5→14.5, contributor rank/name/value 11→13.5, account label/switch/focus state 11–12→13.5–14.5, buttons 14→15. Check wrapping at the sidebar's width (`minmax(12rem, 1fr)`) and on phones; user visual approval. **Layout (user, 8 October 2026, 1080p screenshot):** ~350 px of the content width sits empty right of the board while the sidebar is ~260 px. Widen the sidebar to ~340–360 px and let the board fill the remaining width, clamped between today's size (minimum, fits a 13" MacBook) and the viewport height (whole board visible without scrolling; ~20% larger tiles at 1080p); tile text (names, numbers, the new "Points") scales with the tile. |
| How To: switching playing account | Normal | A short How To section explaining how to switch the playing account from the header (split from "Playing account in the header", user 9 October 2026). |
| Captain submissions default to themselves | Normal | Captains and co-captains can submit evidence for any team member (production), but the credited player is not preselected, so they must pick a player every time. Make the captain/co-captain's own playing account the default credited player in the submit form; they can still change it. (User, 9 October 2026.) |
| Audit overhaul: readable entries | Normal | Audit entries are still hard to read. Example: a payment update stores a participant id but shows the id instead of the player whose payment changed. Go through every audit entry type and show names and plain descriptions (who, what, before → after) instead of raw ids/keys; the stored audit data stays unchanged. (User, 9 October 2026.) |
| Admin pages scale up on large screens (clamp) | Not needed for now | User check on a 1080p monitor (8 October 2026): all pages look fine; Schedule and Identity a bit small but acceptable. Revisit only if that changes. |
| Public page CSS cleanup | Normal | Leftover transitional CSS on the public pages (U10-E2, 8 October 2026). |
| Board reference preview | Maybe | Keep the reference modal but show the tile area as plain background with "Not supported yet" (U7-Q3, low priority). |
| Full R-3 rehearsal harness | Maybe | The VM-based rehearsal in `docs/PRODUCTION_RUNBOOK.md`; this release used the lighter local rehearsal instead. |
| CI: make JavaScript tests block the release | Normal | `build-and-test` (and so the production image) ignores the JS job today. Once the JS tests are reliable, add `javascript-tests` to its `needs`; also shard the JS job (54 browser scripts × 2 engines on one runner, ~25–40 min). |
| Review the test suite | Normal | An outside reviewer's quick look (8 October 2026) found tests that aren't needed. Audit first, change later — see "Test suite review" below. |
| Maintenance page during deploys | Normal | Caddy shows a styled "being updated, back in a few minutes" page (with `Retry-After`) whenever the app is not answering — during a deploy and if a failed deploy leaves the site down. `handle_errors` for 502/503 + a static `maintenance.html` in `deploy/Caddyfile`; Caddy needs one manual reload on the server. Today visitors see Caddy's bare 502. |
| Documentation cleanup | Normal | Too much documentation, much of it stale, and most of it in the repo root. Git keeps history, so deleting is reversible. **Delete:** tombstones `ADMIN_UI_CONTRACT.md`, `FUNCTIONAL_WORKFLOWS.md`, `UI_OVERHAUL_ROADMAP.md` (fix links first); `MANUAL_TEST_CHECKLIST.md` (self-declared historical ledger); `TICKETS.md` (historical ticket ledger); `TICKETS_2026_09_FOLLOWUP.md` (unreferenced); `docs/archive/`. **Cut down:** `DELIVERY_PLAN.md` (6,000 lines, mostly finished-pass history), `docs/references/` screenshots/evidence (68 MB; keep the `*.dc.html` references), `DEVELOPMENT_SETUP.md` (July; merge into README or drop). **Keep and move to `docs/`:** requirements, contracts, data model, architecture, UI system, page matrix, runbook, topology — trimmed of history. Root keeps README, AGENTS, CLAUDE, CURRENT_STATUS, BACKLOG. Update the `AGENTS.md` authority table and links. **Rule to add to `AGENTS.md`:** documents describe only the current state; history lives in Git, PRs and review notes; replace text instead of appending exceptions; delete finished plans/tickets once done. |
| README as a project introduction | Normal | Rewrite `README.md` to explain what DK Legacy Bingo is (what the site does for players and admins, how an event runs, main features, tech stack in one line) with a short "Getting started" link. Move the detailed environment, setup, test and command instructions into a developer document under `docs/` (together with the documentation cleanup). |
| Remove Claude co-author lines from history | Maybe | 39 commits from 7 October 2026 (Claude helpers, before attribution was turned off) carry `Co-Authored-By: Claude Opus 5.5`, so GitHub lists Claude as a contributor. Removing it means rewriting `main`'s history and force-pushing: every later SHA changes, so do it only at a quiet point, with explicit approval, and rebuild/redeploy references afterwards. |
| Mark generated files for GitHub | Normal | `.gitattributes`: `linguist-generated` for EF migrations (`*.Designer.cs`, model snapshot) and vendored `wwwroot/lib/`, so GitHub's language stats and diffs skip them. Cheap. |
| Squash old migrations into a baseline | Maybe | Once production and every environment are past them: replace the old migrations with one baseline from the current model and mark it applied (history row) — removes most of the ~320,000 generated lines. Old-schema upgrade/rehearsal tests depend on the old chain and would have to go or change; needs its own rehearsal. |
| Remove Bootstrap/jQuery if unused | Normal | `wwwroot/lib/` (~83,000 lines, template leftovers incl. rtl/slim variants). Check which pages still load them; remove what nothing uses. |

## Bug fixes

| Bug | Where | Noticed | Notes |
| --- | --- | --- | --- |
| "!" icon off-centre, reads as "l" | Admin Final review and WOM pages | 9 October 2026 (user) | Normal priority. The exclamation-mark icon is not horizontally centred and looks like a lowercase "l". Fix the icon so the mark and dot are centred and read as "!". |
| Team removal audit can still grow with team size | Teams/Draft — remove a team | 8 October 2026 (review 109 F1) | Ended membership ids are listed; ~4,000 chars only at ~90 members or max-length names. Cap or hash above a threshold. |
| Audit label missing a name | Audit page — finalized roster removal | 8 October 2026 (review 109 F2) | Removing someone not on the published roster shows "Membership ·" without a name; fall back to the character name. |

## Test suite review — what to look for

Starting point for the audit (read-only first: measure, classify, propose; no test or code changes until the user approves the plan). Goal is shorter feedback and tests that each earn their cost — not a smaller number.

**Measure before judging.** Per project and per class: cases, wall-clock time (TRX), slowest tests, how many PostgreSQL containers start and how much time goes to container start and migrations. Today (8 October 2026): ~2,250 .NET tests, ~30–45 min locally; 72 of 165 integration test files start their own PostgreSQL container; JS job 54 browser scripts × Chromium/WebKit on one CI runner (~25–40 min); page conformance 85 cases × 2 engines.

**Patterns to find:**
- *Self-confirming tests* — the expected value is computed by the code under test (e.g. `EventLifecycleFoundationTests.TransitionGraphMatchesTheApprovedContract` building its expectations from `EventStatePolicy.CanTransition`). Replace with an explicit table or delete.
- *Echo tests* — set a value, read it back, no rule involved. Keep only where they guard a real invariant.
- *Source-string tests* — `File.ReadAllText` + `Assert.Contains` on Razor/CSS/JS (15 BrowserTests files). Sort each into: a real static rule worth a dedicated check (e.g. frozen tokens/components CSS byte-identical, resx rules, handler classification), runtime behaviour that belongs in an HTTP/browser test, visual detail that belongs to page conformance or manual review, or implementation trivia to delete. Don't keep old UI structure alive just because a string test encodes it.
- *Shotgun tests* — one method asserting dozens of unrelated things; keep the behavioural core, drop the trivia.
- *Same fact proven at several layers* — prove each invariant at the cheapest layer that can (Domain/Application for rules and calculations; PostgreSQL integration for persistence, transactions, constraints, concurrency; HTTP for routing, authorization, antiforgery, binding; conformance/manual for layout).
- *Retired workflows* — exhaustive success paths for things users can no longer do (old admin pages and modals, banners, retired draft or evidence actions, Live withdrawal/replacement, Emergency Captain, CSV paths) shrink to "rejected / unreachable, nothing changes, history still readable".
- *Slice/pass-named classes* — judge by today's product contract, not the ticket they were written for; merge duplicates.
- *JS browser scripts* — run in both engines only where engine behaviour matters (WebKit-specific layout, focus, dates); fix or remove flaky waits (the Audit "Clear all" WebKit failure, 8 October 2026).

**Infrastructure (likely the biggest win):** one shared PostgreSQL container per collection with a database per class (template clone or reset) instead of a container per class; keep isolated containers only for migration/history tests. Check whether parallel container starts overload a laptop. Shard CI by measured duration, not case count (`.github/scripts/partition-integration-tests.sh`), and shard the JS job.

**Tiers:** a fast inner loop (Domain, Application, static checks, changed area) runnable in minutes; a standard PR set (plus integration and HTTP/security); a slow release set (migration rehearsals, historical compatibility, heavy concurrency); the full suite before deploy.

**Never weaken:** authentication/authorization and privilege changes, evidence authority, ranking/tiebreaks, Luck calculations, event lifecycle, transaction rollback and audit atomicity, concurrency, PostgreSQL constraints, migrations on real data, WOM synchronization boundaries, published/historical snapshots, test #14 (`Slice3DestructiveLifecycleIntegrationTests.TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect`). For every proposed removal, state: what it protects, whether that is still product behaviour, whether it can catch a realistic regression, where else it is covered, and what confidence is lost.
