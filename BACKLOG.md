# Backlog

Ideas and fixes to consider later. Nothing here is approved or scheduled; an item
becomes work only when the user picks it and it gets a brief. Bugs found in
production are added under "Bug fixes" as they are noticed.

Priority: **High** = most likely will be done · **Normal** · **Maybe** = decide later.

## Ideas and improvements

| Item | Priority | Notes |
| --- | --- | --- |
| Draft page uses large screens better | High | Readable at 1080p (user check, 8 October 2026) but much of the screen is empty. Draft only: larger player chips and team cards, or a scale-up between today's size and a max width. |
| Admin pages scale up on large screens (clamp) | Not needed for now | User check on a 1080p monitor (8 October 2026): all pages look fine; Schedule and Identity a bit small but acceptable. Revisit only if that changes. |
| Public pages: fixes from the last bingo's feedback | Normal | Mainly text that is too small. |
| Playing account in the header | Normal | Move the account switcher from its current place into the site header, shown only while the event is Live. It always names the account you are playing on; players with one account see it without a switch option (today they can see "Not active"). Possibly a How To section on switching. |
| Public page CSS cleanup | Normal | Leftover transitional CSS on the public pages (U10-E2, 8 October 2026). |
| Board reference preview | Maybe | Keep the reference modal but show the tile area as plain background with "Not supported yet" (U7-Q3, low priority). |
| Full R-3 rehearsal harness | Maybe | The VM-based rehearsal in `docs/PRODUCTION_RUNBOOK.md`; this release used the lighter local rehearsal instead. |
| CI: make JavaScript tests block the release | Normal | `build-and-test` (and so the production image) ignores the JS job today. Once the JS tests are reliable, add `javascript-tests` to its `needs`; also shard the JS job (54 browser scripts × 2 engines on one runner, ~25–40 min). |
| Review the test suite | Normal | An outside reviewer's quick look (8 October 2026) found tests that aren't needed. Audit first, change later — see "Test suite review" below. |
| Maintenance page during deploys | Normal | Caddy shows a styled "being updated, back in a few minutes" page (with `Retry-After`) whenever the app is not answering — during a deploy and if a failed deploy leaves the site down. `handle_errors` for 502/503 + a static `maintenance.html` in `deploy/Caddyfile`; Caddy needs one manual reload on the server. Today visitors see Caddy's bare 502. |
| Documentation cleanup | Normal | Remove stale information (old pass/slice history, superseded statuses and rules) and tidy the layout: many documents sit in the repo root. Move them into a clear `docs/` structure, keep the authority table in `AGENTS.md` accurate, and fix links. |

## Bug fixes

| Bug | Where | Noticed | Notes |
| --- | --- | --- | --- |
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
