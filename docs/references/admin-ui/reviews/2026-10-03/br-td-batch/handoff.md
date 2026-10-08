# Codex hand-off: BR-1/BR-3 and Teams/Draft fix batch

Status: **Authorized by the user on 3 October 2026: “You can go ahead with g1 through g6.”** It is limited to G1–G6 below. Other AU/RC work remains stopped.

## 1. Authority and baseline

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`.
- Start from `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5` (the R-2 fix; base main `22af254c893bb51e7820d84fc4154ff9af3bcc90`). Confirm HEAD and a clean tree before starting. If the branch has moved, stop and report. Expected documentation-only activation edits (CURRENT_STATUS.md, this brief) are allowed as a separate first commit.
- Source of findings: Claude's read-only review in `/Users/christopher/Documents/BingoWebpage/review-notes/` (index `00-index.md`). User decisions are in `08-decisions.md`. Each item cites its report section; read it before changing code. The reports are review evidence, not specifications. Where a report and an owning authority document disagree, the owning document plus `08-decisions.md` win; report the conflict.
- Line numbers below are from `f2ea1cf` and are pointers only.
- Follow AGENTS.md (working-tree safety, bounded execution, verification and handoff). Routing, roles and models are the user's/planner's choice; this brief does not prescribe them.

## Assignment routing

User instruction, 3 October 2026: same setup as `09-codex-handoff-fix-batch.md`.

- Planner: UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration parent `/root`.
- No orchestrator for this batch. One direct collaboration implementer uses `gpt-5.6-luna` / `max`; no extra implementation workers or separate chats.
- This assignment authorizes that implementer to perform its own narrowly scoped local staging/commits, including a separate initial documentation checkpoint for activation changes. This is an assignment-specific override of the normal packager route; no self-review is authorized.
- No item needs a decision before it starts. If one arises during work (for example G4's history-retention stop), send the proposal to `/root`, then continue with the next items while it awaits a decision.
- Send a direct collaboration report to `/root` before every final/turn-ending response and immediately for a blocker or required decision. Include item, commit/evidence, unresolved issue and exact next step. Do not use `wait_threads`, polling, or claim delivery guarantees that have not been observed.
- Independent final review belongs to Claude per `08-decisions.md`; prepare a stable per-item commit handoff and stop awaiting that review. Do not call implementation complete in the independent-review sense before it passes. Named remediation can return to this implementer when assigned.

## 2. Purpose

Close the two Review findings the user placed straight after the step 2 batch (BR-1, BR-3) and the Teams/Draft findings from the X-4 review (TD-1 to TD-4, TD-7). Nothing else. This is not a resumption of the AU queue.

## 3. Items

Work in this order. Each item is independently reviewable and gets its own commit. If an item needs a product decision not covered here, stop that item, report it with a recommendation, and continue with the others.

### G1 — BR-1: approval follows upload order (Review)
- Report: `06a-board-review-final.md` BR-1. Decision: `08-decisions.md` "BR-1 (decided)", including the navigation addition.
- Problem: `ApproveAsync` (`src/Bingo.Infrastructure/Evidence/SubmissionService.cs:156-165`) computes room from contributions already approved. With the queue's "newest first within Pending" order, a later upload B is normally approved first, and the earlier upload A can then only be rejected. The objective's completion time becomes B's upload time permanently, which feeds tile, board-finish and score-time ranking.
- Required outcome:
  - **Blocking rule (user decision, 3 October: the narrow, room-aware variant).** Approving an upload is refused only when it would reduce what an earlier **pending** upload for the same team and the same objective could still be credited. If the objective has room for the later upload and for every earlier pending one, the later upload is approved normally. Example: an objective needs 5 items, A (10:00, 1 item) and B (10:30, 1 item) are pending, nothing is approved yet: B can be approved first. If only 1 item of room remains, B is refused until A is resolved.
    - "Earlier" means earlier immutable server `SubmittedAt` (deterministic tie-break, e.g. ID, for equal timestamps).
    - "Could still be credited" uses the same calculation `ApproveAsync` already uses for an approval amount (claimed weight, remaining target room, per-drop maximums and duplicate rules), applied to the earlier pending uploads in upload order. Admins don't choose amounts, so this is deterministic.
    - When refused, the message tells the admin to approve or reject the earlier upload first. If several earlier uploads are affected, report the earliest.
  - Rejecting (or otherwise resolving) the earlier upload unblocks the later one.
  - The refusal returns the blocking submission's ID and upload time as **structured data** in the approval result. The UI never parses message text.
  - The existing Review details page renders the message with a direct link to the earlier submission's review page, in the page's current style.
  - The check runs inside the approval's transaction, under the same lock/isolation that already serializes approvals for that team/objective, so two concurrent approvals in reverse order can't both pass.
  - The current text "Mark the submission as a duplicate or reject it" is replaced. For the remaining case (no earlier pending upload, but no room left), use wording that doesn't overlap BR-5; propose it in the report.
  - No scoring or calculator change. Already-approved historical results are not rewritten.
- Not in this item: binding the new Review reference (RC07/UI integration). Record in the handoff that the integration must bind the same structured data and keep the admin's queue/filter context on the way back.
- Proof (real PostgreSQL):
  - Target 1, A and B pending, same team/objective: approving B is refused with A's ID/time and no writes; reject A → approving B succeeds.
  - Target 5, A and B pending with 1 item each: B approves first without a block; A then approves; completion facts match upload order.
  - Room for only one: B is refused. Per-drop maximum case: B would use up a drop limit A also needs → refused.
  - Approving A first, then B, works as today (within room).
  - Different team or different objective is not blocked.
  - Concurrent approvals of A and B in both orders: the result is consistent with upload order.
  - Page test: the refusal renders a link to A's review page.

### G2 — BR-3: paused-interval warning judges the screenshot's game time (Review)
- Report: `06a-board-review-final.md` BR-3. Decision: `08-decisions.md` "BR-3 (bundled with BR-1)".
- Problem: `FindEligibilityGapAsync(…, s.SubmittedAt)` (`src/Bingo.Web/Pages/Admin/Review/Details.cshtml.cs:93`, `:145-161`) feeds an alert (`Details.cshtml:99-104`) that tells the admin to treat the evidence as outside the eligibility intervals. A drop before a premature end that is uploaded during the grace period before a resume gets this alert, although the drop was in-window.
- Required outcome (approved rule, `PRODUCT_REQUIREMENTS.md` 7.2): screenshot game time decides activity eligibility; upload time decides cutoff and order.
  - The warning no longer uses `SubmittedAt` as the eligibility basis and no longer instructs the admin to treat the evidence as ineligible based on upload time.
  - Where the system has no trusted screenshot time, show the paused interval(s) as information and tell the admin to verify the screenshot's in-game time against them, in the same style as the adjacent "after event end" box ("Verify the screenshot time").
  - Upload-time cutoff behaviour is unchanged.
- Proof: the 11:55 drop / 12:00 premature end / 12:10 upload / 13:00 resume example shows no "treat as outside" instruction and shows the verify guidance with the paused interval. Existing after-end box tests keep passing.

### G3 — TD-1 and TD-2 inclusion boundary: serialize inclusion changes, then fix capacity (Teams/Draft)
Split into two commits: G3a (TD-1), then G3b (TD-2).

#### G3a — TD-1: inclusion change or team removal isn't serialized with Start or Finalize
- Report: `11-teams-draft.md` TD-1.
- Problem: `OnPostUpdateTeamAsync` (`src/Bingo.Web/Pages/Admin/Events/Draft.cshtml.cs:125-160`) reads the team and the draft state before its transaction, then writes in a ReadCommitted transaction. `OnPostRemoveDraftTeamAsync` (`:121-122`) has no explicit transaction. Start (`:356-408`) and direct Finalize (`:582-609`) run Serializable but write nothing that conflicts with the team row. Both orders commit, leaving a Running draft with an unvalidated team set, or a Finalized `DirectRoster` publication with 2+ included teams. The guard `ck_teams_formation_draft` that made inclusion immutable was dropped by `20260926113811_RetireFormationTypeConstraint`.
- Required outcome (`FUNCTIONAL_CONTRACTS.md` "Approved Admin simplification precedence": every touched mutation rechecks lifecycle, dependencies and versions at its authoritative boundary; `PRODUCT_REQUIREMENTS.md` 17.1: inclusion follows the structural lock):
  - UpdateTeam (inclusion and active changes) and RemoveDraftTeam read the draft and event state **inside** their transaction and are serialized with Start and both Finalize paths, e.g. by locking the draft row (`FOR UPDATE`) in all of them, or by running them Serializable with a conflicting write. Choose one mechanism and apply it consistently.
  - Outside Setup (Running, Finalized) or after the configured start, an inclusion change or included-team removal is refused with no writes.
  - Start's audit records the team set it actually validated.
- Proof (real PostgreSQL, independent connections, both orders): inclusion change vs Start; inclusion change vs direct Finalize; RemoveDraftTeam vs Start. Each pair ends either with the change refused or with Start/Finalize seeing the new team set; never a Running draft with an unvalidated team or a `DirectRoster` publication with 2+ included teams. Plus: inclusion change refused while Running and while Finalized.
- Optional (only if the same handler is touched anyway): TD-8's RemoveDraftTeam vs concurrent AddMember. Report if included; don't widen otherwise.

#### G3b — TD-2: manual-team membership changes capacity without promoting waiters
- Report: `11-teams-draft.md` TD-2.
- Problem: `SignupParticipants()` (`src/Bingo.Infrastructure/Signups/SignupService.cs:2905-2908`; same in `src/Bingo.Infrastructure/Events/EventSignupLifecycleService.cs:357-360`) excludes active members of active non-included teams from every capacity decision. Switching an included team to manual (`Draft.cshtml.cs:148`) or adding a Confirmed participant to a manual team (`:193`) lowers the count without promoting waiters, so a later signup can be Confirmed ahead of earlier waiters. The reverse switch can push Confirmed above the cap. AddMember to a manual team doesn't require Confirmed status and has no draft-state gate. `PromoteAvailablePlacesAsync` has no non-test caller.
- **Decision (user, 3 October 2026, option B): manual-team members count toward signup capacity.** The cap counts every participant in the event, whatever kind of team they're on. Reason: a person can only reach a manual team as an existing event participant (a signup, or a Participants-page Add, which already applies the cap or the explicit +1 override), so they have already been counted once. The exclusion is a leftover from retired accountless Preformed teams.
- Required outcome:
  - Remove the manual-team exclusion from every **capacity** decision: admission, promotion, the capacity floor and any capacity display, in both `SignupService` and `EventSignupLifecycleService`. Changing a team's inclusion or adding someone to a manual team then never changes the capacity count.
  - Draft turns are **unchanged**: manual-team members still don't consume draft turns and aren't in the draft pool (`DATA_MODEL.md` "Teams not included in the draft and their assigned members do not consume draft turns"). If the same helper feeds both capacity and the draft pool, split it; don't change the draft side.
  - Going over the cap uses the existing explicit +1 override on the Participants page. No new override.
  - Adding someone to a manual team requires Confirmed status, the same as an included team. A waiter must be confirmed first (normal capacity or +1).
  - Manual-team membership changes respect the draft lock: refused while a draft is Running.
  - Check whether removing the exclusion changes counts or blockers for any existing event (capacity is locked after draft lock, so historical events should be unaffected) and report the finding.
  - Update `DATA_MODEL.md` (capacity/waiting-list section) to state that capacity counts all participants regardless of team type.
- Proof (real PostgreSQL): full event with waiters → switching a team to manual, or adding a Confirmed person to a manual team, leaves the count unchanged, keeps waiters waiting and doesn't let a new signup be Confirmed; the reverse switch never pushes Confirmed above the cap. Adding a waiter to a manual team is refused. Manual-team add during a Running draft is refused. Draft turn plan with a manual team present is unchanged.

### G4 — TD-3: restart after cancelling an attempt that had picks
- Report: `11-teams-draft.md` TD-3.
- Problem: Cancel keeps `FirstPickRecordedAt` (`src/Bingo.Domain/Teams/DraftSession.cs:54-62`) and the team order (`Draft.cshtml.cs:546-552`). Setup still lets the admin add or include a team, but that team has no position, and Scramble (`:335-338`), Pick (`:433-437`), Finalize (`:630-631`) and preassignment to any included team (`CanDirectDraftedSetupAssignment`, `:818-820`) are then permanently blocked. The guidance names Scramble, which is always refused.
- Required outcome (`PRODUCT_REQUIREMENTS.md` 17.1: an eligible cancellation returns the private attempt to Setup): after an eligible Cancel, Setup is consistently usable. Every Setup action the page offers leads to a draft that can be started, picked and finalized, or is refused up front with a correct message. No guidance names an action that is always refused.
  - Preferred direction: an eligible Cancel restores a full Setup for the next attempt (new or newly included teams can get a position, a fresh scramble is allowed, preassignment works) while the cancelled attempt's pick history stays retained as history.
  - Alternative: lock team structure and inclusion while `FirstPickRecordedAt` is set, with correct messages.
  - If the chosen direction changes what history is retained, or contradicts the existing `CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory` assertions in a way beyond history retention, stop and propose.
- Proof: cancel after picks → add/include a team → restart → scramble/pick → finalize succeeds (or the add is refused up front, for the alternative). Cancel after picks → remove a Captain → add a replacement Captain to that team → restart works. Pick history from the cancelled attempt remains.

### G5 — TD-4: finalized-roster Add can assign Captain or Co-captain
- Report: `11-teams-draft.md` TD-4.
- Problem: `SignupService.cs:1477-1478` refuses any role but Participant on a finalized-roster Add, and the Add form (`Draft.cshtml:576-582`) has no role field. Adding a Captain takes two operations and two publications, the first showing the person as a Participant.
- Required outcome (`FUNCTIONAL_CONTRACTS.md` 5.6: Add "assigns Participant/Captain/Co-captain and republishes atomically"): the service accepts the three roles and publishes once with the chosen role; the existing page's finalized Add form gets a role field (default Participant) in the page's current style. Existing Add rules (version checks, first-Live lock, account choice, reuse, no cap) are unchanged.
- Proof: Add with Captain and with Co-captain → one new publication showing that role; an invalid role is refused with no writes; existing finalized Add/Remove tests keep passing.

### G6 — TD-7: Live and Final Review role changes update the public Teams page
- Report: `11-teams-draft.md` TD-7. Decision: `08-decisions.md` "TD-7 (decided, option 1)".
- Problem: from Live onward, a role change (`Draft.cshtml.cs:297`) calls `ChangeRoleAsync` only, while the public page renders the roles frozen in the active publication (`src/Bingo.Web/Pages/Events/Teams.cshtml.cs:51-75`). The page also picks its path from a state read before the transaction (`:263-266`).
- Required outcome:
  - During Live and Final Review (while role changes are allowed), a role change makes the public Teams page show the real current roles, as it already does before Live.
  - Implementation is Codex's choice: republish a new snapshot in the same transaction, or have the public page show current roles. Earlier published versions are kept as history.
  - Role labels only. Membership stays locked from first Live; no WOM or membership side effects.
  - The republish/no-republish path is decided from the state read inside the transaction, not before it.
- Proof: a role change during Live and during Final Review → the public page shows the new role; earlier publication versions still exist; membership and WOM untouched. A role change racing Finalize (real PostgreSQL, both orders) ends with the public roster matching current roles.

## 4. Out of scope (do not do)

- Any AU/RC ticket, UI integration or reference binding (including the new Review and Teams references); frozen `*.dc.html` references.
- BR-4, BR-5's remaining text, WA-2/AU20, DB-2/DB-3, WA-3.
- TD-5 (stale DATA_MODEL text) and other documentation cleanup beyond what an item directly needs: that is the separate step 4 pass. TD-8 only as noted under G3a.
- The R-3 deploy rehearsal.
- Production access, user-database mutation, deployment, merge or push, unless separately authorized.

## 5. Verification and handoff

- Focused tests per item at the affected boundary (real PostgreSQL where persistence or concurrency is involved). Scoped build/format/diff checks. Not a full-suite rerun without reason.
- Keep durable evidence inside the repository (e.g. under `docs/references/admin-ui/reviews/`), not only in `/private/tmp`.
- One local commit per item (G3a and G3b separate), containing only that item's code, necessary docs and evidence. Inspect the staged diff and preserve unrelated work. Commit review corrections separately with the affected item IDs; don't amend earlier checkpoints. Record blocked items separately; don't mark them completed. Push, merge and deployment remain prohibited.
- Report per item: commit SHA, changed files, tests and results, deviations, and anything stopped for a decision. Update `CURRENT_STATUS.md` and owning docs only where an item changes behaviour they describe.
- The independent review of the finished batch is done by Claude (read-only, commit by commit, against this brief and the original findings). Don't call the batch technically complete before it passes. G1 and G3a get extra attention.
- Stop after the batch. Don't start step 4 (ticket/doc cleanup) or anything else without a new instruction.
