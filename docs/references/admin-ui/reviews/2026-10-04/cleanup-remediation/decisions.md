# Decisions after the review (3 October 2026)

## Step 2 fix batch (to hand to Codex)
LK-1, EI-1, WA-1, OS-2, X-3, BR-5, BR-6, X-1 (banner rollout path; production has 1 asset), LK-2 (conversion run path; production has 1 version-1 checkpoint), and AU13 (see BR-2 below).

## Fix batch workflow (agreed 3 October)
- Brief: `09-codex-handoff-fix-batch.md`, sent to Codex by the user.
- Codex sends the F1 proposal to the user first; implementation continues with F2 onwards meanwhile.
- One implementer, items in order, with F2+F3 together and F4 early. One local commit per item, no push.
- Focused checks per item; evidence stays in the repository.
- The **final independent review is done by Claude**: read-only, commit by commit, against the brief and the original findings, written to `review-notes/`. F1 and F4 get extra attention.
- Codex stops and reports after the batch.

## Fix batch follow-ups (3 October)
- **R-1 (decided):** a failed Luck conversion keeps blocking deployment for this release.
- **R-2:** fixed in `f2ea1cf`; Claude recheck PASS.
- **R-3 (deploy gate):** before any production deployment, the full `bingo-deploy` sequence (`--migrate`, `--convert-luck-checkpoints`, `--production-preflight`, web start) must succeed against an isolated restored production backup. Any failure is resolved before deploying.

## Teams/Draft decisions (3 October)
- **TD-1 to TD-4:** go into the next fix batch together with BR-1/BR-3 (see `11-teams-draft.md`).
- **TD-7 (decided, option 1):** during Live and Final Review, a role change must also update what the public Teams page shows, as it already does before Live.
  - Earlier published versions are kept as history.
  - Membership stays locked from first Live; this affects role labels only. No WOM or membership side effects.
  - Implementation is Codex's choice (republish a new snapshot, or have the public page show current roles). The required outcome is that the public roster matches the real current roles.
  - Also close the small timing gap: decide the republish path from the state read inside the transaction, not before it.
- **TD-2 (decided, option B):** members of manual (not included) teams count toward signup capacity. The cap counts every event participant, whatever kind of team they're on. Reason: people reach a manual team only as existing participants (signup, or Participants Add with the cap or the +1 override), so the exclusion double-counted their place. Draft turns are unchanged: manual-team members still don't take draft turns. Adding to a manual team requires Confirmed; no manual-team changes during a Running draft.
- TD-5 (stale DATA_MODEL text) goes in the documentation cleanup. TD-8 stays an observation unless the batch touches those handlers anyway.

## Step 3 decisions
- **BR-2 (decided):** pull AU13 forward into the fix batch. Lift the post-finalization team-size lock and keep the manual planning estimate as the value used; actual roster sizes never replace it. This closes the regression against main and completes approved AU13.
- **WA-2 (decided, extends AU20):**
  1. The WOM check compares the competition with the website's **configured** start and end, exactly, at every stage, final review included. Actual times are not used for this check; they still drive the upload cutoff, eligibility and the review window.
  2. An early end or Resume always succeeds locally. It is never blocked on WOM.
  3. **Early end:** the WOM end is set to the click time rounded **up** to the next whole minute (21:59:55 → 22:00:00). The same value is stored as the event's configured end; the actual end keeps the precise click time. No lead time is needed.
     **Resume** (corrected 3 October; the earlier wording wrongly applied the click-time rule to Resume): Resume requires the admin to choose a replacement end in the future. The WOM end is set to that **replacement end**, which already passes the schedule's validation and increments. It is not set to the resume click time, and no click-time rounding applies. The replacement end is stored as the configured end, as the existing Resume does. The retry and fallback rules in steps 4–5 apply to this update too. Revised after Codex's WOM source check: WOM accepts editing an existing competition's end into the past (not before its start) and recalculates the end values from the last snapshots inside the revised window. The past-date rejection applies only to creation. Source: WOM `EditCompetitionService.ts` / `CreateCompetitionService.ts`, read from public source, not a live call.
  4. Temporary failures are retried with spaced backoff, never a fast loop that spams WOM's API. Because a past end is accepted, retries may continue until results are published.
  5. While the WOM end is unmatched, no fetch runs after the actual end, because it would count post-end gains. Once the update succeeds, normal fetches resume and give correct end-window data. If the update has still not succeeded at publication (WOM down for a long time, missing or invalid verification code, or another non-transient rejection), the event is marked "WOM end could not be updated", the final fetch is skipped, and the last fetch before the actual end is the official WOM data. Luck's freshness timestamp shows its age.
  6. External ID-only competitions can't be edited without a verification code, so they always take the step 5 fallback after an early end.
  7. AU20 must also list Resume as a third place where the 5-minute check lives. An end before the start returns HTTP 400 `COMPETITION_START_DATE_AFTER_END_DATE`; handle it as a non-transient rejection.
  - Known limit: WOM's end values come from the last player snapshot inside the window, so precision depends on how recently players were updated before the end.
  - The snapshot limit is known and accepted: players know to log out right before an event ends if they want their data fully updated.
- **BR-1 (decided):** approval follows upload order. While an earlier pending upload exists for the same team and objective, approving a later one is blocked with a clear message ("approve or reject the earlier upload first"). Rejecting the earlier upload unblocks the later one. Reason: a later submission must not take an earlier drop's progression for the team. This changes no scoring logic, and already-approved historical results are not rewritten. This message replaces the current "Mark the submission as a duplicate or reject it" text, which overlaps BR-5.
  - **Blocking scope (refined 3 October):** the block applies only when approving the later upload would reduce what an earlier pending upload for the same team and objective could still be credited. With room for both (e.g. 5 items needed, 1 each), the later one is approved normally. For one-item objectives this is the same as always blocking.
  - **Navigation (user addition):** the admin must not have to search for the earlier upload. The blocked-approval message links directly to the earlier submission's review page, styled like the rest of the UI.
    - Backend: the approval result returns the blocking submission's ID (and its upload time) as structured data, so the UI never parses message text.
    - Existing Review page: render the message with a link in the current style during this ticket.
    - New Review reference: bind the same data when the UI is integrated, following that page's link/navigation pattern (RC07/UI integration). It must keep the admin's queue/filter context on the way back.
- **BR-3 (bundled with BR-1):** the paused-interval warning judges the screenshot's game time, not the upload time, per the approved eligibility rule.
- **Placement:** BR-1 and BR-3 form one small ticket right after the step 2 fix batch. Both predate the branch, so they don't make production worse than main.

## Step 4 cleanup decisions (3 October)
All recommendations in `13-codex-handoff-doc-ticket-cleanup.md` section 3 accepted (option (a) for each):
- **D1 (BR-7):** documents say retained completion-time corrections are ignored (production has 0).
- **D2 (P-6):** keep the contract; Add/Restore return the capacity outcome during Participants integration.
- **D3 (P-7):** ticket for Restore to use stored account data, no WOM call, like Admin Add.
- **D4 (WA-6):** AU15 keeps generic wording; AU20 adds the structured reason.
- **D5 (BR-9):** AU17's approved full-pool correction becomes its own queued ticket.
- **D6 (WA-7):** AU16/AU21/AU22 are approved defect fixes; AU20/AU23/AU24 shown as approved.
- **D7 (DB-2/DB-3):** fixes are required parts of the Dashboard integration ticket.
- **D8:** Codex proposes the order inside the AU group and the UI integration group; the user approves.

## UI integration planning (3 October)
- **Owner:** Claude lays out the UI integration plan when that phase starts (after the remaining AU tickets). That covers batching, order, parallel lanes, and which model implements each batch. The user delegated these decisions; Claude reports the plan and its reasons, and the user can override.
- **Direction already agreed:** every page ticket gets an explicit inventory (routes, handlers, old → new mapping, what's retired or redirected, mechanically checkable leftovers). Use the stronger implementer model (project default `gpt-6-astra`) for the first page integration and for Review, Teams/Draft, Identity and Participants. Luna is acceptable for simpler pages once the pattern is set. Independent review is kept for every batch.
- Model choices that differ from the AGENTS.md defaults are recorded in each brief's routing section, as with `09` and `12`.

## AU ticket model choice (3 October)
- **Default implementer for the remaining AU tickets: `gpt-5.6-luna` / `max`,** as in the current batches.
- When Claude plans the AU batching (step 3 in `00-index.md`), Claude also decides which tickets or batches use `gpt-6-astra` because they're more complex, gives the reason for each, and records the choice in that batch's routing section. The user can override.

## Cleanup review answers (4 October)
- **Tile Luck section (H3-2):** the user visually inspected the Luck display on board tiles on 2 October and it looked correct. This is visual acceptance only; functional correctness is covered by automated tests, not by the user's check.
- **AU23 roll count (decided 4 October):** the drop-rate text is ordinary input for every Admin, exactly as the wiki shows it ("3/1024" = one roll at 3/1024; "3 x 1/1024" = three rolls at 1/1024), both when adding and when editing a drop. The roll count derived from "N x" is part of that text, so ordinary Admins may change it; the current "Changes to reward rolls require an operator" refusal is removed by AU23. SuperAdmin-only remains for the advanced mechanics the wiki text can't express: roll group above all (content like the Colosseum, where each wave has different rates), plus probability scope, conditional-on-parent/parent probability and assumed participants, as already approved.
  - **Follow-up (4 October):** new drops (including those on a newly added boss) use the regular `default` roll group; the code already does this (`SourceDrop.RollGroup` defaults to "default", and an ordinary Admin posting any non-default advanced field is refused at `Catalogue/Index.cshtml.cs:73`). The roll count has no input field of its own. **Correction (4 October):** the Catalogue UI reference does show it read-only, in the collapsed "How the rate is counted" panel (summary "3 rolls", row "Rolls per kill") and in the rate preview ("3 rolls of 1 in 1,024 · …"). It also still says "Changing the number of rolls in the rate needs an operator" and refuses such edits (`Catalogue.dc.html:378`, `:801`), which contradicts this decision and must be changed when the reference is bound. Whether to keep the read-only roll display is pending the user's answer.

## Drop-rate mechanics (4 October)
- **How rates are entered (user's rules):** EHB for team content assumes an agreed team size: the efficient kills per hour at the team size that balances kill speed against drops, not the maximum kills per hour. A drop rate entered by an admin is normally the **final** chance of seeing that specific item in one's own name at that agreed team size (e.g. Nex 1/2,000 in a team of 5).
- **Team size / "whose chance" (probability scope, assumed participants):** context only, so admins agree what the EHB and the rate are based on. It is never a calculation input. The code already behaves this way.
- **"Only after" (conditional on parent, parent probability):** a real calculation input, used only when the wiki rate is **conditional**: raid uniques (CoX/ToA/ToB) whose wiki rate applies after a purple (about 1/27 for a solo raid). Then the true chance = parent chance × item rate, and **both EHB and Luck must apply it**. Never set for final rates like Nex. Editable by SuperAdmin only.
- **Bug found:** Luck applies the parent chance, but EHB (catalogue drop EHB and board EHB estimates: `EhbCalculator`, `BoardEstimateService.cs:323-324`, `Catalogue/Index.cshtml.cs` effective probability) ignores it, so a conditional drop would look parent-times easier. **Production check by the user, 4 October:** `SELECT … FROM source_drops WHERE conditional_on_parent` returned **0 rows**, so no real board is affected. Fix in a ticket: EHB applies the parent chance the same way Luck does; affects new or recalculated draft estimates only, never approved/published snapshots or results.
- **Roll group:** a real calculation input (EHB and Luck treat drops in one group as competing outcomes of a single roll, e.g. Colosseum waves). New drops use `default`. Editable by SuperAdmin only.
- **Catalogue "How the rate is counted" panel (proposal, updated 4 October, pending user OK):** header shows the roll group; rows: chance per kill (including the parent chance), source, plus "Only after" only for conditional drops and roll group as a row only when not "default"; note if present. Bottom text: "Roll group and 'only after' can only be changed by the Super Admin." Removed rows: chance per roll and rolls per kill (stated in the rate text), and **whose chance** (user, 4 October: "your own chance at the agreed team size" is the general rule for the whole application, so repeating it per drop adds nothing).
- **Agreed team size belongs to the boss/activity (decided 4 October):** two drops on the same boss must never be based on different team sizes, so the team size is one value per boss, shown next to its kills per hour (what the EHB rate assumes). It stays informational, never a calculation input. Today it is stored per drop (`source_drops.assumed_participants` and `probability_scope`) and never calculated with. Ticket: move it to the boss. **Production check by the user, 4 October: 0 rows.** Every drop has the default (own chance, team size 1), so there are no set or conflicting values; the move needs no per-boss conflict decisions. The ticket re-runs the check just before migrating. Approved/published board snapshots keep the per-drop values they captured.
- **Decided 4 October (Option 1), superseding the "Only after" and EHB-fix bullets above:** always enter the final chance (the user entered the production raid rates that way). "Only after" is retired quietly from the editor, the panel and new input; the database columns stay so history and snapshots are untouched. No EHB parent-chance fix ticket. Panel proposal loses the "Only after" row.
- *(Superseded)* **Reopened 4 October (pending user answer):** the user checked production. All raid uniques are already stored as **final per-raid chances** (purple chance included), which is why 0 drops use "Only after". Proposed (Option 1): always enter the final chance; retire "Only after" from the editor and panel (keep the column for history); drop the EHB parent-chance fix ticket. Option 2 keeps "Only after" for wiki-literal raid entry and keeps the EHB fix. Until answered, H5-9's "Only after" and EHB-fix parts are on hold.
- **Roll groups in production (user query, 4 October):** non-default groups exist only where outcomes compete within one roll: Barrows Chests `barrows-equipment` (24 items, 7 rolls), Chambers of Xeric / Theatre of Blood / Tombs of Amascut (Expert) `purple table`, Doom of Mokhaiotl `doom-1-16-aggregate`, Sol Heredit `fortis-full-run-unique`. All 56 rows active. These confirm roll group is a real calculation input (Super Admin only) and that raid rates are stored as final per-raid chances. Existing groups are to be kept unchanged by any later ticket.

## Catalogue layout (decided 4 October)
- **"How the rate is counted" panel (per drop):** header shows the roll group ("default", "purple table", …). Rows: chance per kill (includes the "N x" roll count), source, and note only when the drop has one. Bottom line: "Roll group can only be changed by the Super Admin." The Super Admin edits the roll group in this panel; everyone else sees it read-only. Removed: chance per roll, rolls per kill, whose chance, "only after", and the operator sentence.
- **Team size on the activity:** a "Team size" field in the activity **Settings**, next to "Kills per hour" (whose help text already says "at the group size the drop rates assume"), and the same field in **Add activity**, which uses the same fields as Settings. Whole number, at least 1, default 1. Informational only: never used in calculations. Every Admin can edit it, like kills per hour.
- These intentionally differ from the frozen `docs/references/admin-ui/Catalogue.dc.html` reference. The Catalogue binding ticket (WA-5) follows this decision, not the reference, for these parts.
