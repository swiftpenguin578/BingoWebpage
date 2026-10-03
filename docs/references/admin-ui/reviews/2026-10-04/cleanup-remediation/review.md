# Independent review: ticket and documentation cleanup (H1–H7)

Reviewer: Claude (planner), with two read-only reviewer agents, 4 October 2026.
Range: `576c661..f6b5bd9` on `codex/participants-functionality` (activation `e9e65d6`, items `6c7603f`…`b11b6a1`, handoff `f6b5bd9`). HEAD `f6b5bd9e75957892f7323ae55d29c380f1181266`, clean tree at start and end. Nothing was built, run or changed.
Detailed reports: [15a-h1-h3-review.md](15a-h1-h3-review.md), [15b-h4-h7-review.md](15b-h4-h7-review.md). The planner re-read the critical findings in the repository: H1-1, H3-1, H3-2, H4-1, H4-2, H5-1, H5-2 and H5-3.
Requirements: `13-codex-handoff-doc-ticket-cleanup.md`, `08-decisions.md`.

## Verdict

**Four of seven items need a remediation round: H1, H3, H4 and H5.** H2 has small misses. H6 and H7 pass. No production code changed outside H6, and nothing was pushed.

| Item | Commit | Result |
| --- | --- | --- |
| H1 durable evidence | 6c7603f | **FAIL**. The copies are faithful (61 files byte-identical to the backup, no secrets), but pointers and notes are missing. |
| H2 status and registers | 4c59b19 | PASS with notes |
| H3 behaviour documents | 0fadc2f | **FAIL**. Ranking rule missing from DATA_MODEL; an unsupported acceptance claim was added. |
| H4 release gate | de812b4 | **FAIL**. Wrong BR-11 query; the R-3 procedure is unsafe as written. |
| H5 ticket ownership | f5bf0c2 | **FAIL**. AU20 not rewritten; Dashboard bugs and EI-3 misdescribed. |
| H6 test names and N-1/N-2 | eb129e5 | PASS (one Danish string missing) |
| H7 CLAUDE.md | b11b6a1 | PASS. The paragraph is verbatim; AGENTS.md is untouched. |

## Required fixes

### Highest priority
- **H5-1 (High): AU20 was never rewritten.** `DELIVERY_PLAN.md:400` only says "Rewrite the ticket around…", and the AU20 scope row (`:925`) is unchanged. None of the WA-2 rules exist in the repository. Write them into AU20 exactly as in `08-decisions.md` "WA-2 (decided, extends AU20)", items 1–7 and the known limit:
  - configured window, exact, at every stage including final review; actual times keep driving cutoff, eligibility and the review window
  - early end and Resume never blocked on WOM
  - early end: WOM end = click time rounded **up** to the next whole minute, stored as the configured end; actual end keeps the precise click
  - Resume: WOM end = the admin's replacement end, with no click-time rounding
  - spaced backoff, never a fast loop; retries may continue until results are published
  - no fetch after the actual end while the WOM end is unmatched; the fallback marks the event "WOM end could not be updated", skips the final fetch, and uses the last pre-end fetch as official
  - external-ID-only competitions always take the fallback after an early end
  - Resume is the third tolerance site
  - HTTP 400 `COMPETITION_START_DATE_AFTER_END_DATE` is non-transient
  - the snapshot-precision limit is accepted

  Also record in AU20 that three documents currently say an early end keeps the scheduled end (`DATA_MODEL.md:340`, `TECHNICAL_ARCHITECTURE.md:616`, `PRODUCT_REQUIREMENTS.md:612`). Update them to the decided rule, marked "pending AU20" so they don't claim it's implemented.
- **H4-2 (Medium, safety): the R-3 rehearsal procedure is unsafe as written.** `bingo-deploy` refuses any configuration outside `/etc/bingo` (`deploy/host/bingo-deploy:34`) and stops the live web service. A web start also starts `EventLifecycleWorker`, `EventCompetitionSynchronizationWorker` and `EventCompetitionManagementWorker` (`src/Bingo.Web/Program.cs:161-163`). Run against a restored production copy, those would use production WOM credentials and could change real WOM competitions or advance real event lifecycles. **Propose first, implement after approval:** an isolated rehearsal procedure on a separate host or compose project that never touches `/etc/bingo` or the live service, with outbound provider and network calls blocked (for example, no egress). It must exercise the same steps: backup restore, exact migration history, `--migrate`, `--convert-luck-checkpoints`, `--production-preflight`, web start and health. If it needs any code or configuration switch (for example, disabling workers), say so in the proposal; that needs user approval.

### Medium
- **H5-2: Dashboard bugs misdescribed.** `DELIVERY_PLAN.md:382` (DB-1) treats "D7" as the equal-start cohort and Provisional rules; those are DB-7 documentation items. `:410` (RL-1) labels DB-2/DB-3 as "metadata/next-date", which are DB-4/DB-5. Per D7, the **Dashboard integration ticket** must own these two bug fixes:
  - **DB-2:** a pre-Live finalized-roster removal (or any Confirmed participant not on a team) makes the EHB fingerprint differ, so the event shows "Unavailable" (`02-dashboard.md` DB-2).
  - **DB-3:** with any imported event counted, the approved-submissions headline is always "unknown" (`02-dashboard.md` DB-3).

  Remove them from RL-1.
- **H5-3: EI-3's fix direction is reversed.** RL-1 says the Identity uncertain response "must read the database". The fix is to render the uncertain page **without** a database read, as Create does (`04-events-identity.md` EI-3).
- **H4-1: the BR-11 query checks the wrong condition.** It uses `events.team_rosters_published` (`DELIVERY_PLAN.md:2615`, `docs/PRODUCTION_RUNBOOK.md:270-276`). The public board and final review depend on an active, unsuperseded publication cycle with roster rows on a Finalized draft (`src/Bingo.Infrastructure/Teams/DraftPublicationQueries.cs:13-17`). Rewrite the query to match that, and note that the 3 October "0" was from the user's earlier check, which must be re-run with the corrected query.
- **H1-1: the Luck evidence pointer was not restored.** `DELIVERY_PLAN.md:38` and `:252` still say evidence and identity "are in CURRENT_STATUS". Point them to `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/luck/` and restore the final identity they used to give.
- **H1-2: the DB-7 identity note is missing.** State that the Dashboard PASS applies to the earlier hashes `846aed…`/`871051…`, that the later change is AU04's `GetEventParticipationAsync`, and that AU04's review covers it (`FUNCTIONALITY_CHANGES.md:674-683`, `DELIVERY_PLAN.md:1228-1232`).
- **H3-1: the retained ranking order is missing from `DATA_MODEL.md` 13.1** (`:1846-1858`). Add it next to the AU12 order, matching `PRODUCT_REQUIREMENTS.md:719-731` and `PublicProgressCalculator.cs:201-206`.
- **H3-2: the acceptance record needs to be precise.** `UI_PAGE_MATRIX.md:131-133` says the tile Luck section's visual acceptance "is recorded with the 2 October Luck approval", but that approval record (`:163-166`) names only the Stats demo. **User confirmation, 4 October 2026:** the user did visually inspect the tile Luck section on 2 October and it looked correct. That is a visual check only; functional correctness rests on the automated tests. Record exactly that, attributed to the user on 4 October, instead of implying it was part of the Stats-demo approval text.

### Low
- **H4-3:** add the `20261003184632_AllowCancelledDraftRestart` backfill count and Down check to the gate and the runbook.
- **H4-4:** the BR-7 query uses an undefined global `:current_cycle_start_utc`. A cycle is per event, so rewrite it per event (the current review cycle of events in Final Review), or count all retained corrections, since D1 says they're ignored anyway.
- **H4-5:** runbook step 1 must only **verify** the one-time banner cleanup, not tell the operator to perform the deletion again (`docs/PRODUCTION_RUNBOOK.md:227-243`).
- **H1-3:** the two `/private/tmp` Dashboard citations in `MANUAL_TEST_CHECKLIST.md:22-27` still remain.
- **H1-4, H1-5, H1-6:** copy the two dropped small logs that are the only record of a cited result (`web-build-after-test-fixes.log`, `ehb-correction-proof-parsed.log`). Correct the claim that `dashboard-remediation-r1-r5-fixturefixed.trx` (9/9) is retained, or retain it. Restore the two Luck files lost to name collisions by using subfolders. All of these are available in `/Users/christopher/Documents/BingoWebpage/review-notes-evidence-backup/` if `/private/tmp` is gone.
- **H1-7:** the sanitized demo note dropped the route and the values the user inspected before approving Luck. Keep the route and aggregate values (no personal data).
- **H2-1, H2-2, H2-3:**
  - `DELIVERY_PLAN.md:981` ("uncommitted changes")
  - `FUNCTIONALITY_CHANGES.md:668-672`, `:678`
  - the behaviour-table rows at `FUNCTIONALITY_CHANGES.md:87-90` (AU16/20/21/22 per D6)
  - the stale "Next permitted action" at `DELIVERY_PLAN.md:267-269`
  - the AU13 row at `FUNCTIONALITY_CHANGES.md:306` (done by F8)
- **H3-3:** qualify the cohort wording (`docs/references/admin-ui/README.md:538-541`, `FUNCTIONALITY_CHANGES.md:744-745`) to match the test: unique 1, first-time 1+1 for equal-start events.
- **H3-4:** `DATA_MODEL.md:885` ("Replacements require explicit admin action") and the G4 `requires_fresh_order` restart rule (`:995-1004`, `:1086`).
- **H3-5:** record the dead `BingoEvent.ConfigureSchedule` capacity rule as an observation in the Signup/Schedule integration ticket.
- **H3-6:** restore the "C33 states awaiting manual acceptance" note on the Finalize row (`UI_PAGE_MATRIX.md:76`).
- **H5-4:** remove the unapproved X-6 approach ("audit stays inside the transaction"). State the problem only.
- **H5-5:** give AU17's approved full-pool correction its own ticket ID and slot (D5).
- **H5-6:** place RL-1, P-3, DB-6 and the Luck proof gap in the proposed order. Give the Luck proof gap a real owner and a new ID (not "LK-1"). Include RC01–RC11 in the proposed order. BR-10 must mention RC07.
- **H5-7:** tickets should cite the original review section by finding ID and report file name. The review notes are private, so record the finding text needed to act inside the ticket itself rather than only linking.
- **H5-8 (user decision, 4 October): AU23 scope.** Update AU23 (`DELIVERY_PLAN.md:353`, `:403`, `:928`; `FUNCTIONALITY_CHANGES.md:88`, `:264`, `:316`) to `08-decisions.md` "AU23 roll count (decided 4 October)". Rate text, including any "N x" roll count, is ordinary input when adding and editing; AU23 removes the "Changes to reward rolls require an operator" refusal (`src/Bingo.Web/Pages/Admin/Catalogue/Index.cshtml.cs:139-143`, `:209-213`). Roll group, probability scope, conditional/parent probability and assumed participants stay SuperAdmin-only. Also record: new drops use the `default` roll group. The Catalogue reference (`docs/references/admin-ui/Catalogue.dc.html:378`, `:801`) still says changing rolls needs an operator and refuses it; the Catalogue binding ticket (WA-5) must drop that text and refusal. Its read-only roll display follows `08-decisions.md`. Remove "roll-count input remains an open product choice". Ticket text only; no code change in this round.
- **H5-9 (user decisions, 4 October): drop-rate mechanics.** *Updated 4 October: Option 1 decided. Record "always enter the final chance" and retire "Only after" from the editor/panel/new input (keep columns); do **not** create the EHB parent-chance fix ticket. The team-size move and roll-group parts stand.* Record `08-decisions.md` "Drop-rate mechanics (4 October)" in the owning documents (`DATA_MODEL.md` catalogue/EHB rules, `PRODUCT_REQUIREMENTS.md` Catalogue) and create a fix ticket: EHB (catalogue drop EHB and board estimates) applies the parent chance for conditional drops, matching Luck; production has 0 conditional drops (user query, 4 October); no change to approved/published snapshots. Add the decided panel layout and the activity team-size field (Settings and Add activity) to the Catalogue binding ticket (WA-5), per `08-decisions.md` "Catalogue layout (decided 4 October)", noting they intentionally differ from `Catalogue.dc.html`. Also create a ticket to move the agreed team size from drops to the boss/activity (`08-decisions.md`, decided 4 October), with a pre-migration re-check for set or conflicting per-drop values (production had 0 on 4 October). Ticket and document text only; no code change in this round.
- **H6-1:** add the Danish string for the N-1 message (`Draft.cshtml.cs:165`).

## Decisions for the user
- **H3-2 (answered 4 October):** visually inspected and looked correct; not a functional acceptance. Written into the fix above.
- **AU23 roll count (answered 4 October):** rate text including the roll count is ordinary input; roll group and the other advanced fields stay SuperAdmin-only. Written in as H5-8.
- **D8 order:** a proposal only. Claude turns it into the batching, parallel and model plan after this round passes.

## Remediation instructions for Codex
- Same implementer and routing as before. Fix the items under "Required fixes" only. H4-2 is propose-first: send the isolated rehearsal proposal to `/root` before writing it into the runbook, and continue with the other items meanwhile.
- Separate commits per item (H1, H2, H3, H4, H5 including H5-8 and H5-9, H6), each naming the finding IDs. No amendments. No production code changes except the H6-1 resource string.
- Proof: list each finding ID with the exact file:line changed. Grep checks for `/private/tmp` in active documents, for "Rewrite the ticket around", and for "must read the database".
- Stop after the round. Claude rechecks the fix commits only.
