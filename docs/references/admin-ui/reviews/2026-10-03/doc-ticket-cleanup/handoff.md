Planner activation: 3 October 2026. User forwarded the accepted G-batch recheck and the ready H1–H7 cleanup handoff. This is the active bounded cleanup assignment; its source draft label is historical. No later AU/UI implementation is authorized.

# Codex hand-off: ticket and documentation cleanup (step 4)

Status: **ready to send, 3 October 2026.** Execution is authorized when the user sends this brief to Codex, limited to H1–H7. The G1–G6 batch passed Claude's review at `576c661` (`14-g-batch-review.md`). Other AU/RC work remains stopped.

## 1. Authority and baseline

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`.
- Start from the reviewed G-batch head `576c6615c64af4d0959152dbcbf7c327106f624f` (G1–G6 plus remediation, accepted 3 October). Confirm HEAD and a clean tree. If the branch has moved, stop and report.
- Source: Claude's review in `/Users/christopher/Documents/BingoWebpage/review-notes/` (index `00-index.md`), decisions in `08-decisions.md`. Line numbers come from `ee187bb` or `f2ea1cf` and are pointers only. Re-verify each item at the start HEAD; if one is already resolved, record it as such and move on.
- Document owners (AGENTS.md authority table): `CURRENT_STATUS.md` for current work, evidence and blockers; `DELIVERY_PLAN.md` for scope, ticket order and gates; `PRODUCT_REQUIREMENTS.md` / `FUNCTIONAL_CONTRACTS.md` for behaviour; `DATA_MODEL.md` / `TECHNICAL_ARCHITECTURE.md` for invariants; `UI_SYSTEM.md` / `UI_PAGE_MATRIX.md` for UI rules; `docs/PRODUCTION_RUNBOOK.md` for operations. Put each fix in its owning document and make other documents point to it rather than restating it.
- Follow AGENTS.md (working-tree safety, bounded execution, verification and handoff).

## Assignment routing

Same setup as `09-codex-handoff-fix-batch.md` and `12-codex-handoff-br-td-batch.md` unless the user says otherwise: UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59` (`/root`), one direct implementer on `gpt-5.6-luna` / `max`, own local commits per item, reports to `/root`, no self-review. Independent review by Claude.

## 2. Purpose and limits

Make the documents and the ticket queue tell the truth before the remaining AU tickets and the UI integration start: durable evidence, no stale status, approved rules written down where they're missing, and every open review finding owned by a ticket or explicitly recorded as an accepted observation.

**Documentation and tickets only.** No production code change, except the cosmetic test renames and code comments in H6. H7 edits `CLAUDE.md` only. Writing a ticket for a defect does not mean fixing it; the fix happens when that ticket runs.

## 3. Decisions (user, 3 October 2026: all decided, option (a) in each)

- **D1 – BR-7:** document that retained completion-time corrections are ignored by readiness and ranking (production has 0). No code change.
- **D2 – P-6:** keep the contract. Add/Restore return the capacity outcome as structured data, as part of the Participants integration ticket; correct the handoff text.
- **D3 – P-7:** a ticket that makes Restore use stored account data like Admin Add, with no WOM call.
- **D4 – WA-6:** AU15 keeps generic cooldown wording; AU20 adds the structured reason.
- **D5 – BR-9:** split AU17's approved full-event-pool correction into its own approved, queued ticket; the rest of AU17 stays proposed.
- **D6 – WA-7:** AU16, AU21 and AU22 are approved defect fixes (only transport/scope choices stay open); AU20, AU23 and AU24 are shown as approved.
- **D7 – DB-2 / DB-3:** both fixes are required parts of the Dashboard integration ticket.
- **D8 – Order:** remaining AU tickets, then UI integration tickets, then final candidate, R-3 rehearsal and deploy. Codex proposes the order inside each group in its report; the user approves it.

## 4. Items

One commit per item. Keep H1 first: it protects evidence that a reboot would delete.

### H1 — Durable evidence (P-4, LK-3, EI-4, OS-4 pointer, DB-7 tmp part)
- Problem: approval and review evidence exists only under `/private/tmp`, which macOS clears on reboot (folders confirmed present on 3 October):
  - Participants: `/private/tmp/participants-backend-remediation-20260930/`
  - Dashboard: `/private/tmp/dashboard-backend-20261001/`, `/private/tmp/dashboard-remediation-20261001/`
  - Luck: `/private/tmp/luck-redesign-evidence-20261001/`, `…-remediation-evidence-20261001/`, `…-final-remediation-20261001/`, `/private/tmp/luck-sol-recovery-20261001/`, `/private/tmp/luck-manual-demo-20261001/`, plus `luck-redesign-approved-20261001.md` and `luck-redesign-plan-20261001.md`
  - Provenance only: AU04 `review-initial.md`, AU09 `initial-review.md`
- Required outcome:
  - Copy the reports, handoffs, review verdicts, result summaries and `.meta` hash files into `docs/references/admin-ui/reviews/` (for example a dated folder per area), the same way the 2026-10-02 set was done. **Don't copy build output, binaries, packages or large logs.** If a folder is mostly build output, copy only its text evidence and list what was left out. Report the size added.
  - If a folder is already gone, use the backup Claude made on 3 October: `/Users/christopher/Documents/BingoWebpage/review-notes-evidence-backup/` (same folder names; build output such as `bin/`, `obj/`, DLLs, PDBs and caches was left out). Don't recreate evidence.
  - Re-point every document reference to the durable copy: `FUNCTIONALITY_CHANGES.md:617`, `:668`, `:922-924`, `:1127`; `DELIVERY_PLAN.md` "Completion and UI-binding handoff", `:38`, `:252`; `CURRENT_STATUS.md:29-31`, `:42` (restore the Luck evidence pointer and final identity it used to give).
  - DB-7: state that the Dashboard PASS was for an earlier file identity (hashes `846aed…`/`871051…`), that the only later change is AU04's `GetEventParticipationAsync`, and that AU04's review covers it.
- Proof: grep shows no remaining `/private/tmp` evidence citations in the active documents; the copied files are listed with sizes.

### H2 — Stale status and registers (P-5, DB-7, LK-4 status parts, EI-4, WA-8)
- `FUNCTIONALITY_CHANGES.md:589`, `:628-629`: Participants work is committed (since `d932fdf`), not "dirty/uncommitted".
- `FUNCTIONALITY_CHANGES.md:657-661` and the end of "Dashboard reference inspection": mark as historical.
- `DELIVERY_PLAN.md:914` ("uncommitted changes"), `:18-23`, `:263-271` (Luck "future approved behaviour", stale "Next permitted action"); `FUNCTIONAL_CONTRACTS.md:80` and `DELIVERY_PLAN.md:1076` (old Dashboard runs called "current"); `DELIVERY_PLAN.md:322-324` (AU01/AU02-only status line).
- WA-8: `FUNCTIONALITY_CHANGES.md:1228-1231` ("No other WOM functionality change approved"), the WOM summary row (`:85`), and the status tables that stop at AU15 (`FUNCTIONALITY_CHANGES.md:281-305`, `DELIVERY_PLAN.md:345`): add rows for AU16–AU24 with their real status (per D6). `DELIVERY_PLAN.md:1400`: mention the AU16 server correction.
- `CURRENT_STATUS.md`: bring the current-state summary up to date with F1–F9, R-1/R-2/R-3, G1–G6, and what comes next (per D8).

### H3 — Behaviour documents that contradict the approved rules (LK-4, TD-5, OS-4, BR-8, DB-7, BR-7)
- **Luck (LK-4):** mark `DATA_MODEL.md` "Stats Pass 4 … full Luck checkpoint" (`:2322-2370`) as superseded by the v2 section. Fix `FUNCTIONAL_CONTRACTS.md` "Tile KC/Luck sidebar contract" (`:1187`, `:1199`, `:1203`) to the retain-until-fetch rule. Fix `UI_PAGE_MATRIX.md:117-119` (no sign; coral below 50) and `:130` (acceptance status).
- **Draft (TD-5):** `DATA_MODEL.md` 7.2.1, 7.3 and the draft paragraphs: `IncludedInDraft` decides inclusion; states are Setup, Running, Finalized (Paused only historical); the pool shrinks as teams fill; replacements are retired. Align with whatever G3/G4 changed.
- **Capacity/schedule (OS-4):** `FUNCTIONAL_CONTRACTS.md` 4.4: capacity is owned by Signup setup (remove the contradicting happy-path sentence and matrix row, or point them to Signup setup). `UI_PAGE_MATRIX.md:65`: Schedule no longer configures capacity or an opening toggle. Note the dead `BingoEvent.ConfigureSchedule` capacity rule as an observation in the Participants/Signup integration ticket.
- **Ranking (BR-8):** write down the retained ranking rule existing events keep (finish → finish time → lines → tiles → score time → EHB) in `PRODUCT_REQUIREMENTS.md` 8.2 and `DATA_MODEL.md` 13.1, next to the AU12 order, and say AU12 must keep both, split by an explicit event boundary. Fix `UI_PAGE_MATRIX.md:76` (Finalize): no archive or placement corrections; correct status and notes cells.
- **Dashboard (DB-7):** the README "agreed metric definitions" include Live as Provisional (1 October decision). Qualify "unique people equals the sum of first-time people" with the approved equal-start cohort rule.
- **BR-7 (per D1):** `DATA_MODEL.md:1862-1863` and `FUNCTIONAL_CONTRACTS.md:844`.

### H4 — Release readiness checklist (X-2, R-3)
- Problem: production checks and operator steps for this release are scattered, and the PRE-01 pointers (`DATA_MODEL.md:150`, `DELIVERY_PLAN.md:2529`) point at text `CURRENT_STATUS.md` no longer has.
- Required outcome: one release-readiness section, owned by `DELIVERY_PLAN.md` (gate) with the operator steps in `docs/PRODUCTION_RUNBOOK.md`, listing for each check the query or step, the result recorded on 3 October, and the action if it differs:
  - banner cleanup (X-1; the user's manual cleanup, F1)
  - Luck v1 conversion and its failure rule (LK-2, R-1: a failure blocks deploy)
  - current-cycle completion corrections (BR-7: 0)
  - published boards without a roster publication (BR-11: 0)
  - future-effective account switches (`DATA_MODEL.md:138-140`)
  - the R-3 rehearsal: the full `bingo-deploy` sequence on a restored production backup, on the final candidate, just before deploy
- Fix the dead PRE-01 pointers to point at this section.

### H5 — Ticket ownership for open findings (P-2 and the "no owner" findings)
Every finding below gets an owner in `DELIVERY_PLAN.md`: a new ticket, or a named addition to an existing ticket's scope. Keep each ticket short (problem, approved rule, acceptance, link to the review section). Don't implement.
- **UI integration tickets (P-2):** give each page's integration an ID and a place in the order (per D8), including the explicit retirement or redirect of old handlers and routes (`FUNCTIONALITY_CHANGES.md:571-572`). Pages: Participants (P-1; with D2 and the obsolete legacy Restore overloads from the F7 review note), Dashboard (DB-1; with D7, DB-4 metadata and DB-5 next date), Events and Identity (EI-2), Signup setup / Schedule / Overview (OS-1; with OS-3's dead Resume control and OS-5's capacity in the Schedule readback), Board and Review (BR-10; including G1's structured-data binding with queue/filter context), Teams (TD-6; including the items in `docs/references/admin-ui/README.md:1720-1741`), WOM / Catalogue / Accounts / Audit (WA-5).
- **AU ticket scope additions:**
  - AU13: note that the post-finalization lock it removed came from `525d5d1` (BR-2/BR-9). Mark AU13 done if F8 closed it.
  - AU17: per D5. Name the wrong paused-interval instruction only if G2 didn't fully close BR-3.
  - AU20: rewrite to match `08-decisions.md` WA-2 (configured window, exact, at every stage; early end and Resume rules; retries; fallback; Resume as the third site). Add WA-9: linking must also wait for an in-flight or unresolved website Create.
  - AU21: name shared-item **rename** scope explicitly (WA-3), not only image and adoption.
  - AU22: record that WA-1's consume-time re-authorization is done (F4).
  - AU15: per D4.
  - AU16: note that two tests currently assert the violation and must be inverted (WA-4).
  - AU23: the SuperAdmin path must be DB-checked and keep the blanket refusal for ordinary Admins; decide whether roll count via the displayed-rate syntax stays an ordinary input (WA-5).
  - AU24: don't reuse the legacy `TransferOwnershipAsync(actorId, password, destinationUsername)` overload (WA-5).
- **Small defect tickets (or a single "review leftovers" ticket with one line each):** DB-2 and DB-3 (per D7), EI-3 (Identity uncertain response needs the database), BR-4 (server-side reject/reverse confirmation), BR-12 (review and reopen versions fail open when missing), P-7 (per D3), X-6 (WOM-outcome audit outside the transaction with raw exception text), TD-8 (Scramble and RemoveDraftTeam outside Serializable, unless G3a closed the second).
- **Participants page (from `14-g-batch-review.md` G3b-3):** the page counts manual-team members but hides them from its list and waiting positions. Add this to the Participants integration ticket.
- **Proof-gap tickets:** P-3 (F04 Add when full, normal and +1), DB-6 (recap selection, provisional flags, latest additions, pending/rejected fixtures), the F2 review note (Luck mixed supported/unsupported outcomes).
- **Recorded observations (no ticket, one line each in the owning document):** X-5 (page-model mutations rely on the per-request cookie recheck), BR-11 (production count 0).

### H6 — Cosmetic test names and comments, plus two small recheck fixes
- Rename tests whose names describe retired flows while their bodies assert the new behaviour: `HttpDepartureWaitingFill…`, `ConcurrentFillsHaveOnlyOneWinner`, `LiveWithdrawalAndWaitingReplacement…` (TD coverage note), `…UntilAnyReversal` and `…CompletionCorrectionInvalidates…` (`StatsPass4ReviewCorrectionsIntegrationTests.cs:98`, `:145`).
- Fix the comments at `PublicStatsService.Luck.cs:173-174` and `:377`, which describe removed behaviour.
- Two small code fixes from the G-batch recheck (`14-g-batch-review.md` N-1, N-2), allowed here as an exception:
  - N-1: in `Draft.cshtml.cs` `OnPostUpdateTeamAsync`, refuse cleanly (no write) when inclusion changes and no draft row exists, instead of `draft!`. Add a focused test.
  - N-2: the Running-draft refusal for manual-team Remove and Move should say changes are locked, not "additions". Add the Danish string.
- Proof: build succeeds and the renamed tests are found by name; no assertion changes, apart from N-1's new test.

### H7 — Claude planner/agent workflow in CLAUDE.md (user decision, 3 October 2026)
- In `CLAUDE.md`, section "Codex-specific mechanics that do not apply to Claude", replace "Do not spawn sub-agents unless the user asks." with the user's standing request below, and keep the rest of that bullet (no recreated Codex chains; independent review means a fresh session or an explicitly requested reviewer sub-agent).
- Add this paragraph unchanged:

  > **Planner chats and agents (user decision, 3 October 2026):** the chat the user writes in acts as planner: decisions, briefs, recording decisions, and quick checks. Heavy read-only work (batch reviews, cross-ticket analysis, large sweeps) goes to a sub-agent, which writes its report to `review-notes/`. The planner personally verifies the critical findings before a pass/fail verdict. Start a fresh planner chat per phase, using `review-notes/00-index.md` as the handoff.

- Change nothing else in `CLAUDE.md` or `AGENTS.md`. Proof: the diff touches only these lines.

## 5. Out of scope (do not do)

- Any production-code fix for a finding listed in H5, any AU/RC ticket, and any UI integration.
- Changing a decided rule. If a document and `08-decisions.md` disagree, `08-decisions.md` wins; if two approved sources disagree in a way this brief doesn't settle, stop that line and report it.
- Production access, deployment, merge or push.

## 6. Verification and handoff

- Scoped checks only: link and path checks for every edited reference, grep for remaining `/private/tmp` citations and for the stale phrases named above, a build for H6.
- One local commit per item. Report per item: commit SHA, files changed, what was dropped as already resolved, and anything stopped for a decision.
- In the report, list the proposed order for the remaining AU tickets and the UI integration tickets (D8) for the user to approve.
- Claude reviews the batch independently. Stop after the batch.
