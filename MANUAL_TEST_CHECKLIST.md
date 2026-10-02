# Manual Test Checklist

**Status:** Current September follow-up, managed WOM functionality and boss leaderboards accepted by the user on 2026-09-23, with the explicit waivers/deferrals below. Older slice checklists remain separate history.

**Last updated:** 2026-10-01

**Purpose:** Preserve the user's manual acceptance checks outside chat without adding testing controls to the application.

**Authority boundary:** This file records manual verification journeys, observed results, and accepted evidence only. Product behavior and scope, workflow contracts, data invariants, technical architecture, and UI rules/approval are owned by the active authority documents linked from `README.md`; this checklist does not redefine them.

## Dashboard backend proof complete; UI integration deferred — 2026-10-01

The read-only backend contract and focused PostgreSQL proof are complete. This
section does not claim manual or visual acceptance. Current automated evidence is
`/private/tmp/dashboard-backend-20261001/dashboard-ordering-fixed4.trx` (2/2)
and `dashboard-b2-post-ordering.trx` (7/7), with Release builds for the
Application.Tests and IntegrationTests projects passing with 0 warnings/errors.
Named review-remediation evidence is also
`/private/tmp/dashboard-remediation-20261001/dashboard-remediation-r1-r5-fixturefixed.trx`
(9/9), `dashboard-remediation-ordering.trx` (2/2), and the latest bounded
continuation evidence: `dashboard-remediation-r2-r3-r1-r5.trx` (8/9 with one
PostgreSQL initialization-only authentication failure), its isolated
`dashboard-remediation-r5-queryshape-retry.trx` (1/1), and the affected
`dashboard-remediation-r3-original-login-boundary.trx` (1/1). The latest cases
cover the focused R1–R5 backend boundaries, including unavailable ended-date
classification, weighted published-drop/importless-roster submissions and
reopened winner suppression. The independent Dashboard source review is PASS
after the same reviewer resolved R1–R5 and their direct consequences. It inspected
source and recorded evidence without rerunning tests. Later UI integration and
manual acceptance remain pending.

After UI integration:

- Open Dashboard as an enabled Admin with no events, imported-only history,
  one tracked event and mixed history; confirm truthful coverage and matching
  totals/chart/history. Live and final-review figures are Provisional; the recap
  remains ended-only and the current-event card retains the actual phase.
- Open chart, history, recap and current-card destinations; verify stable event
  identity, direct entry/reload and Back preserving sort/context.
- Check provisional, finalized and reopened results, winner ties and missing/
  partial EHB without invented zero or stale official winner.
- Exercise controlled read failure and retry, newer-response ownership and lost
  authorization; confirm no write/sync side effect or fabricated success.
- Accept both themes, narrow horizontal table scrolling, chart keyboard/touch
  details, focus, localization and reduced motion on the real integrated page.
Page visual approval remains exclusively in UI_PAGE_MATRIX.md.

## Participants new UI integration — deferred 2026-09-30

The user explicitly deferred manual testing until the new Participants UI is
integrated. Backend work is authorized now with automated checks and independent
review; none of the journeys below is claimed manually passed.

Later bind/test the actual new Participants table, Add and edit drawer:

- In an event Draft before signups open, then SignupOpen/SignupClosed before team
  draft, perform authorized admin Add/corrections; distinguish team-draft lock.
- Confirm a selected waiter in an open place and at full capacity with explicit
  +1 confirmation; see the intended person and unchanged remaining queue order.
- Move a confirmed team member to Waiting, disclose team removal, observe the next
  waiter promoted; disabled/no-op cases include open places and no eligible waiter.
- Withdraw and restore with normal and explicit expanded-capacity placement;
  observe refreshed counts, queue order and applicable confirmations.
- Add existing saved accounts, select primary and payment without signup questions
  or WOM; exercise configured limits and actionable missing-data/conflict feedback.
- Switch primary and edit event accounts; reopen and verify primary/EHB while the
  member's saved accounts/defaults remain unchanged.
- Exercise stale requests, changed permission/lifecycle and failure recovery;
  payment/private notes retain their existing broader administration window.
- Verify drawer/direct URL/Back/Forward, dirty closing, focus, filters/scroll and
  responsive feedback against the approved new reference during UI integration.

No temporary controls, demonstration seeds or user-database reset are authorized
by this deferred checklist.

## Current walkthrough disposition — user acceptance, 2026-09-23

The user said “everything approved” after the final boss-KC formatting and actual
Board-family Stats masthead corrections. This closes the current manual acceptance
round; page-specific visual approval is owned by `UI_PAGE_MATRIX.md`. The earlier
pending/blocking wording in this document is historical where superseded here.

- Real managed WOM creation and pre-Live deletion were manually exercised and
  passed. The failed-create retry correction is running; its focused tests and
  independent review passed. WOM controls' visual polish and the broader Admin
  UI/backend overhaul remain explicitly deferred, not newly approved designs.
- Boss leaderboards were manually exercised with real competition-period KC for
  92/93 imported accounts. One unresolved historical account identity remains a
  fixture coverage limitation, not a blocking product defect or fabricated zero.
  Final `-1` → em dash, whole-number boss KC averages and actual shared Board
  masthead on Stats received the user's final approval. Independent UI review of
  these final display corrections was explicitly waived; focused checks/builds
  passed, and no independent review PASS is claimed for them.
- Checks 2.6, 3.8, 4.6 and 8.2 were explicitly approved. Approval does not claim
  that each underlying scenario was manually executed.
- Section 9's completion-time/finalization manual exercise was explicitly waived,
  relying on existing automated evidence.
- 10.2 has no actionable change through the current UI. If the Admin overhaul
  exposes WOM-relevant edits, synchronization must be tested then.
- Empty-team rejection and post-Live deletion/participant locks (10.3/10.4)
  rely on existing automated checks; they are not outstanding manual blockers.
- Real update-all and scheduled update/fetch verification (10.5/10.6) is deferred
  to the live event by the user, relying on existing automated evidence meanwhile.
  The one-off archived metrics backfill is not proof of the scheduled pipeline.
- NEW acknowledgement passed with a newly submitted drop; seeded-data behavior
  was not established as a product defect. Admin/SuperAdmin event-page-only banner
  behavior was explicitly accepted.

No commit, push, deployment, production data change or further implementation is
implied by this acceptance.

## Admin-managed WOM competition — 2026-09-22 (technical checks pending visual acceptance)

The isolated candidate adds a managed-only Create/preview/update/delete journey to
Admin Manage. Automated checks cover the complete pre-Live Playing roster,
provider-normalized limits, protected receipt state, UTC payloads, and source-bound
forms. Manual acceptance remains pending: review the English and Danish Manage
sections for finalized, empty-team, pending/unknown, Live roster-lock, and
pre-Live confirmed-delete states. Do not use a real WOM mutation or a user-owned
database during this walkthrough.

## Boss KC leaderboards — 2026-09-17 (technical review PASS; acceptance pending)

Scope: PRODUCT_REQUIREMENTS.md section 16.1 and DELIVERY_PLAN.md section 19.
Automated isolated fixture evidence is in
`/private/tmp/bingo-boss-leaderboards-20260917/remediation-evidence.md`; final independent
review PASS is in the same directory's `review.md`. No manual checks below are claimed
executed. The user has build/run instructions for the existing local HTTPS7131 preview;
its current restart/build state and visual acceptance are unverified. Do not reset data.

- Enter Leaderboards through Board navigation. Default EHB & Drop EHB retains its
  three existing views. Verify only MVP cells and the approved Danish Rank heading
  changed in their tables. Rank reads Rank in both English and Danish in all views.
- Open METRIC using the reused header dropdown. Select each of two relevant bosses;
  Teams/Players and reload/back navigation remain coherent. Check an unlinked event.
- Compare boss tables directly against existing table styling, sorting and expansion.
  Check contributor-only rows/count/average, multi-account totals, a zero-contribution
  team and one/tied/no MVP examples. No gain-unit suffixes; keep Start/End labels.
- Inspect known stale/estimated figures and missing data: compatible values stay visible
  without freshness/update status text; unknown per-account endpoints remain dashes.
- Click a player's coral Drops number: its count is player-specific, while the existing
  Drops destination searches that boss and selects the whole team, intentionally.
- Check selector position with standings expanded/collapsed and at narrow widths/long
  labels. Stack only when needed, with Metric ABOVE tabs and both left-aligned above
  the divider. Exercise outside/repeated-trigger closing, Escape, option labels without
  METRIC:, keyboard selection and team disclosure. Metric changes stay in place without
  page reload/scroll jump; verify Back/Forward, repeated selection and usable failure
  recovery. Check existing green and + on gains/MVP, arrow-free coral Drops count and
  uniformly muted-blue dark headers. Check the light-mode METRIC trigger is ink normally
  and blue when open, hovered or keyboard-focused. In Danish, boss Teams/Players headings read
  Opnået/Start/Slut. Nested EHB headings omit EHB wording; Drop EHB headings are
  unchanged. Record actual user acceptance.

Objective-breakdown wording and tile-sidebar spacing remain deferred until this slice
is approved. Existing unrelated page approvals remain valid.

## Ticket acceptance walkthrough — 2026-09-14

**Status: integrated candidate PASS; manual acceptance pending.**
Independent integration review found no blocking findings. All 128 changed/new files
match the frozen manifest. Report: [integration PASS](/private/tmp/ticket-integration-evidence/integration-review/independent-review-result.md).
Manifest SHA256: `ced7502b5deae58679b2a78cc530e948a9ed0894cf5c30f8deaee29f19afbaa8`.
The candidate remains uncommitted. Managed-artwork removal-to-Discard recovery passed.
Use only this ticket section; older slice sections below are separate history.
Exact per-check links and prepared values: [manual handoff](/private/tmp/ticket-integration-evidence/manual-handoff.md).
Run MR-18's exact initial row-count check before MR-17/MR-19 add submissions.
Inspect MR-10's original pool before MR-09 applies its CSV. Results are recorded below as the user completes the walkthrough.

### Start the prepared candidate

Checkout `/private/tmp/BingoWebpage-ticket-integration-20260914`, branch `codex/ticket-integration-20260914`, base `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. The exact uncommitted candidate is identified by `final-review/manifest.json` and its full patch. No commit/merge/push is implied.

```bash
cd /private/tmp/BingoWebpage-ticket-integration-20260914
dotnet build Bingo.slnx --configuration Release --no-restore
/private/tmp/ticket-integration-evidence/manual/run-candidate.sh
```

The launcher uses the existing `https` launch profile, overriding its URLs to **https://localhost:7147** and http://localhost:5177. It sources the private environment, starts only `bingo-ticket-manual-20260914`, starts the local synthetic WOM endpoint on 5187, and runs:

```bash
dotnet run --project src/Bingo.Web --configuration Release --no-build --launch-profile https -- --urls "https://localhost:7147;http://localhost:5177"
```

Use the launcher so the isolated database/storage and local WOM fixture are selected. Stop it with Ctrl-C. The preflight server is stopped before handoff; its isolated PostgreSQL container and storage are retained. The user’s existing HTTPS7131 app and database were untouched.

**No reset is needed.** Tool restore, migrations, owner bootstrap, catalogue snapshot and the README reset/seed command already ran against the new disposable database only; supplements were then added. Running `--reset-test-data` again would remove these prepared examples. Do not use the README database-drop command for this walkthrough.

Prerequisites actually used: .NET 10 SDK, Docker Desktop running, the existing trusted ASP.NET HTTPS development certificate (verified), Python 3 for the local synthetic WOM server. No certificate installation or provider registration was performed. Database port is 62600; database/user `bingo_ticket_manual`; storage is under this evidence folder. Private env/access files are mode 0600, outside Git and the review patch.

Prepared Live/signup dates currently extend through **16 September 2026**; later execution needs the delivery worker to refresh only these isolated fixtures before dependent checks. Early-finalized former cutoff is also 16 September. Manual browser, viewport, language and theme are to be recorded by the user; HTTP preflight is not visual approval.

### Accounts

Open [local access instructions](/private/tmp/ticket-integration-evidence/manual/access.txt) for the synthetic passwords. Do not paste them into a review report. Log in at [Login](https://localhost:7147/Account/Login). Use separate browser profiles/private windows for independent sessions.

| Account | Prepared role |
| --- | --- |
| TicketReviewAdmin | Owner/SuperAdmin, all manual administration |
| TicketReviewAdminTwo | Independent Admin session |
| TicketReviewPlayer | Ordinary participant in Live/final fixtures, signup owner; Captain only in the separate neutral-sibling fixture |
| TicketReviewCaptain / TicketReviewCaptainB | Team A / Team B leadership |
| TicketReviewWaiter1 / TicketReviewWaiter2 | Draft waiting positions 1 / 2; Waiter1 saved primary is **Review Replacement**, old signup character is **Review draft 5** |
| TicketReviewEmergency | Password setup complete, initially disabled, scoped to External Clan Team in the prepared pre-Live event |

### Prepared scenarios

| Scenario | Exact pages | Initial facts |
| --- | --- | --- |
| signup | [Manage](https://localhost:7147/Admin/Events/Manage/95623327-a0aa-442b-8327-dd11f10b233f) · [Draft](https://localhost:7147/Admin/Events/Draft/95623327-a0aa-442b-8327-dd11f10b233f) · [Participants](https://localhost:7147/Admin/Events/Participants/95623327-a0aa-442b-8327-dd11f10b233f) | Public SignupOpen, ordinary owner Review Player, Primary/Captain/Co-captain/Public review note questions. |
| draft | [Manage](https://localhost:7147/Admin/Events/Manage/d3411576-9b29-45f5-b518-6e31e8f716f0) · [Draft](https://localhost:7147/Admin/Events/Draft/d3411576-9b29-45f5-b518-6e31e8f716f0) · [Participants](https://localhost:7147/Admin/Events/Participants/d3411576-9b29-45f5-b518-6e31e8f716f0) | SignupClosed/Setup draft; Review Draft A/B plus Review Preformed; ordinary internal pool Review Player and Review draft 3; preformed Review draft 4/6 excluded; two interspersed waiters. |
| live | [Manage](https://localhost:7147/Admin/Events/Manage/e1f91c6a-8746-49f6-99e0-8d2147947ebd) · [Board editor](https://localhost:7147/Admin/Events/Board/e1f91c6a-8746-49f6-99e0-8d2147947ebd) | 1×2 illustrated published board. Review trophies target 3, weight 2, Team A approved contributions 2+1=3. Illustrated practice runs target 5, EHB 5, no evidence initially. Team A ledger: 30 rows (27 Pending, 2 Approved, 1 Rejected). |
| neutral | [Manage](https://localhost:7147/Admin/Events/Manage/a4b98f07-77ea-4c6b-bf41-1a0d5d7efb67) · [Board editor](https://localhost:7147/Admin/Events/Board/a4b98f07-77ea-4c6b-bf41-1a0d5d7efb67) | One manual tile, EHB 1; two target-5 objectives. First approved for 5; second has no evidence. Team A earned EHB 0.5. Changing only second objective Duplicates allowed is scoring-neutral. |
| final | [Manage](https://localhost:7147/Admin/Events/Manage/5a1d34a1-ceac-44ec-8fb7-55a6e74fce2f) · [Board editor](https://localhost:7147/Admin/Events/Board/5a1d34a1-ceac-44ec-8fb7-55a6e74fce2f) | AwaitingFinalReview, both teams complete target 3 with approved 2+1; Team A has one Pending and one Rejected row. No acknowledgments performed. |
| missing-art | [Manage](https://localhost:7147/Admin/Events/Manage/3765ddc7-09a6-4653-a953-919d39f14feb) · [Board editor](https://localhost:7147/Admin/Events/Board/3765ddc7-09a6-4653-a953-919d39f14feb) | Live with private correction already open; synthetic artwork for Illustrated practice runs is deliberately absent. Discard must report failure and stay open. |
| cancelled | [Manage](https://localhost:7147/Admin/Events/Manage/e6abc54b-0b7d-42dc-b383-55d0ed844117) · [Board editor](https://localhost:7147/Admin/Events/Board/e6abc54b-0b7d-42dc-b383-55d0ed844117) | Separate previously published cancelled fixture. Private reason must not appear in public cancellation state. |
| early-finalized | [Manage](https://localhost:7147/Admin/Events/Manage/a3683d36-9373-48b3-8cee-c2aed0cac26a) · [Board editor](https://localhost:7147/Admin/Events/Board/a3683d36-9373-48b3-8cee-c2aed0cac26a) | Official results already published through the real service; original cutoff remains in the future. Unfinalize must not reopen uploads. |
| Prepared pre-Live | [Manage](https://localhost:7147/Admin/Events/Manage/65f80b22-9dec-4623-b039-30a116be397a) · [Draft](https://localhost:7147/Admin/Events/Draft/65f80b22-9dec-4623-b039-30a116be397a) · [Board](https://localhost:7147/Admin/Events/Board/65f80b22-9dec-4623-b039-30a116be397a) | Finalized draft; validated private board, three teams. Emergency setup complete but disabled; Waiter1 ready as replacement. Publish board/start through normal controls. |


### How acceptance works

There are **25 visible checks in seven journeys**, plus one short evidence-summary
acknowledgment. Several tickets share a check. This is a walkthrough of changed behavior,
not a repeat of every automated test or a whole-site redesign review.

Check a box only after seeing its expected result on the recorded candidate. Report an
issue by check ID and what happened; a short note is enough. A skipped or unavailable
check stays unaccepted unless the user explicitly accepts its named limitation. Pause
an affected journey when its setup or expected outcome is broken; the delivery worker
handles the correction, rather than asking the user to investigate internals.

Use the normal desktop viewport for the journeys. Check the changed filter, tile editor
and new discard/error confirmations once at narrow width and with keyboard navigation;
use a representative second-language/light-or-dark check for new wording. Do not repeat
all journeys across every browser/theme/language combination. Preserve existing page
composition. Fresh confirmation, error and recovery states need their own observed
acceptance; a prior approval of the page layout does not automatically accept them.

### 1. Accounts and signup — MR-01 to MR-04

- [x] **MR-01 — Corrected character survives rejoin and restore (C05, C09).** In the
  prepared ordinary signup, correct the linked character in My Accounts, withdraw and
  rejoin. Also follow the prepared Admin withdrawal/restore variant. The selected
  primary character stays correct. Newly created withdrawal/restore notifications open
  that recipient's signup instead of unexpectedly sending them home. No old-data cleanup.
- [x] **MR-02 — Account edits recover from an old tab (C08).** Open My Accounts in two
  tabs, save a change in the first, then try to save the older second form. A clear
  conflict/reload path appears, useful entered values are retained, and the first save
  remains after reload. A fresh edit works.
- [x] **MR-03 — Discord account feedback and last login (C06, C07).** Real-provider
  linking/replacement/login remains pending the user's Discord sign-in and a valid OAuth
  callback for the isolated HTTPS candidate. If that setup is unavailable, skip this
  portion and leave it pending. When available, follow Settings link/replacement and
  ordinary Discord login. Wording distinguishes linking from replacing; returning to
  Settings shows the expected linked account. Admin Accounts shows the successful login
  time. Prepared local Settings/last-login inspection and existing controlled callback/
  session evidence remain separately identified; they do not establish that real-provider
  manual acceptance passed. Password-session invalidation and rejected-callback behavior
  use the existing executable evidence; no manual token work or provider setup changes
  are included in preparation.
- [x] **MR-04 — Stale Admin confirmation (C39).** Open a prepared account action, change
  the target's state in the other authorized session, then confirm the old action. It
  explains that the state changed and offers recovery; the newer state remains. Reload
  and use the normal current action. Judge the new conflict wording and confirmation flow.

### 2. Event setup and competition settings — MR-05 to MR-08

- [x] **MR-05 — Invalid dates keep the form usable (C01).** Enter a passed date or signup
  close after event start. Relevant validation appears without a generic error page or
  losing other fields. Correct it and create the event successfully. Include the prepared
  competition-derived-date example if it adds a different visible validation state.
- [x] **MR-06 — Reused names have usable URLs (C02).** Create the prepared same-name
  pair with automatic URLs: both open independently. An explicitly duplicated URL gives
  a useful validation message and can be corrected. There is no need to race creations.
- [x] **MR-07 — Public answers warning and signup code (C03, C04).** Open signups with
  a public text question: the warning makes answer visibility clear and requires the
  expected acknowledgment. The no-text-question example has no irrelevant warning.
  Save the prepared signup-code setting and verify the normal signup prompt/behavior.
  Scheduled-opening and audit-failure variants are covered by recorded checks.
- [x] **MR-08 — Live WOM competition replacement (C29).** In the Live event's settings,
  clearing the competition is unavailable. The prepared valid replacement follows the
  existing confirmation/reason flow and displays the new association. Pre-Live settings
  still use their normal flow. This does not add boss-KC Stats functionality.

### 3. Draft, roster and start — MR-09 to MR-13

- [x] **MR-09 — CSV errors preserve account roles (C13).** Preview the supplied blank-
  primary and invalid-encoding files: useful errors appear and nothing is applied.
  Preview the valid file: primary, secondary and informational columns stay in their
  intended positions; optional blanks are accepted. Apply the valid disposable example.
- [x] **MR-10 — Ordinary pool and waiting list (C10, C14).** Eligible internal signups
  appear in the normal pool and can be assigned/picked. Actual preformed members stay
  out of that pool. Interspersed preformed rows do not interrupt waiting positions 1, 2.
  Existing website-versus-external removal controls still make sense.
- [x] **MR-11 — Captain readiness, roster move and notifications (C12, C15, C16, C17).**
  Make a valid same-event roster move and follow the resulting authorized context.
  In the prepared draft, removing its last required Captain prevents finalization with
  a useful correction destination; assigning a Captain permits it. Follow sample
  leadership/Admin notifications before and after publication: each reaches a usable
  page for that recipient. Forged cross-event moves and transaction faults stay automated.
- [x] **MR-12 — Finalized pre-Live departure and replacement (C11, C17).** Record a
  departure with the optional note, inspect the vacancy, and choose either waiting-list
  or internal replacement from the prepared examples. Leaving a vacancy is possible.
  The published roster, empty-team state, Captain recovery and notices tell a consistent
  story; departed-owner and leadership notices lead to permitted destinations. A private
  note stays out of public/member notices. Prior picks/history remain readable. This
  does not require a Running/Paused departure workflow, which remains deferred.
- [x] **MR-13 — Start readiness and the correct replacement character (C18, C38).**
  Enable the set-up emergency fallback after draft finalization. It can satisfy start
  readiness, but evidence submission remains closed until actual start. Start the prepared
  event, then perform its Live replacement: the replacement's current saved primary is
  active, not an older secondary. Judge the phase-specific controls and feedback; exact
  time boundaries and authorized early-start behavior use the recorded tests.

### 4. Catalogue and board editing — MR-14 to MR-17

- [x] **MR-14 — Useful objective/approval validation (C19, C25).** A catalogue objective
  uses derived EHB; a manual objective permits its legitimate explicit estimate. The
  prepared incomplete position/source/estimate cases identify what needs fixing. Correct
  the example and approve it. No manual override should hide a broken standard estimate.
- [x] **MR-15 — Catalogue conflicts are recoverable (C23, C24).** With a board open,
  change a relevant catalogue input in the other session. Old approval requires reload
  and review of the new values. Also edit the same shared item through its two prepared
  source forms: the old form refuses to overwrite the first save and gives clear recovery.
- [x] **MR-16 — Wording correction preserves the published game (C20).** On a tile with
  evidence, change only title/description privately. Players still see the currently
  published version and can read/submit evidence. Publishing the wording change updates
  the text while preserving recorded progress and readable prior submissions. Attempts
  to change substantive rules or remove an evidenced objective explain why they are
  blocked. The prepared scoring-neutral unevidenced sibling can still be changed.
- [x] **MR-17 — Discard a private correction, including artwork (C20, C21).** Privately
  change/remove the prepared unevidenced illustrated objective; the published artwork
  and player view must remain usable. In a participant session, submit against the old
  publication: incompatible replacement publication is then refused. Open Discard
  private correction: its warning clearly says **all unpublished edits** will be lost.
  Cancel/Escape preserves them. Confirm restores the published working board, original
  objective identity and artwork, closes the correction, and permits a new correction.
  Evidence/progress stay intact. Also view the prepared missing/unreadable-artwork refusal:
  it reports failure and leaves the correction open. The delivery worker supplies this
  controlled error state; the user must not delete storage files to manufacture it.

### 5. Submissions, review and recorded changes — MR-18 to MR-20

- [x] **MR-18 — Submission status filter (C30).** Combine status, player and search,
  paginate and reload. The full matching authorized history is filtered, not merely the
  current page. Clear filters and open a result; the filter is understandable and usable
  at narrow width without disrupting the accepted ledger layout.
- [x] **MR-19 — Changed authority and concurrent review feedback (C28, C33).** Use the
  prepared stale Captain form after losing the relevant role: teammate submission is
  refused; ordinary permitted self-submission remains available. View the prepared
  concurrent-review losing request: it says the action was not saved, directs you to
  reload current state and retry, and a fresh permitted action succeeds. Worker-run
  tests supply the actual timing/race proof; judge visible wording and recovery here.
- [x] **MR-20 — Reversal and ordinary audit entries are understandable (C04, C15, C22, C24, C35).**
  Reverse the prepared capped-contribution example through normal Admin Review. Its
  before/after progress and affected later contributions match the supplied expectations.
  Inspect its audit explanation and representative signup-code, roster, board and shared-
  item changes already made in this walkthrough. You can tell who changed what, in which
  event, and what the result was. No hidden-event audit secrecy test, target-ID filter
  or general Audit-page overhaul is required. Atomicity and exact audit invariants use
  recorded automated evidence rather than manual database inspection.

### 6. Final review and public lifecycle states — MR-21 to MR-23

- [x] **MR-21 — Pending link and fresh completion inspection (C31, C33).** From Finalize,
  follow Pending submissions: Review opens the correct event and Pending filter. Inspect
  a completed team's time, then use the other Admin session to make the supplied review
  change affecting completion/rank. The old finalization attempt is refused and affected
  inspection needs repeating. Reload, inspect current facts and finalize successfully.
  Previous official history stays readable. Complete/incomplete/complete, unaffected-team
  and concurrency permutations are already covered by executable evidence.
- [x] **MR-22 — Unfinalize does not reopen submissions (C32).** Unfinalize the prepared
  early-finalized event whose former cutoff is still future. A new review cycle opens,
  but uploads stay closed. Only the separate valid, explicit Reopen submissions action
  restores eligibility. The screen and messages should make that distinction clear.
- [x] **MR-23 — Cancelled-event destinations (C37).** Follow the supplied old Board,
  Teams, team and tile links of the cancelled published event. They show the consistent
  cancellation state without private reasons or actionable competitive controls. The
  prepared finalized/archived comparison remains readable. Managed-image access details
  and direct-route coverage are supplied by the recorded HTTP checks.

### 7. Public presentation — MR-24 to MR-25

- [x] **MR-24 — Font loading in normal use (C41).** Open landing and Signup with a cold
  load, then revisit them warm. Judge whether noticeable font swapping is reduced and
  the intended typography/layout remains intact. View the supplied blocked-font scenario:
  text and controls remain readable. Some swap or slower paint under throttling is not
  automatically a defect: the measured result did not promise universal faster rendering.
- [x] **MR-25 — Existing public interactions remain familiar (T01, T02, T03, T04).**
  During the same pass, check Board/team/tile navigation, evidence opening/zoom/closing,
  Recent Drops, Leaderboards and a transient status message. The interactions should
  retain the approved behavior and composition. These tickets repaired obsolete tests;
  do not invent new controls or redesign expectations to satisfy an old test.

### Evidence acknowledgment — MR-26

- [x] **MR-26 — Accept the recorded internal checks for this candidate.** The delivery
  worker provides a short applicable-evidence summary: authorization, transaction rollback,
  concurrency, persistence/migrations, immutable IDs/history, session semantics and test
  repairs. No unresolved required result may be hidden behind a passing test count. The
  user accepts this evidence alongside the observed journeys; they do not rerun faults,
  inspect SQL, write tests or approve implementation details line by line. Combined
  integration gaps must already be resolved or explicitly accepted as named limitations.

### Results and coverage

User explicitly accepts skipped CSV MR-09 on2026-09-14; accepted manual-test waiver, not claimed executed. All ticket manual acceptance is now observed or explicitly waived.

User reports Discord checks1–3 passed on selected branch using prepared DB/7131:
linking, ordinary Discord sign-in to existing account and updated Last login. User lacks
a second Discord account and explicitly requests all Discord checks marked pass. Replacement
check4 accepted by explicit manual-test waiver using existing passing automated/review
evidence; not claimed executed with a second identity. C06/C07 are Done. MR-03 accepted.
No further provider testing, review or follow-up required for these tickets.
Ledger:45 Done,1 Awaiting manual acceptance (C13 skipped CSV),5 Closed — no change,
2 Deferred (C40/D05). This approval concerns Discord; it does not silently waive the
separately skipped CSV check. Local commit5354a34 and preserved announcement overlay
unchanged; no push/deploy or cleanup authorized by this acceptance.

User explicitly approved closing chat steps 13, 14 and 15 (MR-12, MR-13 and
MR-14) without further manual testing: "just pass 13 14 15" and "These are likely
never gonna be used anyway". Record accepted manual-test waiver and reliance on existing
passing executable/integration/review evidence, not a claim these manual actions ran.
This clears C11/C17/C18/C19/C25/C38. No further fixture preparation or testing is needed
for those checks. Ledger: 43 Done, 3 Awaiting manual acceptance (C06/C07 Discord;
C13 CSV), 5 Closed — no change, 2 Deferred. All prior accepted observations and
integration evidence remain unchanged; no publication/packaging authority is implied.

User checkpoint — 2026-09-14, second walkthrough response. Chat step 12/MR-11
passed; steps 16–25/MR-15,16,17,19,20,21,22,23,24,25 passed; step 26/MR-26 accepted
recorded internal evidence. Earlier nine passes remain recorded. Chat step 13/MR-12:
user hit replacement instructions ambiguity and explicitly declined further manual effort.
Existing signup should be selected from "Waiting-list participant (optional)", not added
again through "Internal owner username"; no production failure established from this
report. Chat step 14/MR-13: user reports other teams also need emergency credentials
and declines setup; guide/fixture readiness was incomplete, not a passed manual scenario.
Chat step 15/MR-14: "Manual approval example" was a requested new title, not a seeded
item; planner clarified the misleading guide. This check remains unexecuted/unaccepted.
Discord MR-03 remains pending later configuration; CSV MR-09 remains skipped. No
additional manual effort on the declined checks, fixture expansion, remediation, workers,
new tests or review is dispatched. Do not infer a passing manual result for skipped cases.
C37 clarification: existing Board/Teams/team/tile routes were retained; ticket added a
shared generic cancellation display/guards. Ticket-review event records are disposable
fixture data created for this walkthrough, not new product pages.

Current ledger: 37 Done (31 newly accepted plus the existing six), 9 Awaiting manual
acceptance (C06/C07/C11/C13/C17/C18/C19/C25/C38), 5 Closed — no change, 2 Deferred.
C17 retains its unobserved departure-notification portion despite passing MR-11.
Skipped/unexecuted portions remain named acceptance limitations; no code failure is
inferred. Page-specific observed acceptance is recorded in UI_PAGE_MATRIX. Integration
candidate/frozen manifest remains unchanged. Packaging/publication is not authorized.

User checkpoint — 2026-09-14, chat guide steps 1–11 (chat step order differs from MR IDs):
steps 1, 2, 4, 5, 6, 7, 8, 9 and 10 passed, mapping respectively to MR-01, MR-02,
MR-04, MR-18, MR-05, MR-06, MR-07, MR-08 and MR-10. User described step 4 as
"a little funky UI but functionality works" and step 6 as "a bit weird UI"; record
those observations without inventing a defect or authorizing UI redesign. Step 3/MR-03
cannot currently run; user expects real Discord testing later with suitable configuration.
Step 11/MR-09 CSV is optional for continuation and may be skipped; manual acceptance
remains open, not waived. The original prepared preformed roster supports later checks
without applying the CSV. Continue at chat step 12/MR-11. MR-26 remains unacknowledged;
no group-wide or untested-state acceptance is inferred.

Record results against the exact candidate above; reported passes are checked. A group
is accepted only when its applicable checks and named limitations are accepted; an
unchecked/failed check is not silently waived. The planner records ticket outcomes and
updates page-specific approvals in UI_PAGE_MATRIX only after actual user acceptance.
This checklist itself grants no approval, integration, commit or deployment authority.

| Group | Ticket coverage | User result / issue IDs |
| --- | --- | --- |
| Accounts/signup | C05, C06, C07, C08, C09, C39 | MR-01/02/04 passed; MR-03 accepted after real provider checks, replacement explicitly waived; UI observation on MR-04 |
| Event setup | C01, C02, C03, C04, C29 | MR-05/06/07/08 passed; UI observation on MR-05 |
| Draft/roster/start | C10, C11, C12, C13, C14, C15, C16, C17, C18, C38 | MR-10/11 passed; MR-09/12/13 accepted by explicit manual-test waiver |
| Catalogue/board | C19, C20, C21, C23, C24, C25 | MR-15/16/17 passed; MR-14 accepted by explicit manual-test waiver |
| Submission/review/audit | C04, C15, C22, C24, C28, C30, C33, C35 | MR-18/19/20 passed |
| Final review/lifecycle | C31, C32, C33, C37 | MR-21/22/23 passed |
| Public presentation/test repairs | C41, T01, T02, T03, T04 | MR-24/25 passed |
| Internal checks and candidate integration evidence | Applicable evidence across all groups; MR-26 | MR-26 accepted |

**Excluded:** C26/C27/C34/C36/D03 are closed without new implementation; C40/D05 are
deferred outside this batch. D01/D02/D04/D06/D07/T05 are already Done. No C05/C09
old-data scan/repair, Running/Paused C11 departure extension, WOM Stats feature or
separate announcement acceptance is added here. Previously accepted behavior remains
protected, but does not create another whole-site manual audit.

## How to use this checklist

- Reset/regenerate the documented Development scenarios before a slice when its data assumptions require it.
- Record the application revision, date, browser/device, scenario, and account used.
- Check the normal path plus the listed permission, validation, failure, narrow/mobile, keyboard, and no-JavaScript cases that apply.
- A checked item means the observed result matched the written expected result. Record defects beside the failed item; do not rewrite the expectation to match a bug.
- Keep automated-test results in the normal test output. This file records only human-verifiable behavior and visual/interaction acceptance.
- The manual tester is not expected to exercise both success and failure for every mutation. Implementation and review must inventory the slice's mutation endpoints, apply the shared feedback contract consistently, and cover the common mechanism plus high-risk exceptions with focused automated tests. Manual acceptance samples representative successful, validation, permission, and conflict paths.

## Verification record template

- Revision:
- Date:
- Tester:
- Browser/device:
- Seed/scenario:
- Account/role:
- Result: Not run / Passed / Failed / Blocked
- Notes or defect links:

## Drop announcements and NEW tracking — user walkthrough, 2026-09-12

DELIVERY_PLAN `Drop announcements and NEW tracking` owns expected behaviour. The user
waived the ordinary agent browser preflight on 2026-09-12 and will perform this
walkthrough; retain automated checks for the harder authorization, persistence,
concurrency and timing-race boundaries. Source review and automated checks are not
visual approval. Unchecked items below remain unverified by the user.
Do not run a production reset or add a separate load-test gate.

Fixture basis: `test-15-dkl-live` (`Vinterbingo 2026`), first team `Touch kids, not grass`.
After the 2026-09-13 seed update, a reset creates 15 pending submissions across varied
tiles/items. Superior Slayer, the linked Araxxor resubmission, Vorkath (Dragonbone
necklace) and Alchemical Hydra (Hydra's claw) each complete their tile when approved
from the reset state. Phosani's Nightmare (Harmonised orb) adds partial progress.
These completion/partial outcomes passed a focused real approval-service test.
The running manual-test database is not automatically reset by this code change.
Use `SeedEvidenceParticipant` for an ordinary participant, `SeedEvidenceCaptain` for
submission and `SeedAdminTwo` for Review; Development-only credentials are defined in
`DevelopmentScenarioSeeder` and the existing evidence checklist below. The reset creates
one pending non-drop submission on `Superior Slayer`, target/claimed contribution 4,
with note `DA-07 pending non-drop completion: Superior Slayer.` Its manual objective
uses the Imbued heart/Eternal gem/Mist battlestaff/Dust battlestaff pool. This supplies
the controlled completing/non-drop approval for DA-07. Executable reset fixture test
passes. Before preflight was stopped, real Admin -> Review -> Superior Slayer Details
-> Approve succeeded with contribution 4 in the isolated runtime; that runtime's
controlled submission is therefore already approved. A fresh Development reset
recreates it as pending. The remaining ordinary browser journeys are left to the user.

- [ ] DA-01: From Events, enter the eligible Live event as a genuine participant.
  Approve pending evidence through Admin Review in a separate session. Observe item
  artwork/title and live NEW navigation badge without a board reload or focus theft.
- [ ] DA-02: Observe the coral top line drain for ten seconds. Focus/click inside:
  line resets and holds full; tab among controls: remains full; click/tab outside:
  fresh ten seconds. Check hover, compact/expand, keyboard/touch and reduced motion.
- [ ] DA-03: Approve several entries within two minutes; current selection and timer
  stay stable. Switch manually; compact and manually expand to retain selection.
  After cooldown automatic expansion for a newer approval selects the newest entry. Repeat
  while interacting, with an active submission drawer and a visible submission result.
- [ ] DA-04: Navigate Board, Drops, Teams, landing, Admin and back. Admin hides the
  banner; navigation retains it. Close/reopen within cooldown: compact. After an
  offline interval beyond cooldown, return without new approvals: no automatic
  expansion. With outstanding approvals newer than the last automatic expansion,
  return to one expanded queue with newest selected. Navigation uses the same rule.
  A second device/session respects cooldown, announcement boundary and acknowledgements.
- [ ] DA-05: Dismiss: queued announcements disappear but entry/nav NEW remains.
  GO TO DROP on a later announcement from landing opens that exact existing popup,
  including an entry outside the initial loaded/filter result. Only that view clears
  both states; remaining queue compacts. Direct popup opening has the same effect.
- [ ] DA-06: Use Drops filters and load-more, scroll into the feed and open a popup.
  Further approvals update matching entries and unfiltered NEW without losing reading
  position or popup. CLEAR ALL NEW clears both states across filters; later approvals
  still appear. Simply visiting Drops never clears NEW.
- [ ] DA-07: Approve non-drop progress and a completing contribution: tile title/art
  for non-drop, green Tile completed only for the completing approval. Reverse it
  through Review: update disappears; normal linked-correction approval is new.
- [ ] DA-08: Repeat representative presentation in light/dark and desktop/narrow.
  Switch English/Danish: announcement status, compact count, position counter,
  actions and accessible labels use the chosen language, including related NEW/clear wording.
  Verify anonymous/nonparticipant/Admin-only accounts receive no participant banner;
  recover a brief disconnection without duplicate/lost updates. Failed popup opening
  must not acknowledge viewing (automated failure-seam proof may supply this evidence).
- [ ] DA-09: End the event with pending final reviews. Approve through Admin Review:
  participants still receive updates. Finalize through the existing Admin workflow:
  banner, all entry NEW marks and navigation NEW disappear for every account. An
  offline/second session returns to the same cleared state. Existing drop entries and
  evidence remain readable. Automated checks cover stale-update/finalization races.

## Board Create/Edit tile popup behavior — authorized 2026-09-08

Current composition/flow accepted; DELIVERY_PLAN section 14 owns behavior and
revealed-state changes. Start Admin -> event -> Board -> Create tile or an editable
tile. No agent production-data mutations are required for the implementation pass.

- [ ] Create/Edit remains the same layout. Initial Edit is clean; name, objective,
  added/removed objective and file-only edits all receive correct Discard/Keep
  protection on Close/Cancel/Escape/outside, in-page navigation or tile switching.
  Browser Back/reload/leave uses the native unsaved-change prompt; staying retains
  the editor. No custom Back-closes-popup behavior is introduced.
- [ ] Objective/tile removal confirmation has compact complete controls, Cancel
  first, visible consequence, focus/reveal behavior and restored initiating action
  after cancellation. Escape dismisses the topmost confirmation first.
- [ ] Pending save blocks duplicates/dismissal; failed save retains all inputs and
  provides usable feedback/recovery. Narrow/resize preserves the same editor state.
- [ ] Success returns to fresh canvas/statistics/actions and gives one truthful
  toast. Successful save with warning or failed refresh does not invite a duplicate
  save or misreport the mutation as failed.

## Teams/Draft roster corrections — 2026-09-08

User approved the roster popup on 2026-09-08 after the removal-control corrections.
Individual unchecked scenarios below are not claims of separately witnessed execution.
Use Admin -> event -> Teams/Draft -> an existing
team's roster, in a lifecycle state that already permits manual additions.

- [ ] Add an available participant with Captain selected: one save adds them as
  Captain, and the roster no longer claims that a Captain must be assigned.
- [ ] Check Participant remains the default and Co-captain is selectable; existing
  role editing after addition remains usable.
- [ ] In a pre-formed team, the manual external-member addition also offers the
  role choice. A Captain without website access receives accurate access guidance,
  rather than a misleading missing-Captain warning or false access-ready state.
- [ ] Check the new fields, readiness text and outcome toast in the popup at the
  usual desktop/narrow sizes; the established composition should remain intact.

Follow-up guard completion is implemented with focused tests/build/source review
clear after the missed outside-click and dirty/pending paths were identified.
New guard states are ready for manual check; previously accepted visuals remain.

- [ ] Change a roster field, then use Close, outside click or Back: Keep preserves
  the value; Discard restores the baseline and completes the requested dismissal.
- [ ] Check switching teams or saving another roster form cannot silently discard
  unrelated edits; the compact discard state is readable and keyboard reachable.
- [ ] During a save, duplicate actions and dismissal are blocked; after a failure,
  entered values and usable recovery remain. Confirm one accurate result toast.

## Teams/Draft Add team popup behavior — 2026-09-08

Start Admin -> event -> Teams/Draft -> Add team where existing lifecycle controls
permit creation. This pass preserves the current appearance; team roster is separate.
Implementation and source-only review complete; focused tests/build pass. User
approves on 2026-09-08, accepting minor behavior differences without remediation.
No persisted team creation tested by the agent; unchecked individual steps below
are not claims of manual execution.

- [ ] Name, Formation and Affiliation remain in their existing layout. Resize
  with values entered; the modal and values remain, with usable Close/scroll.
- [ ] Dirty Close/Escape/outside-click offers Keep editing/Discard; Keep preserves
  fields. In finalized pre-formed correction, Cancel/Escape restores the inline
  review trigger and focus without losing entered fields.
- [ ] An allowed creation returns to fresh Draft state with one accurate outcome
  toast. Failed creation retains fields and usable recovery; a completed creation
  cannot be submitted again merely because parent refresh failed.

Use focused mocked execution evidence for pending/failure cases; no agent-created
database records. User provides final visual acceptance of this Add-only pass.

## Add Participant popup behavior — 2026-09-08

Use Admin -> event Participants for an existing SignupOpen/SignupClosed event
whose draft is unlocked. Visual approval covers this Add popup only.
User visually approved the current Add popup on 2026-09-08. Focused mocked-client
checks and source-only review clear; real persisted creation was not exercised by
the agent. Individual unchecked journeys below are not claims of manual execution.

- [ ] Open Add participant at desktop and narrow widths; resize while entering
  values. The same popup remains, with usable scrolling and reachable Close.
- [ ] Edit a field, then Close/Escape/Back. Keep editing preserves values and
  meaningful focus; confirmed discard returns to the existing Participants context.
- [ ] Check owner search, required/optional fields, action hierarchy and any
  failure message in the current theme. No duplicated action beside confirmation.
- [ ] After an authorized creation, check one accurate outcome notice (including
  waiting-list wording where applicable), fresh Participants data and retained
  filter/sort context; Add and Manage remain reachable. Use focused mocked
  execution evidence for pending/transport failure without creating test records.

## Catalogue Add/Edit popup behavior — 2026-09-08

Implementation, source-only review and targeted mocked-write browser checks
complete. Existing general appearance is preserved; awaiting user acceptance.

- [ ] Add/Edit and expanded drop forms retain edits through failed saves and
  cancelled discard. Switching forms/actions cannot silently erase other inputs.
- [ ] Deactivate/delete/duplicate-item decisions appear inline in view, use readable
  body text, and restore the trigger or original typed input on cancellation.
- [ ] Successful save updates the catalogue behind the popup; close returns with
  search/filter/scroll preserved. Reload Edit -> close -> Add remains a popup.
- [ ] Narrow resize preserves the same editor, values and pending action; deliberate
  standalone routes retain their action/validation behavior.

## Accounts Create/Manage behavior — 2026-09-08

User manually approved Create and Manage on 2026-09-08 after confirmation
auto-scroll correction. Source review and focused execution evidence are recorded
in CURRENT_STATUS; the scenarios below remain a regression reference. Actual
security mutations were not part of the automated preview checks.

- [ ] Manage website and emergency accounts: action buttons reveal one compact
  inline confirmation, with readable messages/reason fields and Cancel first.
  Cancel/Escape closes that confirmation and keeps Manage open.
- [ ] Create: type a username, change event scope or close/Back, and cancel the
  discard choice. Entered details remain; failed saves retain values. Resizing
  keeps the same popup, including while an action is pending.
- [ ] Complete a permitted action, then close: the directory shows current state
  and retains filters/page position. A generated one-time link remains readable
  in Manage; the directory never exposes its secret value.
- [ ] Refresh with Manage open, close, then open Create/another Manage. Both remain
  popups. Check action-revealed wording and narrow-width controls visually.

## Participant-management popup — 2026-09-08

Implementation and focused review/preview verification complete; awaiting user
visual acceptance. Use a local Development Admin, an event with an existing
participant and the real Participants -> Edit link. Use a disposable participant
for mutations. Do not rerun unrelated signup journeys or reset existing user data.

- [ ] Save shares the final answer row when space allows; bottom action buttons
  share a right-aligned wrapping row while heading/body remain left-aligned.
- [ ] Save in Participant or Signup form editor, then close: the parent shows
  current data with filters/sort/scroll preserved.
- [ ] Summary labels/values read together, with all prior facts present. Signup
  answers, private notes and participant actions follow in a compact clear order.
  Long names/help text wrap without hiding controls; notes do not repeat the same
  help in both text and placeholder.
- [ ] Change an answer or note, attempt to close, cancel discard and save. Values
  remain and the save updates the same workspace. Changing payment or saving notes
  while answers are dirty must not silently discard those answers.
- [ ] Open ownership-transfer or removal/restoration confirmation where available:
  the intended action and consequence stay explicit, Cancel/Escape returns to the
  editor, and no unrelated action is triggered. Actual lifecycle mutations use
  disposable records and existing permissions/confirmation requirements.
- [ ] Open at narrow width, resize with edits in progress, scroll and close/Back.
  The same full-screen modal remains usable and Close stays visible; cancelling
  discard preserves edits. Desktop returns to the compact centered presentation.
- [ ] Reopen through Participants, reload while Manage is open, close it, then
  open Edit signup form: it must remain a popup. Check the direct editor route.
  Context/filter/scroll recovery remains usable. Spot-check accepted Signup Questions
  popup and its code settings after common behavior is shared.

## Admin popup pilot — Signup Questions (2026-09-07)

Implementation, scoped browser proof and independent source/visual review clear;
user manual acceptance received 2026-09-08 after trying the completed pilot.
This acceptance does not claim every checklist item was separately executed.
Use a local Development Admin and an
editable event reached through Admin Events -> Participants -> Signup form. Use
throwaway questions for mutations; do not reset existing user data. Automated fault
injection and repeated-save checks belong to focused browser proof, not a repeated
manual workload. Keep current public signup behavior intact.

- [ ] Open Signup form and inspect light/dark desktop: popup title, section titles,
  fields, buttons and help follow the Admin hierarchy; deletion confirmation is
  compact body text and does not dwarf its question. Close stays reachable while
  scrolling a long editor.
- [ ] Add/edit a throwaway question and save: editor stays open, updated question
  and parent count appear, one success notice, another edit is immediately usable.
- [ ] Change text, attempt to close, cancel discard, then save: changes survive.
  An unchanged close needs no confirmation; closing and reopening returns cleanly
  to the Participants context. Browser Back follows the same discard decision.
- [ ] Open Delete question on a short-text question: only question/answer deletion
  is described. Cancel/Escape restores its trigger. An Account question also
  describes event-account removal while keeping saved My accounts. Confirm only
  on a throwaway question and verify its row/count update.
- [ ] In Signup code, an off checkbox hides the code field. Checking it reveals a
  required new-code field when no code exists, without saving. Save code settings
  applies the change. With an existing code, Replacement code is optional and blank
  keeps the current code. Turning protection off hides the field and takes effect
  only after saving. A failed save retains the checkbox and entered code.
- [ ] At narrow width, open Signup form as a full-screen popup. Resize an open
  editor both ways: values, URL and pending state survive. Check readable controls,
  no horizontal clipping, usable Close/Back and validation; direct standalone URLs
  remain usable.
  Keyboard focus remains visible; test one localized EN/DA rendering.

## Slice 5 Pass 5.2A/5.2B — teams and pre-formed external roster CSV

Historical note: Slice 5 acceptance used `TEST 52 — Team and CSV setup`, which has now been retired rather than retained as stale setup data. The current Development reset seeds the Slice 6 baselines documented below.

The retained account/catalogue foundations remain preserved by the real PostgreSQL double-reset regression. The current exact fixture contract is TEST 13, TEST 15, and TEST 62.

- [x] **S5-01 — Passed.** Create Drafted and Pre-formed teams; duplicate creation gives safe feedback with no additional team or audit.
- [x] Derived setup distribution updates for drafted-team changes, including uneven remainders.
- [x] **S5-02 — Passed.** Managed PNG/JPEG/WebP image replacement/removal and retired-asset protection passed.
- [x] **S5-03 — Passed.** Saved managed images remain visibly attached after reload.
- [x] Existing participant Pre-formed roster assignment removes the participant from the draft pool and rejects duplicates.
- [x] **S5-04 — Passed.** Unowned external roster members retain correct EHB/informational semantics without inferred ownership or captain role.
- [x] **S5-05 — Passed.** Reserved external-account retries give safe feedback with no residue.
- [x] Pre-event roster remove/move history, Cancel-draft restoration, and post-start metadata lock passed.
- [x] **S5-06 — Passed.** Numbers semicolon CSV preview/apply and canonical comma CSV passed.
- [x] **S5-07 — Automated/covered and approved.** Multi-row CSV with optional additional `Account` columns and Playing/Informational semantics passed.
- [x] Invalid CSV values, duplicates, reservations, stale/replayed previews, and Drafted-team CSV exclusion passed.

## Slice 1 — Website accounts, authentication, Super Admin, recovery, and disabling

### Executable manual-test environment

Use a disposable Development database, never a retained-data copy. Start PostgreSQL with `docker compose up -d postgres`, then run the app with the Development connection string from `src/Bingo.Web/appsettings.json`. Configure a disposable Discord application through user secrets (do not commit them): `DiscordAuthentication:ClientId`, `DiscordAuthentication:ClientSecret`, and its redirect URI `https://localhost:7131/Account/DiscordCallback`; the exact HTTPS URL printed by `dotnet run` takes precedence. The app deliberately leaves Discord unavailable when these values are absent.

Before running the cases, apply the migration and catalogue snapshot, provision one intended owner through the explicit operator command, and create the remaining roles through the UI:

```bash
dotnet user-secrets set --project src/Bingo.Web "Slice1:BootstrapOwnerPassword" "<10+-character-disposable-password>"
dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot
dotnet run --project src/Bingo.Web -- --slice1-bootstrap-owner --username slice1-owner --confirm-username slice1-owner
dotnet run --project src/Bingo.Web -- --reset-test-data
dotnet run --project src/Bingo.Web
```

The bootstrap command works only with an empty account table and creates the sole Super Admin; its password is read only from user secrets. The recovery command only promotes an existing active website account. The seed command creates the labelled workflow scenarios and prints the secondary Admin and captain credentials: `SeedAdminTwo` / `SeedAdmin!1234`, and generated captain usernames / `SeedCaptain!1234`. Sign in as `slice1-owner` with the user-secret password. Create a normal User and disabled User through Discord onboarding when credentials are available; create an emergency credential under `/Admin/Accounts` for a real seeded team, generate its setup link, choose a password, then enable it. Use two independent browser profiles for stale-session checks. The exact local URLs are `https://localhost:7131/Account/Login`, `/Account/Settings`, `/Admin/Accounts`, `/Admin/Accounts/Transfer`, `/Admin/Audit`, and `/Captain`; HTTP is `http://localhost:5164`. A retained legacy-database migration rehearsal must use a separate copied database and begin with `--slice1-migration-preflight --slice1-owner <existing-admin-id-or-username>` before applying the migration.

**Prerequisites:** Document the revision, migrated/reset Development database, selected owner account, Discord test application/callback, one User, two Admins, one disabled User, one emergency credential, and two separate browser sessions.

- [ ] **S1-01** — Migration preflight identifies every legacy Admin/Captain, preserves account IDs, flags ambiguous usernames/scopes, and requires an explicit initial owner.
- [ ] **S1-02** — The selected existing account becomes the sole Super Admin; another Admin remains Admin; both retained password hashes still authenticate.
- [ ] **S1-03** — Every legacy Captain becomes a disabled emergency credential with the correct event/team/participant role scope, and no free-text participant Discord name is linked.
- [ ] **S1-04** — Clean-production rehearsal applies migrations to an empty database, initializes the reviewed catalogue snapshot, creates no Development/test account or workflow data, and provisions exactly one intended Super Admin through controlled operator setup.
- [ ] **S1-05** — First-time Discord authentication reaches onboarding; cancelling, provider failure, or 15-minute onboarding-state expiry creates no account.
- [ ] **S1-06** — Successful onboarding atomically creates the website account, unique public/password-login username, password, initial OSRS character, preferred link, and completed state.
- [ ] **S1-07** — Password creation/change rejects fewer than 10 characters, accepts a valid 10-character password and long printable passphrase, and imposes no character-class composition rule.
- [ ] **S1-08** — A public-username collision retains every other entered value and neither takes over nor merges the existing account.
- [ ] **S1-09** — Returning Discord and public-username/password login open the same website account; Discord-server membership is never requested or required.
- [ ] **S1-10** — Incorrect username/password uses generic feedback; identifier and network throttles activate independently without revealing account existence.
- [ ] **S1-11** — Non-persistent login ends with the browser session or after its 12-hour ticket maximum; **Remember me** persists for no more than 30 days and cannot slide indefinitely.
- [ ] **S1-12** — Password change reissues the intended current session, rejects prior password sessions, and does not unnecessarily invalidate a separate Discord-authenticated session.
- [ ] **S1-13** — A non-persistent password session expires within 12 hours; Remember me expires within 30 days from sign-in even after continued activity; identifier and network failure limits work independently while exposing only generic login feedback.
- [ ] **S1-14** — Forgot password is a static contact-admin page with no username/account lookup form.
- [ ] **S1-15** — Admin can generate a raw reset link once for a User but cannot see/set the replacement password; ordinary Admin cannot generate one for an Admin or Super Admin.
- [ ] **S1-16** — Super Admin can generate an Admin reset link but has no in-product self-reset link; links expire after 60 minutes, and expired, used, and superseded links all fail generically.
- [ ] **S1-17** — Reset completion changes the password, consumes the link, rejects prior password sessions, and records no secret in audit history.
- [ ] **S1-18** — Linked account can unlink after current-password confirmation and remains usable through a reissued password session with all roles/history intact.
- [ ] **S1-19** — Unlinked account can link Discord and linked account can replace Discord directly; an already-used Discord ID fails without mutating either account.
- [ ] **S1-20** — Link/unlink/replace invalidates other sessions, preserves the website account ID and all history, and warns—but does not block—the Super Admin before unlink.
- [ ] **S1-21** — Only Super Admin sees and can execute Admin grant/revoke; the confirmation names the target/before-after role, requires no typed username/reason, emergency credentials are ineligible, and revoke returns the target to User without changing event records.
- [ ] **S1-22** — Grant/revoke rejects the target's prior sessions with accurate access-changed feedback; Super-Admin transfer requires the owner's password plus typed destination username and invalidates affected sessions.
- [ ] **S1-23** — Super-Admin transfer changes recipient to Super Admin and former owner to Admin atomically; stale/concurrent transfer cannot produce zero or two owners.
- [ ] **S1-24** — Admin can disable a User but not an Admin, self, or Super Admin; Super Admin can disable/restore another Admin but not self/current owner.
- [ ] **S1-25** — Disable requires a reason and immediately rejects all target sessions; it retains username/Discord reservations and every event record. Restore needs confirmation but no reason and does not restore expired event authority.
- [ ] **S1-26** — No website-account merge/permanent-delete action exists; clean Development/production reset remains the test-identity cleanup path.
- [ ] **S1-27** — Admin creates separate disabled emergency credentials for one team, each with a globally unique login username; a 60-minute single-use setup/reset link lets the captain choose the password without exposing it to the Admin.
- [ ] **S1-28** — Emergency password setup does not enable access; explicit enablement works only after setup, multiple individual credentials can coexist, and submission cutoff disables them.
- [ ] **S1-29** — Emergency credentials remain usable through the configured submission grace and are durably disabled with audit history at submission cutoff; an Admin must explicitly re-enable them, which is also audited.
- [ ] **S1-30** — One Accounts area keeps website accounts and emergency credentials visibly separate; both search/filter server-side and paginate at 25 rows without losing active filters.
- [ ] **S1-31** — Website rows show the approved summary, and details show every linked OSRS character, non-authoritative Discord display name, participation, role history, and disable history without password/OAuth/setup/reset-token data or unnecessary raw Discord IDs.
- [ ] **S1-32** — Emergency rows show username, event/team, setup/enabled/cutoff state, last login, and only actions authorized for the current Admin/Super Admin.
- [ ] **S1-33** — Audit table shows exactly 25 newest rows per page, retains event/actor/action/entity/date filters across navigation, reaches older history, renders structured changes readably, and offers no edit/delete/export.
- [ ] **S1-34** — Routine website login updates Last login without a main-audit row; failed/throttled attempts reach security logs, while emergency login and approved security-sensitive mutations create durable audit entries.
- [ ] **S1-35** — Grant/revoke/restore notifies the affected account; disablement shows neutral contact-an-admin guidance without exposing its private reason.
- [ ] **S1-36** — Operator recovery has no web action; its owner-reset and explicit ownership-transfer modes enforce identifiers/confirmation and create system audit history.
- [ ] **S1-37** — Every mutation has accurate success/failure/conflict feedback through both enhanced and ordinary no-JavaScript form submission.
- [ ] **S1-38** — Every new Slice 1 page uses the approved shared UI rules and passes desktop, intermediate, narrow/mobile, keyboard, focus, validation-summary, feedback, and no-JavaScript checks.
- [ ] **S1-39** — Login, onboarding, normal account settings/recovery, and emergency-captain setup/login show complete English and Danish headings, controls, help, validation, confirmations, empty/error/permission states, and feedback through the existing language switch; Admin/Super-Admin-only pages may remain English-only.

## Slice 2 — My accounts, OSRS characters, event assignments, and identity migration

### Executable manual-test environment

Use a disposable migrated Development database and a normal website account completed through Discord onboarding. Keep a second normal website account available for shared-character checks, and retain one seeded event with the existing fixed signup/edit form plus one event that displays the draft and public board projections. Do not use a production or retained-data rehearsal database for manual acceptance.

Start PostgreSQL and the application with the documented Development settings:

```bash
docker compose up -d postgres
dotnet run --project src/Bingo.Web -- --reset-test-data
dotnet run --project src/Bingo.Web
```

The exact HTTPS URL printed by `dotnet run` takes precedence. The Slice 2 routes are `/Account/Onboarding`, `/Account/MyAccounts`, `/Account/Settings`, the existing `/Events/{slug}/Signup` and private `/Events/{slug}/Signup/Edit/{token}` compatibility routes, `/Admin/Events/Draft`, and the existing public board/team/tile routes. Use two browser profiles when checking two website accounts. Record the event slug and participant used for projection checks.

Automated acceptance cases remain listed here so their IDs match the [archived Slice 2 manual results](docs/archive/manual-evidence/SLICE_2_MANUAL_TEST_RESULTS.pre-consolidation-2026-08-15.md); a row marked `Automated` there does not need manual repetition. Manual acceptance should concentrate on the end-to-end, visual, responsive, keyboard, localization, and protected-surface cases.

- [ ] **S2-01** — A clean PostgreSQL database migrates to the current model, and a representative retained Slice 1 database migrates deterministically without losing existing account/character links, participant IDs, playing EHB, informational characters, or unowned participant records.
- [ ] **S2-02** — OSRS-character matching is case-insensitive, one website account cannot duplicate its own link, and two website accounts may deliberately link the same trusted or borrowed character.
- [ ] **S2-03** — My accounts retains one account/character history row across unlink/reactivation, keeps personal label/order/saved EHB on that link, and permits at most one active preferred character.
- [ ] **S2-04** — An event permits only one current assignment for a normalized OSRS character; playing assignments require an event EHB snapshot while informational assignments have no EHB.
- [ ] **S2-05** — Existing signup creation, private-link editing, CSV import, and Admin correction store and display the primary playing character, optional informational character, and event EHB through event assignments rather than removed participant fields.
- [ ] **S2-06** — Imported and external participants remain valid without a website-account owner; ownership is never guessed from website username, Discord text, or an OSRS-character link.
- [ ] **S2-07** — Discord onboarding clearly collects separate website-username and first-OSRS-character values, creates the preferred My accounts link atomically, and retains entered values after a field-specific collision.
- [ ] **S2-08** — My accounts supports the empty state, add/reactivate, label, saved-EHB edit, ordering, and preferred selection while preventing one account from mutating another account's links.
- [ ] **S2-09** — Saved EHB belongs to one website-account/character link and does not change another borrower's default or an already submitted event snapshot.
- [ ] **S2-10** — Unlinking a character used by an upcoming or live registration requires an understandable confirmation and clearly explains that the event registration remains.
- [ ] **S2-11** — A spelling correction preserves link metadata and updates only currently editable account-owned assignments; a conflict names the affected event safely and rolls back the whole correction.
- [ ] **S2-12** — Account settings clearly distinguishes website identity from OSRS characters and Discord; a correct-password username change updates the login/display username, while wrong-password and case-insensitive collision attempts leave it unchanged.
- [ ] **S2-13** — A successful website-username change refreshes the current session without changing Remember-me expiry, invalidating unrelated sessions, or changing My accounts, event assignments, teams, evidence, roles, or event-facing names.
- [ ] **S2-14** — Emergency credentials cannot open My accounts or use website-username rename, and their reserved login names still block conflicting normal website usernames.
- [x] **S2-15 — Superseded by Slice 4.** Private signup edit tokens and fixed signup/edit fields were intentionally removed; authenticated ownership is the current path.
- [ ] **S2-16** — Active runtime code and projections no longer read or write the removed participant primary-name, normalized-name, second-name, or EHB authority; matching names in migrations and fixed-form view models are intentional compatibility.
- [ ] **S2-17** — Every User-facing Slice 2 page, label, validation message, warning, empty state, success, and safe failure is understandable in both English and Danish.
- [ ] **S2-18** — My accounts and username settings remain usable at desktop, intermediate, and narrow widths with logical keyboard order, visible focus, restored validation focus, and understandable empty/warning/conflict states.
- [ ] **S2-19** — Discord's external authorization page requires JavaScript and is outside the application's fallback boundary. Automated native-form coverage verifies the local onboarding, My accounts, website-username rename, sign-out, and new-username sign-in endpoints without requiring a disproportionate manual no-JavaScript journey.
- [ ] **S2-20** — Using representative retained participants, verify the live draft and public board/team/tile/submission routes show assignment-backed OSRS names/EHB while preserving their existing composition, Pass 12 overlay behavior, responsive route navigation, and no-JavaScript fallbacks; the board editor remains visually and functionally unchanged.

## Slice 3 — Event creation, scheduling, readiness, cancellation, archive, and current-event policy

- [ ] **S3-01** — As an Admin, open `/Admin/Events/Create` at desktop and narrow widths. The familiar five-step flow remains usable with JavaScript, and name plus Copenhagen-defaulted supported timezone create a private draft with every optional section blank.
- [ ] **S3-02** — Disable JavaScript and create the same minimal private draft. Every step's fields remain reachable in document order, the native submit redirects to its Admin event workspace, and no public signup/board/team state is exposed.
- [ ] **S3-03** — Enter an optional full setup (description, valid half-hour schedule, capacity, signup setting/question, buy-in, and board rows/columns limited to 1–8). Verify entered values remain after creation; leave a different optional section blank and verify it does not block creation.
- [ ] **S3-04** — Enter an invalid/ambiguous DST time, an invalid custom question, or an invalid board dimension outside 1–8. Verify field-specific feedback retains input and creates no event. A partial date/time manual case is not applicable to the combined calendar control.
- [ ] **S3-05** — Verify a generated private event link is editable before first public exposure, rejects invalid/colliding links safely, and does not disclose another event's details.
- [x] **S3-06** — Approved: Event identity saves redirect back to Manage with mutation feedback. Banner behavior remains covered by the recorded identity checks.
- [x] **S3-07** — Passed: a first-public timezone change previews stored instants and requires confirmation; after actual start it requires a written reason.
- [ ] **S3-08** — Use two browser sessions to submit conflicting identity changes. Verify the stale session receives safe conflict feedback, retains its attempted values for review, and does not overwrite the latest saved identity.
- [x] **S3-09** — Passed for the functional interaction boundary: native creation/identity feedback, direct-route fallback, keyboard/focus, and validation behavior. Visual review and styling are deferred to the full UI overhaul.
- [ ] **S3-10** — Confirm a normal User cannot use `/Admin/Events/Create` or an identity route, and the Admin audit trail records creation and identity updates without a signup code, upload payload, or other secret.

Pass 3.2 is approved: no-JavaScript creation passed; unset schedules show “Not set”; and board dimensions accept 1–8 while rejecting values outside that range. Visual review is deferred to the full UI overhaul.

Pass 3.3 is approved: private/open/closed schedule editing, proposed Open-now and Reopen closing behavior, capacity/waiting-list behavior, multiple non-overlapping signup windows, overlap rejection, exact back-to-back boundaries, and exact-link/unlisted signup behavior passed. Calendar appearance remains deferred to the full UI overhaul.

- [x] **S3-11 — Passed.** Create and Schedule show localized values while posting canonical `yyyy-MM-ddTHH:mm`; a future five-minute opening schedules and opens automatically without a checkbox.
- [x] **S3-12 — Passed.** Opening and closing edits preserve untouched values, and overlap feedback appears in notification history.
- [x] **S3-13 — Passed.** Postponed-start readiness updates after blocker resolution; manual Start resolves the attempt, preserves its history/configured start, records the actual start separately, and immediately renders Live controls.
- [x] **S3-14A — Passed.** The historical reset check passed; the current reset contract is TEST 13, TEST 15, and TEST 62.
- [x] **S3-14B — Passed.** Fixture-only data has no automatic public current event and one real current event is selected.
- [x] **S3-15 — Passed.** “Event discarded.” appeared exactly once on Admin Events through enhanced and no-JavaScript journeys.
- [x] **S3-16 — Passed.** Protected participant/team/access/submission/evidence history blocks discard without cleanup.
- [x] **S3-17 — Passed.** Never-public cancellation preserves private history and exposes no public event or reason.
- [x] **S3-18 — Passed.** Previously public cancellation remains generic publicly; explicitly account-owned confirmed and waiting-list cancellation notifications are accepted as Automated for Slice 3 because the retained pre-Slice-4 signup UI creates unowned participants. The private reason is excluded.
- [x] **S3-19 — Passed.** Finalized archive preserves historical public routes and results.
- [x] **S3-20 — Passed.** Archived unfinalization succeeds without a competing current event and preserves snapshots.
- [x] **S3-21 — Automated.** Competing Live/review/finalized current events block archived unfinalization atomically.
- [x] **S3-22 — Passed.** Repeated destructive actions and expired schedules produce no duplicate lifecycle effects.
- [x] **S3-23 — Passed.** No-JavaScript and enhanced lifecycle forms agree.
- [ ] **Slice 3 review remediation — terminal route retest.** As an Admin, open a populated Cancelled event and submit a stale direct POST to one configuration route (for example Identity or Board); verify readable read-only feedback, no mutation, and read-only Manage history. Also verify Finalized/Archived expose only Archive or reasoned Unfinalize on their lifecycle history route. This is the sole additional manual check before restricted re-review; it does not re-open prior passed manual cases.
- [ ] Minimal draft, manual/scheduled opening, and readiness blockers.
- [ ] Empty discard versus populated cancellation.
- [ ] Multiple non-overlapping signup windows (including back-to-back) work through exact links; overlaps and live/review/finalized singleton conflicts fail safely, while Development fixtures do not weaken Production commands.
- [ ] Finalized archive and historical-route preservation.

## Slice 4 — Signup, EHB/account questions, public signup board, capacity, and participants

### Consolidated post-4.7 review (accepted 2026-07-29)

- [x] Sign in normally and confirm signup/edit uses the event's current questions and My Accounts. There is no private-edit link, setting, or CSV import control for ordinary participants.
- [x] As an Admin before draft lock, correct a participant's answers and Regular/Alt accounts; confirm their place, timestamp, payment, and note remain unchanged. A reserved account changes nothing.
- [x] Create one participant with a selected website owner and one explicitly unowned participant while public signup is closed. Confirm normal confirmed/waiting placement and source without inferred ownership.
- [x] Transfer one participant by typing the destination username twice. Confirm the old account immediately loses My Events/confirmation access, the new account gains it, and My Accounts links, team/history, payment, and notes do not move.
- [x] Confirm payment/Admin notes stay private. Protected board, draft, team, tile, and submission routes remain available, while obsolete private-edit and ordinary-participant CSV controls are absent.
- [x] **Restricted Slice 4 remediation evidence.** Focused authenticated/PostgreSQL route tests prove retained Admin/private history versus anonymous output, stale-edit no-residue, shared withdrawal/promotion notifications, canonical Yes/No values, and My Events confirmation → roster → board → finalized/archived destinations.

- [x] **S4-01 — Passed.** The unauthenticated Login/My Accounts safe-return, normal-account, and emergency-credential boundaries passed. Changing a Regular account updates EHB to that account's saved default (or clears it) without copying the prior account's value.
- [x] **S4-02 — Passed.** Creation with a preferred Regular account/EHB and captain volunteer confirms only status, selected event accounts/EHB, and answers; website username, Discord identity, payment, Admin notes, token, and secrets remain excluded.
- [x] **S4-03 — Passed.** Open-signup editing updates answers, Regular/Alt selections, EHB, and captain volunteer without changing status/order/time/payment/Admin fields; closed signup hides Edit and rejects direct POST safely.
- [x] **S4-04 — Passed.** An unlinked current assignment remains editable without relinking; replacements require an active My Accounts link, and unavailable/duplicate replacements preserve the form and existing registration.
- [x] **S4-05 — Passed for current My events ownership/isolation and accessible destinations.** Two normal accounts see only explicitly owned records, and emergency credentials are excluded; current/history separation and event overview behavior passed.
- [x] **Lifecycle destinations — Automated.** Rendered HTTP coverage verifies confirmation, roster, published board, Finalized, and Archived destinations.
- [x] **Cancellation notifications — Automated.** Eligible owned confirmed/waiting participants receive generic notifications; withdrawn/removed/unowned participants and the private reason are excluded.
- [x] **S4-06 — Passed.** Form builder acceptance is recorded in CURRENT_STATUS.md.
- [x] **S4-07 — Passed.** Structural locking/replacement acceptance is recorded in CURRENT_STATUS.md.
- [x] **S4-08 — Passed.** Withdrawn confirmation states, open rejoin, closed read-only feedback, and stale rejoin protection passed.
- [x] **S4-09 — Passed.** Exact-link signup-table privacy, headings, positions, frozen EHB, answer history, lifecycle destinations, Admin historical access, and My Events ownership/destinations passed.
- [x] **S4-09 lifecycle presentation.** Roster-before-board and board-after-publication destinations plus Draft finalized / Board published / Event ready / Start postponed presentation were accepted.
- [x] Public/Admin answer visibility and optional historical answers.
- [x] Participant administration, payment, account correction, notification, and restoration.

- [x] **S4-10 — Passed.** Admin workspace filtering, totals, ownership presentation, and accepted smooth anchor returns passed; instant-jump review is deferred to the whole-site UI overhaul.
- [x] **S4-11 — Passed.** Repeatedly change Paid/Unpaid from participant details and confirm immediate save, `#payment` return, and accurate feedback. No-JavaScript parity is best-effort for these controls.

### Slice 4 Pass 4.6B — accepted

- [x] Correct a pre-draft participant using active Account and custom questions. Queue time/status/waiting position and payment/note remain unchanged; a reserved character rejects the whole correction.
- [x] Create one owned and one explicitly unowned internal participant while public signup is closed. Active-form validation, normal confirmed/waiting placement, source, and no inferred ownership passed.
- [x] Transfer one participant by entering the exact destination username twice. Old My Events/confirmation access is revoked, new access works, history/assignments/team remain unchanged, and both owners receive generic notifications.
- [x] Correction, creation, and transfer are rejected once the draft is locked, and no Admin-only data appears in public projections.

## Slice 5 — Teams, captain roles, external teams, and draft

**Slice 5 final acceptance (2026-07-29):** S5-01 through S5-10 and the final Public boards/Board-route retests are approved. Automated gates passed: Domain `153/153`, Application `82/82`, Browser `66/66`, Integration `188/188`, combined `489/489`; Release solution build, formatting, `git diff --check`, EF pending-model verification, and the relevant migration/reset checks passed. Slice 5 is ready for packaging/merge/push; those actions remain unperformed.

- [x] **S5-08 — Passed.** Captain assignment, controller acquisition/takeover/release, lease-guarded Start, and safe displaced-controller behavior passed.
- [x] **S5-09 — Passed.** The consolidated private-draft journey, including controller-only cancellation and retained draft history, passed.
- [x] **S5-10 — Passed.** Finalization/publication, frozen public roster privacy, reopening, ledger correction, publication history, and confirmed published Pre-formed correction without a typed reason passed.
- [x] **Final Public boards/Board-route retests — Passed.** Historical TEST 52 roster-only/public-board route acceptance passed. TEST 62 now supersedes it as the current separate-publication fixture.
- [x] Derived roster distribution, captain balancing, picks, repeated undo, pause/resume, finalization, external/pre-formed corrections, CSV scope, concurrent-admin, and permission states passed.

## Slice 6 — Catalogue, board derivation, approval snapshot, preview, and publication

**Slice 6 manual acceptance (2026-07-30):** S6-01 through S6-06 are approved, including the final mixed same-boss single-roll/multiplied-roll Zulrah-style EHB retest. Board preview functionality is accepted; its exact visual match to the established public **View bingo** Board is deferred to the UI overhaul and is not a functional blocker.

Reset Development data first. It leaves exactly TEST 13, TEST 62, and TEST 15, with valid selected catalogue-rate bindings and frozen-rate approval data. Use `TEST 13 — DKL Board` (`test-13-dkl-board`) for S6-02 through S6-04. Then use `TEST 62 — Board publication setup` (`test-62-board-publication-setup`) in this order: verify the finalized roster/approved-private board, dismiss the separate-publish handoff, publish later, inspect the public board, then run the confirmed/reasoned correction. Use `TEST 15 — DKL Live` only to compare the existing public live/progress surface.

**Final retest:** The catalogue, Board editor, frozen EHB, preview overview, finalized-roster reopening, published correction, feedback, and mixed same-boss single-roll/multiplied-roll EHB paths are approved.

- [x] **S6-01 — Catalogue administration — Passed.** Confirmed the retained catalogue, source-drop lifecycle, distinct Nid/Nid (Destroy) values, dependency-safe deletion, and absence of Wiki-import web surfaces.
- [x] **S6-02 — Draft derivation — Passed.** Confirmed live derivation, frozen approved EHB, automatic-EHB validation, manual-objective EHB, and the mixed same-boss single-roll/multiplied-roll Zulrah-style retest.
- [x] **S6-03 — Private approval — Passed.** Confirmed residue-free rejection, valid immutable approval snapshots, history-preserving unapproval, and Draft return after competitive edits.
- [x] **S6-04 — Preview — Passed.** Confirmed deterministic, side-effect-free preview behavior without Admin controls or EHB. Exact visual parity with the established public Board is deferred to the UI overhaul.
- [x] **S6-05 — Separate publication — Passed.** Confirmed the separate publish handoff, dismissal behavior, later publication, and event-start publication gate.
- [x] **S6-06 — Published correction — Passed.** Confirmed confirmation/reason, replacement snapshot and recalculation, retained history, Live correction, and readable feedback.

## Slice 7 — Live account swaps, participant navigation, and team focus

- [x] **S7-01 — participant live context and navigation.** Confirm owned participants reach the correct roster/team-board surface and can see their event, team, role, planned/current Playing account, and event-end context on the standalone route.
- [x] **S7-02 — participant self-swap.** During Live and before event end, select another current Playing account and confirm the planned change, next-whole-minute activation, feedback, and visible focus styling work without exposing another team's private state.
- [x] **S7-03 — captain/co-captain unlinked-member swap.** Confirm a Captain/Co-captain can schedule a Playing-account change only for an unlinked member of their own pre-formed team, while linked and unrelated participants remain protected.
- [x] **S7-04 — private focus authority and visibility.** Confirm Captain/Co-captain focus mutations, normal member read-only visibility, other-team privacy, and explicit Super Admin read-only inspection.
- [x] **S7-05 — concurrency and stale mutation behavior.** Covered by the accepted automated PostgreSQL gate.
- [x] **S7-06 — realtime refresh and reconnect.** Confirm authorized focus updates become visible without leaking focus data to unauthorized viewers.
- [x] **S7-07 — event-end boundary.** Confirm swap/focus mutation is unavailable at or after event end while retained focus can remain visible for historical/UI presentation.
- [x] **Final focus remediation.** Confirm overlapping tile/row/column markers coexist, one marker can be unfocused independently, Clear all affects only the current team, completed tiles cannot be focused or styled, completion clears an existing tile marker, and reversal does not restore it.

Slice 7 manual acceptance approved 2026-07-31.
- [ ] Planned/active account, whole-minute pending swap, unlimited swaps, and event-end freeze.
- [ ] Participant/captain/external-team authority.
- [ ] Team focus visibility, mutation, Super-Admin inspection opt-in, and realtime isolation.

## Slice 8 — Evidence submission, resubmission, review, and public visibility

### Development reset contract and ordering

Run `dotnet run --project src/Bingo.Web -- --reset-test-data` in Development after applying migrations. The reset creates the named workflow fixtures, including `Sommerbingo 2026` (`test-13-dkl-board`), `Vinterbingo 2026` (`test-15-dkl-live`), `Efterårsbingo 2026` (`test-16-signup-lookup`), `Det Store Danske Vinterbingo 2027` (`test-62-board-publication-setup`), and `Påskebingo 2026` (`test-84-evidence-history`). The approved `Det Store Danske Sommerbingo 2026` record is not Development seed data: run the zero-write `--preflight-historical-import` command with the external operator input before any explicitly authorized apply. Finalized positive fixtures have active frozen roster publication snapshots; `test-98-missing-playing-assignment` intentionally remains unpublished. Use `slice1-owner` with the disposable password supplied to `--slice1-bootstrap-owner` (or `SeedAdminTwo` / `SeedAdmin!1234`) for Admin review and lifecycle actions. Use `SeedEvidenceCaptain` / `SeedEvidence!1234` for the normal linked website Captain on the first team of test-15; use `SeedEvidenceCoCaptain` / `SeedEvidenceCoCaptain!1234` for its distinct website-owned Co-captain; and use `SeedEvidenceParticipant` / `SeedEvidenceParticipant!1234` for its distinct ordinary website-owned Participant. The enabled and disabled/expired emergency credentials remain available as documented above. Reset is idempotent; repeat it before each sequence that mutates shared state.

Run the sequences in this order from a fresh reset unless stated otherwise:

The Admin **End event now** action on TEST 15 moves the event to `AwaitingFinalReview` but does not itself close the still-active submission cutoff; it is not a post-cutoff read-only shortcut and it does not by itself permit finalization. Use TEST 84 for deterministic post-cutoff read-only and Finalized/Archived checks.

1. **Ordinary Participant live workflow — TEST 15.** Sign in as `SeedEvidenceParticipant`, open the rendered first-team board route `/Events/test-15-dkl-live/Board/{teamSlug}`, choose a tile, and submit one screenshot through the ordinary team-board drawer (or its `/Captain/Submit` route-backed fallback) for the credited participant shown by the server. Open `/Submissions`, follow the new row to `/Submissions/{submissionId}`, edit the pending structured fields, and withdraw that submission; expected: the edit and withdrawal succeed through the live cutoff and history remains. Then create a second live submission from the same board route and note its `{participantSubmissionId}`. Before the separate seeded Captain Rejected-history check, sign in as `SeedAdminTwo` / `SeedAdmin!1234`, open `/Admin/Review?eventId={test15EventId}`, open that specific participant submission, and reject it with a written reason. Return as `SeedEvidenceParticipant`, open the actual rejection notification, and confirm it marks read and resolves to `/Submissions/{participantSubmissionId}`; upload a new image through **Create linked resubmission**. Expected: the participant remains the credited owner, the stored credited-character snapshot is unchanged, the new row links `ResubmissionOfSubmissionId` to the rejected predecessor, and a replay or second direct child is denied. The same account can create only for itself; it cannot select a teammate.
2. **Website Co-captain team scope — TEST 15.** Sign in as `SeedEvidenceCoCaptain`, open `/Submissions?eventId={test15EventId}&teamId={firstTeamId}`, confirm the Captain-only team-focus and team-submission-status sections, submit one screenshot for a current teammate through the existing team-board drawer, and confirm the canonical result/history route. Then request legacy `/Captain?eventId={test15EventId}&teamId={firstTeamId}` and `/Captain/Submit/{tileId}?eventId={test15EventId}&teamId={secondTeamId}`; expected: the first resolves only through the canonical workspace, the second is NotFound/denied for a different team, and neither leaks private evidence or assets. Do this before ending TEST 15.
3. **Admin review — TEST 15.** Sign in as `SeedAdminTwo` / `SeedAdmin!1234`, open `/Admin/Review?eventId={test15EventId}`, approve the seeded Pending row or reject it with a reason, and confirm only the allowed review controls and private notification result. Do not end TEST 15 in this sequence; its cutoff remains future and its live evidence journey must stay available.
4. **Deterministic post-cutoff read-only — TEST 84.** After the reset, sign in as `SeedEvidenceParticipant`, open `/Submissions`, and follow the seeded Rejected history row to `/Submissions/{historyRejectedId}`; expected: the record and asset remain readable, but edit, withdraw, and linked-resubmission controls are absent/denied because TEST 84 is already past its authoritative cutoff. A new `/Captain/Submit/{tileId}?eventId={test84EventId}&teamId={historyTeamId}` request is denied/read-only.
5. **Finalized then Archived history — TEST 84.** Sign in as `SeedAdminTwo`, open `/Admin/Events/Finalize/{test84EventId}`, verify the Finalized official-results/history projection, submit the required confirmation to **Archive event**, then reload the same route. Expected: state is Archived, official result history remains visible, public `/Events/test-84-evidence-history/Board` and tile routes remain historical, and the Rejected evidence is not public. This sequence may be repeated from a fresh reset; do not use TEST 15 for the post-cutoff or archive checks.

- [x] **S8-RM-01** — The exact reset identities, pending/rejected records, live/cutoff ordering, standalone fallback, finalized/archived progression, and expected private/public results above passed in English for the approved reachable surfaces; Danish manual inspection remains deferred to the UI overhaul.

### Slice 8 consolidated manual acceptance — 2026-08-01

- **S8-01 — Historical acceptance superseded only for route ownership.** Participant and Captain history now use the one canonical `/Submissions` workspace and `/Submissions/{id}` detail route; legacy Captain history routes are compatibility aliases only.
- **S8-02 — Passed after correction.** Linked resubmission uses the shared upload interaction, retains success feedback, and Admin review exposes the existing local time plus UTC.
- **S8-03 — Passed after correction.** Co-captain teammate submission retains visible success feedback.
- **S8-04 — Passed.** Emergency authority boundaries behaved as expected.
- **S8-05 — Passed after correction.** Only the selected requirement’s drops are visible/selectable; crafted cross-tile POSTs remain server-rejected.
- **S8-06 — Passed after correction.** TEST 84 post-cutoff history is reachable and read-only through normal navigation.
- **S8-07 — Passed.** Public approved-only/privacy behavior remained correct.
- **S8-08 — Scoped exception.** Manual snapshot-mutation steps were intentionally dropped as unreasonable; public privacy checks remain required and passed.
- **S8-09 — Passed.** Existing automated authority/concurrency boundary coverage remains accepted.
- **S8-10 — Deferred by approved boundary.** Danish manual inspection remains deferred to the UI overhaul; existing localization behavior and tests remain preserved.

### Final manual correction notes

The final correction retest passed in Safari after replacing `optgroup.options` with `group.querySelectorAll("option")`; initial server filtering, native requirement-change synchronization, and cross-tile server rejection all remain accepted. Slice 8 is accepted after the independent implementation review, all restricted re-reviews, final scope-delta review, and consolidated manual acceptance passed. The remaining Slice 8 packaging/worktree note is historical traceability; the current Slice 9 and Slice 10 records follow below.

- [x] Participant self-submit/edit/withdraw and Captain/Co-captain teammate-submit use server-derived credited character; no account selector or cross-team/private-history leakage.
- [x] Rejected-only linked resubmission uses a new image, copies credited snapshots, preserves predecessor history, blocks replay/stale writes, and remains cutoff-bound.
- [x] Admin Pending review exposes only reasonless Approve or reasoned Reject; rejection reaches linked credited participant/current linked Captain/Co-captains once, with unlinked fallback to eligible leadership.
- [x] Admin reasoned metadata correction derives participant from the selected Playing character and preserves submission time, weight, contribution, and active asset.
- [x] Approved and Archived public/tile/board evidence shows only active Approved evidence and stored credited-character snapshots; no private notes/reasons/authority data appear publicly or in realtime payloads.
- [x] Only Pending evidence blocks finalization; Approved reversal preserves exact deduction/rebalancing/history.
- [x] Deprecated Admin upload, Request Changes, same-record resubmit, active duplicate, privacy/visibility controls, hidden placeholders, routes, and controls are absent; private non-Approved evidence remains scoped, linked Rejected resubmission and protected drawer/standalone fallback remain.

### Slice 8 final automated acceptance — 2026-08-02

Domain `156/156`, Application `83/83`, Browser `67/67`, and Integration `230/230` passed with zero failures/skips (`536/536` combined). Durable final Integration TRX: `/private/tmp/slice8-final-automated-gates-20260801/integration-rerun.trx`. Release build, formatting, EF pending-model, `git diff --check`, clean/retained migration/preflight, Development double-reset, parity, artifact/secret scan, and staged-state checks passed; no required gate remains unverified.

## Slice 9 — Event end, live replacement, finalization, notifications, and history

- [x] Consolidated manual cases finalized for the Slice 9 handoff.
- [x] Scheduled/early end, upload grace, cutoff, and UTC screenshot review.
- [x] Withdrawal, vacancy, optional replacement, and captain-warning paths.
- [x] Blocker resolution, finalization/unfinalization, archive, and participant read-only history.
- [x] Personal notification versus unresolved-action behavior.

### Slice 9 consolidated manual checklist — Passes 9.1–9.3

Development reset: `dotnet run --project src/Bingo.Web -- --reset-test-data` after migrations. Reset is idempotent and recreates the named workflow fixtures while removing outdated or manually created disposable events. Finalized fixtures intended to expose rosters have one active frozen publication cycle; the explicit missing-playing-assignment blocker remains unpublished. The linked `SeedReplacement` waiting-list participant belongs to `Vinterbingo 2026` (`test-15-dkl-live`). Use `SeedAdminTwo` / `SeedAdmin!1234` for Admin actions, `SeedReplacement` / `SeedReplacement!1234` for the linked replacement, `SeedEvidenceCaptain` / `SeedEvidence!1234`, `SeedEvidenceCoCaptain` / `SeedEvidenceCoCaptain!1234`, and `SeedEvidenceParticipant` / `SeedEvidenceParticipant!1234` for the existing test-15 evidence journeys. Use the reset/bootstrap owner credentials where the existing Admin route requires the owner identity. If a retained database fails the final-review migration preflight, record the reported event and official-snapshot IDs, reconcile the retained transition history from backup/operational records, and rerun migrations before attempting reset; do not delete the database to bypass the preflight.
The user externally completed two post-correction resets against the recreated local Development database through the Release `--no-build` path, both with exit code `0`. Direct inventory confirms the five approved events, including the minimal TEST 16 signup-lookup fixture, and complete DKL/Slice 9 fixture relationships described below; no unrelated manual/test events remain.

Approved DKL fixture inventory, recreated on every reset:

- `TEST 13 — DKL Board` / `test-13-dkl-board`: `SignupClosed`, `Europe/Copenhagen`, future schedule, 20-person cap, 5×5 `DKL comparison board` in `Draft`, no teams or waiting-list rows, and no publication pointer. Use `/Events/test-13-dkl-board/Board` and the Admin board/editor routes for private derivation, approval, and preview checks.
- `Vinterbingo 2026` / `test-15-dkl-live`: `Live`, started before reset with a future scheduled end and normal +30-minute cutoff, 60 confirmed drafted participants across six named teams of ten, finalized draft with 48 picks, one active frozen roster publication cycle, six enabled Captain authorities plus the disabled emergency coverage account, published 5×5 DKL board/approval snapshots, approved progress, one Pending and one Rejected review fixture, and one linked `SeedReplacement` waiting participant with a frozen Playing account. Use `/Events/test-15-dkl-live/Teams`, `/Events/test-15-dkl-live/Board`, `/Admin/Events/Participant/{eventId}/Participants/{participantId}`, `/Admin/Events/Manage/{eventId}`, `/Admin/Review`, and canonical `/Submissions` plus legacy `/Captain` alias routes for the Slice 9 journeys.

Required routes: `/Admin/Events`, `/Admin/Events/Manage/{eventId}`, `/Admin/Events/Finalize/{eventId}`, `/Admin/Events/Participant/{eventId}/Participants/{participantId}`, `/Admin/Review`, `/notifications`, `/Account/MyEvents`, `/Events/{slug}/Board`, and `/Evidence/{id}`.

- [x] TEST 15: exercise the due/premature-end path, confirmed reasoned resume with a future replacement end, stale/repeated recovery rejection, and the second authoritative end; confirm ordinary uploads/cutoffs and emergency access are not silently reopened.
- [x] TEST 15: withdraw a live participant, leave the vacancy open, promote `SeedReplacement`, verify prospective membership/authority/history, and mark the waiting-list follow-up complete. Confirm notification reading does not resolve the Admin action.
- [x] Final review: use a no-completed-team event to confirm no completion acknowledgement is required; use completed teams to confirm each team requires **Completion time inspected**. Verify reasoned correction, reinspection after correction, and strongly confirmed/reasoned exceptional override.
- [x] Finalize, unfinalize with confirmation/reason, re-finalize, and archive through `/Admin/Events/Finalize/{eventId}`. Confirm both official snapshot cycles/resolutions remain visible, results are deterministic, and uploads do not reopen.
- [x] Finalization: submit the normal finalization POST without the bound confirmation value and confirm it is denied; submit the checked confirmation and confirm official publication succeeds. Review a resumed-cycle submission timestamp in the final-review gap and confirm the Admin review page identifies it as outside an eligible live interval.
- [x] TEST 84: archive the seeded finalized event, open `/Account/MyEvents`, follow the archived evidence-history link, and confirm the former participant sees only their own rejected/withdrawn history read-only. Confirm another participant, team-private evidence, submission, edit, review, and public rejected evidence remain unavailable.
- [x] As Admin, inspect `/notifications`: personal unread count/read state and Admin action count/list remain separate. Confirm direct destinations for pending review, waiting follow-up, postponed start, vacancy, and missing Captain appear only while authoritative work is unresolved.
- [x] Follow the waiting-list action link after the vacancy is filled and confirm the promoted participant route still renders the explicit Mark follow-up complete form; complete it once and repeat the stale/idempotent POST.

This checklist is the compact Slice 9 manual handoff. S9-01 through S9-07 passed; S9-05 preserved placements, metrics, and Version 1 history after unchanged TEST 84 re-finalization. S9-06 accepted the owner-rendered archived evidence link, current raw-image display, and unrelated/anonymous privacy boundaries; viewer presentation is deferred to the UI overhaul. Consolidated Slice 9 manual acceptance and final automated verification are approved; Slice 10 was not started.

## Slice 10 — Wise Old Man integration

### Current SEP-02 scheduling authority — fixed hourly slots (2026-09-22)

The current scheduling correction supersedes only the cadence and freshness
claims in the historical S10-04 record below; that older manual evidence is
retained unchanged as superseded history. For a retained actual Live start at
18:00 UTC, the normal slots are 19:00, 20:00, 21:00, and every hour thereafter.
There is no normal fetch solely because the event entered Live. A late fetch,
retry, manual refresh, urgent assignment refresh, cooldown, restart, or resume
does not move those slots; a downtime recovery performs at most one current due
refresh before selecting the next future slot. A Live record without
`ActualStartedAt` remains explicitly unscheduled. Compatible cached activity
remains visible after one hour as stale-compatible data, without a public
freshness/status banner.

Focused automated evidence covers fake-time slot/retry/UTC-DST/resume/downtime,
PostgreSQL lease/publication and stale-compatible projection behavior. No real
Wise Old Man request is permitted. Manual visual acceptance of this correction
remains separate from those technical checks.

Reset Development data after applying migrations. The reset is idempotent and makes no Wise Old Man request:

```bash
dotnet run --project src/Bingo.Web -- --reset-test-data
```

Use `SeedAdminTwo` / `SeedAdmin!1234` for the Development journeys. After reset, TEST 15 (`/Admin/Events` → follow the rendered **TEST 15 — DKL Live** Manage link) is `Live` with local competition `1515`, a complete cached generation fetched three hours ago and therefore due for manual refresh. TEST 16 (`test-16-signup-lookup`) is `SignupOpen` with an empty signup roster. `SeedAdminTwo` has linked fictional `Dev Lookup Player` with saved EHB `12.5`; this is the deterministic My Accounts and signup/edit lookup character. The reset seeds TEST 15 with fictional `Dev Player 001` plus current regular `Dev Activity Secondary` and never contacts Wise Old Man.

In Development, the configured `WiseOldMan:DevelopmentFake` transport handles all WoM routes without network access. Override these settings before starting the app to exercise the named controls, then reset again:

```text
WiseOldMan__DevelopmentFake__AutomaticSynchronizationEnabled=true|false
WiseOldMan__DevelopmentFake__PlayerMode=Success|NotFound|Unavailable|RateLimited
WiseOldMan__DevelopmentFake__CompetitionMode=Complete|Incomplete|Unavailable|RateLimited
WiseOldMan__DevelopmentFake__TemporaryFailures=0..3
WiseOldMan__DevelopmentFake__Remaining=19 (use 3 to prove the final-three reserve)
WiseOldMan__DevelopmentFake__RetryAfterSeconds=30
```

The fake serves `GET /players/{username}` and official-shape `GET /competitions/1515?metric=ehb`; it never exposes or calls WoM write endpoints. Set `AutomaticSynchronizationEnabled=false` and restart the Development app for manual lookup, due-refresh, and partial/recovery checks; this prevents the worker from consuming the deterministic due state. Set it back to `true` only for the lifecycle stop/resume check. The control is ignored outside Development. Return to `Success`/`Complete`, `Remaining=19`, and reset before the final cached-projection check.

- [x] **S10-01 — Cached public/team projection.** With `AutomaticSynchronizationEnabled=false`, `PlayerMode=Success`, `CompetitionMode=Complete`, and `Remaining=19`, reset and start the app. As `SeedAdminTwo`, open `/Account/MyAccounts` and use **Fetch from Wise Old Man** for `Dev Lookup Player`; confirm the saved EHB becomes `12.5`. Open `/Events/test-16-signup-lookup/Signup`, select `Dev Lookup Player` as the regular account, use its explicit **Fetch from Wise Old Man**, submit the valid signup, and open the rendered Edit action. Restart with `PlayerMode=Unavailable`, use the Edit page’s explicit fetch to show failure feedback, then submit a manually edited valid signup and confirm the optional WoM failure does not block it. Finally use anonymous `/Events/test-15-dkl-live/Board?view=leaderboards&ranking=activity` and `/Events/test-15-dkl-live/Board/touch-kids-not-grass`: confirm `Fetched from Wise Old Man`, team totals, participant-based average, tied MVPs, and both `Dev Player 001` / `Dev Activity Secondary` account rows. No page render makes a WoM request. **Passed.** The successful action at step 8 had no visible feedback; cached/team EHB is functionally correct, but its presentation is visually broken and deferred to the UI overhaul.
- [x] **S10-02 — Bootstrap, reserve, and known retry feedback.** Restart with `PlayerMode=Success`, `Remaining=3`, and automatic synchronization disabled. The first explicit My Accounts fetch observes the final-three reserve; the next explicit fetch is rejected locally, makes no external request, and displays the known reset time. Restart with `PlayerMode=RateLimited`, `Remaining=19`, `RetryAfterSeconds=30`, and automatic synchronization disabled; use the explicit My Accounts fetch and confirm localized failure feedback includes the known retry time. Restore `PlayerMode=Success`. **Passed.** Development fake acceptance of arbitrary non-missing names in Success mode is deterministic test behavior; production remains authoritative to real WoM.
- [x] **S10-03 — Due manual refresh and partial recovery.** Reset with automatic synchronization disabled. Sign in as `SeedAdminTwo`, follow the rendered TEST 15 Manage link, and use **Refresh cached activity**. Confirm success (the seeded cache is due). On the same Development-only Manage page, click **Make next refresh due (Development TEST 15)** and confirm the due-state success feedback. Set `CompetitionMode=Incomplete`, restart, follow the rendered TEST 15 Manage link again, and use **Refresh cached activity**: the configured fake omits `Dev Activity Secondary`; Manage shows that exact missing current Playing account name, while public Board/TeamBoard show fresh matched values, a provisional/partial warning, missing-count and participant/account coverage, never the missing name. Confirm no missing account is displayed as zero and no prior-generation value is carried forward. Click **Make next refresh due (Development TEST 15)** again, set `CompetitionMode=Complete`, restart, follow the rendered link, and use **Refresh cached activity** once more; confirm complete rankings recover. The Development-only due control prepares TEST 15’s persisted due/manual-cooldown admission state, including the authoritative successful-time/cooldown facts and normal due time; it is safe to repeat and does not bypass the normal production cooldown/retry rules. A failed refresh is an error, not a success, and its retry/reset time is shown when known. **Passed after the Live competition-synchronization capability correction.** The Admin manual **Refresh cached activity** feature is explicitly approved and remains in scope.
- [x] **S10-04 — Lifecycle stop and legitimate resume.** Reset with `AutomaticSynchronizationEnabled=true`, `CompetitionMode=Unavailable`, `TemporaryFailures=3`, and `Remaining=19`. The first automatic fetch after an event’s initial transition into `Live` is scheduled two hours after that transition; later successful normal fetches remain two hours apart. Wait for the worker’s first attempted refresh and note the private retry time/count on Manage. End TEST 15 through its existing Manage **End event** action and confirmation; after the retry becomes due, confirm the synchronization attempt timestamp/count does not change while the event is outside `Live`, while its cached public/team state remains readable. Use Manage’s **Resume event** action with a future replacement end and a written reason, confirm the event returns to `Live`, and wait for the next due retry: a future persisted due time remains future, while an overdue persisted due time permits one prompt fetch and then receives a new two-hour normal anchor. Lifecycle switching itself creates no extra fetch. No lifecycle/readiness action depends on WoM availability. **Passed.**
- [x] **S10-05 — Retained state after event end.** With a complete or partial cache, end TEST 15 through Manage and reload the public Board and team route. Confirm the last cached timestamp and the retained complete or provisional available rankings remain visible without refreshing; if the latest generation has zero matched accounts, rankings remain hidden. Do not use a direct URL to bypass the rendered Manage navigation. **Passed.** Screenshots confirmed stale cached activity, timestamp, totals/ranks/coverage after event end; the visual table layout is deferred to the UI overhaul.
- [x] **S10-06 — Final reset/reachability.** Restore `AutomaticSynchronizationEnabled=false`, `PlayerMode=Success`, `CompetitionMode=Complete`, `TemporaryFailures=0`, `Remaining=19`, reset once more, and confirm TEST 16 is still `SignupOpen`/empty, TEST 15 is `Live` with due local cache, `/Account/MyAccounts`, `/Events/test-16-signup-lookup/Signup`, the rendered TEST 15 Manage link, and both cached public routes are reachable. No real WoM request is permitted at any step. **Passed; the Development fake prevented real WoM calls.**

**Slice 10 consolidated manual acceptance (2026-08-03):** S10-01 through S10-06 passed. The two S10-01 presentation notes and S10-05 table-layout note are deferred to the UI overhaul and are not functional blockers.

## Admin-test follow-up corrections — implementation complete; manual acceptance pending

This is the compact Section 6 manual journey. Apply the one migration, run the
idempotent Development reset, and use only the named minimal fixtures added by
their owning pass. Automated tests own exhaustive lifecycle/actor/concurrency
matrix coverage; this checklist proves real navigation, integration, and the
interactions that source assertions cannot establish.

- [x] **AF-01 — Danish transport and additive roles.** In Danish, follow normal
  navigation through My Accounts Add/Edit/fetch and Signup create/edit with comma
  decimal input; confirm fetched machine values render and post back locally without
  changing value or validation, then repeat the focused journey in English and the
  existing Onboarding fetch. Sign in with the stable global-only, Admin+Participant,
  Admin+Captain, and Admin+Co-captain fixtures. Global-only accounts retain Admin
  navigation but receive no team ledger, drawer, or mutation. Each additive account
  retains exactly its genuine event navigation and submission/focus authority; the
  existing explicit SuperAdmin read-only focus inspection remains separate.
  Accepted 2026-09-05 for all locally reachable journeys. Local Discord OAuth does
  not return to localhost, so the user accepted the focused EN/DA Onboarding
  rendered-control and persisted-comma integration proof in place of manual
  Onboarding execution.
- [ ] **AF-02 — Item identity and objective isolation.** From the rendered Admin
  Board editor, inspect one duplicate-disabled requirement containing the same item
  from two bosses. Both source alternatives remain configurable, but inconsistent
  effective caps block approval. With a consistent cap of `1`, approve evidence
  from one source and confirm the other cannot add contribution to that same team
  and requirement. Confirm the same item still contributes independently to each
  of three sibling objectives, while a duplicate-enabled requirement follows its
  own target/source cap.
- [ ] **AF-03 — Submission and review lifecycle.** From a rendered team card open
  Team Board, use the submission drawer, follow `Team history` to `/Submissions`,
  open detail, and follow an actual rejection notification destination. In Admin
  Review exercise Pending approve/reject/correction and Approved reversal, including
  a retarget between differently weighted sibling objectives. From the seeded
  Reversed record create its single new-image Pending child while uploads are open;
  confirm repeat/stale actions do not duplicate contribution, audit, notification,
  or child, and that closed/finalized/terminal states expose no invalid controls.
  Archive the dedicated former-owner fixture, open My Events through normal
  navigation, and follow its own Rejected/Withdrawn detail and `/Evidence/{assetId}`
  links. Confirm teammate/private/cross-team records and all mutations remain denied.
- [ ] **AF-04 — Draft and bounded presentation.** Follow Admin navigation to the
  not-yet-finalized primary-plus-secondary Playing fixture and finalize it. Confirm
  Signup has no orphaned `03`, only its required system primary defaults to the
  preferred character, and later account questions begin None/unselected. Confirm
  the Admin logo opens public home and Onboarding's joined EHB/WOM control uses a
  text-color-only hover plus one complete invalid outline. Before Board publication,
  follow the landing-page `View roster` action to the standalone Teams page and
  confirm no Board-view navigation appears. After publication, follow the landing
  page to Board, use its localized Teams/Hold sibling navigation, and confirm the
  unchanged Teams page now shows the same row with Teams/Hold current and the
  reduced navigation-to-masthead gap. Open evidence from Board, Team Board, and
  Admin Review; in each existing popup verify labeled zoom in/out/reset,
  wheel/trackpad, drag/pan, practical pinch/touch, keyboard focus/operation,
  Escape/backdrop close, focus return, viewport containment, and transform reset
  after close/reopen and image change in light/dark and narrow layouts.
- [ ] **AF-05 — Retained migration correction/retry.** Use only a copied retained
  database. Run the Section 6 item-identity preflight, review its two-family report,
  complete the external adjudicated mapping without changing frozen names/current
  source mappings, and invoke the existing migrate path with the mapping file plus
  exact hash confirmation. Confirm stale fingerprint, missing/extra row, unknown
  item, mismatch, and wrong hash fail closed; then apply the valid mapping, verify
  both non-null snapshot identities and shared-item caps, rerun preflight, and
  confirm no temporary table or mapping artifact remains. Never rehearse against
  the user's working database.

## Historical event import — local operator acceptance

**Accepted 2026-09-01.** The corrected one-time import of `Det Store Danske
Sommerbingo 2026` was applied only to the local Development database. The user
manually inspected the resulting archived event and reported that everything
looks good. The archived public result preserves the approved six-team roster,
exact 402 counter units across 150 team/tile cells, itemless reconstructed
approved rows in Recent Drops without fabricated drops or evidence,
within-team EHB-weighted attribution/timing, the exact historical disclosure,
the complete source WoM snapshot without normal refresh, and the approved
Maggot King and Superior Slayer rules.

The reviewed public manifest SHA-256 is
`e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`. Apply is
preflight-first and exact-hash idempotent; divergent input fails closed and
transaction failure leaves no partial event. The named active SuperAdmin and
exact event-name confirmation are validated inside the locked serializable
apply transaction.

- [x] Independent Sol High closure review: the three additional blockers
  (Production CLI reachability, reviewed public-manifest pinning, and active
  SuperAdmin authorization inside the locked serializable apply transaction)
  were remediated before local commit
  `c4130f437b82cf5ceb5f130cf8a77a3a05ae2079` (`Add historical 2026 event import`).
- [x] Focused verification passed: `HistoricalBoardReferenceTests` 25/25;
  `Bingo.IntegrationTests` Release compilation with 0 warnings/errors;
  `HistoricalImportIntegrationTests.AppliesFictionalOperatorInputAndExactRerunIsNoOp`
  1/1 using isolated Docker/Testcontainers; and
  `Slice10Pass103ActivityProjectionTests.DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly`
  1/1 using isolated Docker/Testcontainers.
- [ ] Production import, rehearsal-event hide/removal, push, deployment, and
  production migration remain separate explicitly authorized operations and
  were not performed.

## Milestone 9 — Complete UI overhaul and full regression

This section is the remaining final UI and whole-application regression gate after the version-one functional foundation. Its order and release gates are owned by `DELIVERY_PLAN.md`, and page approval is owned by `UI_PAGE_MATRIX.md`.

- [ ] Apply the approved site-wide visual rules to every public, participant, captain, Admin, Super-Admin, error, privacy, and empty state.
- [ ] Repeat every completed Slice 1–10 manual checklist against the final interface.
- [ ] Verify current Safari and Chromium on desktop, intermediate/tablet, and representative mobile widths.
- [ ] Verify keyboard-only use, focus order/visibility, screen-reader labels, reduced motion, contrast, validation summaries, and no-JavaScript fallbacks.
- [ ] Verify all permission, empty, loading, success, warning, error, conflict, and stale-state presentations.
- [ ] Verify the shared masthead notification and account popups in light/dark desktop and constrained widths: visible trigger text, viewport containment/z-index, keyboard/focus order, outside-click and Escape close, focus return, and clean reopen.
- [ ] Verify `/Submissions` exposes the signed-in current team member's complete retained team ledger, including departed credited members, and normal authenticated navigation reaches it. From the team-specific Board main progress sidebar, all authorized current members use the canonical `Team history` button to `/Submissions`; Captains/co-captains additionally see the team-focus and team-submission-status sections. Pending/rejected mutations are owner-only for ordinary participants; eligible Captains/co-captains retain broader server-authorized editing; teammate-owned and all other states remain read-only for ordinary participants.
- [ ] Open an evidence rejection notification as the credited participant and as an eligible Captain/co-captain recipient. Both must mark the notification read and resolve to the same canonical stored route `/Submissions/{id}`; relevant general submission navigation resolves to `/Submissions`, Admin review notifications remain `/Admin/Review/Details/{id}`, and no route exposes another team's private evidence.
- [ ] Confirm no critical/high defect remains before the production-preparation milestone.


## Stats production integration — consolidated user acceptance (2026-09-15)

**Production UI accepted, 2026-09-16:** The user passed sections 1–6 of the concise chat
checklist (appearance, Drop value, Luck, milestones, Board progress/highlights and guidance/
artwork), subject to later discoveries and the known unresolved sticky Luck comparison gap.
UI_PAGE_MATRIX.md owns that approval. This is not a pass of sections 1–6 below: detailed
functional/provider scenarios remain unexecuted unless separately recorded.

**Functional walkthrough steps 1–9 passed — user report, 2026-09-16:**
“as far as i can tell 1-9 are fine”. This refers to the subsequent numbered action list:

- [x] 1. Sign in, refresh and retain Admin access.
- [x] 2. Approve later then earlier pending evidence; check live Stats and submission-time ordering.
- [x] 3. Reverse approval; check live removal/recalculation and persistence after refresh.
- [x] 4. Submit a new eligible drop; pending evidence excluded, approval counted once without weight multiplying GP/count.
- [x] 5. Verify team/player attribution, event totals and submission-time progress/milestones.
- [x] 6. Inspect empty, missing-price/activity and waiting-activity fixtures.
- [x] 7. Verify Hide tooltips persists across sign-in and remains account-specific.
- [x] 8. Verify artwork Cancel/Save/Reset persistence and editor permissions.
- [x] 9. Verify a stale second artwork save cannot overwrite the first.

These are user-reported manual passes, not new automated executions or approval of broader
edge cases below. Sign-in is accepted for the tested flow; the earlier Safari/localhost
cookie cause was not independently established. The known sticky comparison gap remains.
The 12-step action-list walkthrough is complete, with the recorded UI exceptions.

**Step 12 passed — user completion and persisted verification, 2026-09-16:** Dedicated
event `stats-step12-finalization` (`99e25fb7-adb8-489e-a617-d5feaaebdf32`) is Archived.
The user completed inspection, finalization, reopening, reasoned correction, reinspection,
refinalization and archive. They confirmed choosing 14 September at 14:10 Copenhagen;
the saved correction matches that selection, despite the example suggesting 15 September.
Version 1 retains 15 September 14:00; active version 2 retains 14 September 14:10.
Planner read-only checks confirmed both versions, official Stats/milestone consistency,
unchanged raw drop/tile history, 125M GP / 5 drops / +25% Luck, exact frozen price/rate
and metric records, and original provider timestamps/activity batch. Archived Stats
returned HTTP 200. Evidence is under `/private/tmp/bingo-stats-manual-20260915/` in
`step12-inspection.json`, `step12-verification.json` and `step12-http-after.json`.
Use 127.0.0.1:5189 links; localhost login did not work for the user. No login fix claimed.

**Step 11 completed — executed checks and independent review, 2026-09-16:**
Actual clients successfully fetched Wiki prices and the complete WOM request (71 bosses
plus EHB for all 93 participants). The user confirmed the five normal/alternate mode
pairs have independent KC; the temporary gate was removed. Fourteen focused Release
PostgreSQL checks passed, including separate mode totals and scheduled cache recovery.
The durable catalogue contains 195 API prices and 116 confirmed untradeable zeroes;
22 sparse items use their latest available completed hourly observation for initial
population only. All original identities, rates, drops and legacy variants are preserved.
The isolated manual database received all 311 item values and 67 source mappings, with
audits/version checks. Abyssal Sire's newer temporary-unavailability check was preserved.
All frozen event prices and fixtures were unchanged. Fresh independent review passed;
the checked build was restarted on 5189 and four existing fixture endpoints returned 200.
This is not deployment or a new visual-acceptance claim. CURRENT_STATUS.md links evidence.

**Step 10 accepted — user report, 2026-09-16:** “Everything passes.” The user noted that
the small frozen-GP test values display in M and cannot be read, while the repeat-drop
count is visible. Record this as a display issue, not visual confirmation of exact small
GP values; those values have independent HTTP verification. No formatting fix is claimed.

**Step 10 preparation executed — 2026-09-16:** Dedicated
`stats-step10-luck`, `stats-step10-late`, `stats-step10-board-gates`,
`stats-step10-start-api` and `stats-step10-start-fallback` fixtures are available on the
isolated port-5189 app. Controlled handlers/services and independent HTTP checks passed;
this does not mark live-provider verification passed. Manual acceptance is recorded above. Exact click-through
instructions and expected numbers are in `/private/tmp/bingo-stats-manual-20260915/STEP10.md`
and `STEP10-PRICE-EDGES.md`. CURRENT_STATUS.md records execution scope/evidence.


Prototype design approval remains authoritative;
this walkthrough checks its production port and real behavior, including the earlier
Pass 1–2 catalogue/start UI. It is not a request to redesign the approved Stats sections.
UI_PAGE_MATRIX.md owns page approval. Source/automated evidence is in CURRENT_STATUS.md.

Use an isolated controlled store with `Vinterbingo 2026` (`test-15-dkl-live`),
SeedEvidenceCaptain, SeedEvidenceCoCaptain, SeedEvidenceParticipant, bootstrap Super Admin
and SeedAdminTwo. Use the existing private published-board fixture
`test-62-board-publication-setup` for manual start and a separate scheduled-start counterpart.
Prepare hidden/private/unpublished/excluded-import, missing-price/activity and team-size
variants in that isolated store. Do not reset/populate the user's running database; no
live fixture preparation or app restart was performed by Pass 5. No browser automation.
Keep the Live journeys intact until the finalization checks at the end.

### 1. Catalogue mapping, pricing and action feedback — Admin / Super Admin

- [ ] Open **Admin → Catalogue**, select a source, expand **API mapping**. Use **Suggest
  exact activity mapping**, **Validate and save**, and **Save without validation**.
  Reopen and check the exact metric and Last checked state; ordinary accounts cannot edit.
- [ ] Expand a drop's **API mapping and price**. Check **Suggest exact item mapping**,
  exact variant/ID, **API hourly average**, **Manual catalogue value**, **Untradeable
  (0 GP)**, stored value/time, and both Save actions. Missing prices stay distinct from 0.
- [ ] Exercise a unique match, ambiguous/no match, unsupported ID/metric and unavailable
  API. Inspect each newly revealed warning/error/success message, recovery action and
  modal visibility; no duplicate or hidden-behind-dialog feedback.
- [ ] Use **Add drop** and **Fetch price and add drop**: required checked initial value,
  explicit 0/untradeable, manual no-drop objective, optional exact item ID, successful
  lookup and unavailable lookup. Reopening retains the intended identity/value/rates.
- [ ] Open two editors for the same shared item. Save one, then attempt the stale second
  save. It must reject the overwrite and provide a usable retry without losing personal
  rates. Editing an API identity must not silently replace a different variant.
- [ ] Exercise a candidate outside 0.5–2× the trusted value and positive↔zero candidates.
  Mapping status remains separate from the price rejection. The trusted value stays;
  rejected candidate/hour and the checked manual-value recovery are visible. Verify
  explicit fixed values remain fixed during API refreshes.

### 2. Start and price freezing — Super Admin / scheduled worker

- [ ] In **Admin → Events → Manage** for the manual-start fixture, check the missing-GP
  blocker names its drops and links the recovery to Admin Catalogue; explicit 0 is valid.
  Complete the values and start; inspect the start success/fallback feedback.
- [ ] Start the independent scheduled fixture. Check the same actual-start last-completed
  hour, valid one-sided average/catalogue fallback and guarded unusual-price behavior.
  Use controlled hourly responses for the hour-boundary/retry scenario.
- [ ] Change catalogue prices afterward and introduce a new eligible item. Existing event
  prices stay frozen; the late item uses its required catalogue value at introduction.
  There is no event price editor or retroactive rewriting of approved history.

### 3. Enter Stats and compare the exact approved UI — anonymous / all roles

- [ ] From the event's **Boards / Drops / Leaderboards / Stats / Teams** navigation open
  **Stats** at `/Events/test-15-dkl-live/Stats`. Refresh and deep-link to it. The same
  event, current navigation and applicable Captain/submissions links remain selected.
- [ ] Try hidden, private, unpublished, unknown and reconstructed Sommerbingo slugs.
  They expose no Stats or preferred-event substitution. A cancelled public event uses
  the existing cancelled-event page.
- [ ] Compare the six Stats sections and artwork editor directly with the approved
  `prototypes/stats/outputs/stats-page-prototype.html`, density CSS and JS. Confirm Barlow
  headings, colors, spacing, sizes/caps, list overflow, hover/focus and animation timings.
  The app header/masthead stay unchanged; the Stats content below keeps the prototype's
  1640px maximum and 3.6% gutters, becoming 5% at ≤850px, without double padding.
- [ ] Check both app themes, 2/3/4/5 teams, overflow with 8/15, long public names, narrow
  stacked layouts and empty data. No sample dates, event-size/stage/drop-preview/theme
  toolbar remains. Adjust artwork is visible only to Super Admins. No page initialization
  error appears with the demo controls absent.

### 4. Real evidence and all approved interactions — Captain → Admin → Stats

- [ ] Submit two eligible items; approve the later submission first, then the earlier.
  Stats refreshes on the existing progress notification (and on revisit/direct refresh).
  GP, counts, histories and milestones follow submission time. Weighted progress does not
  multiply item count. Reverse an approval and verify the refreshed values again.
- [ ] **Drop value:** Teams drilldown/back, global Players top five, sixth hover/focus
  preview and pinned comparison, approved player search scopes and Everyone. Check
  divider/compact search, six-result selection without a page jump, bounded pie lists,
  most valuable drops scoped to the selected team, and unchanged hover behavior. A refresh
  preserves the current scope, comparison, searches and local/page scroll.
- [ ] **Luck:** team roster/back, five lowest/highest, search by player/team, sticky
  comparison add/remove/replacement, and synchronized bar/number motion. Scroll without
  rescaling. Scores within ±60 retain the original scale; larger values expand one shared
  full-view scale, including the comparison, with exact score labels and proportional bars.
- [ ] Check unavailable/unranked/zero-recorded/estimated states and a stale prior checkpoint.
  Missing activity is not 0% Luck; missing prices are not 0 GP. Stale results retain their
  original calculation/provider times and evidence revision, including after additive
  approval; reversal invalidates an incompatible checkpoint. First-approved rates survive
  later duplicate/rate edits; team Luck pools expected/received counts.
- [ ] **Event milestones:** team filter, reached chronological order, default order of
  pending milestones, ended unreached states, both horizontal ends and narrow spacing.
  **Board progress:** hover any time; focus/tap completion markers for actual tile,
  timestamp, tile/row-column and contribution totals. Interrupt the entrance animation.
  No extra diagonal scoring rows or independently calculated placements appear.
- [ ] Check Keeps on dropping and Most versatile against the real retained item/player.
  Missing artwork/empty highlights remain honest. Test reduced motion and rapid interrupted
  scope changes, help dismissal, comparison replacement and timeline filtering.

### 5. Saved guidance and shared artwork

- [ ] As an ordinary account select **Hide tooltips**, navigate away, return, and sign in
  in a new session. The preference belongs only to that account. Uncheck to restore
  guidance. Each ⓘ still reopens help and ×/Escape dismisses it for the current visit.
  A stale account save reports a conflict and a usable reload/retry.
- [ ] As Super Admin use **Adjust artwork** for the actual displayed repeat item. Drag,
  arrow/Shift-arrow, horizontal/vertical, zoom and rotation match the approved preview;
  preview sizes remain usable. **Cancel** makes no write; **Save artwork** survives reload
  and is visible to ordinary/anonymous readers. **Reset to default** is a draft until Save;
  Cancel after Reset preserves the saved appearance.
- [ ] Open two artwork editors, save one then the stale second. The second cannot overwrite
  the first. Check validation/permission/conflict feedback inside the open editor and the
  saved outcome after closing it. Ordinary/anonymous/disabled users cannot mutate artwork.

### 6. Deferred external provider gates — before enabling affected real results

- [x] Step 11: actual client verified the live request for 71 boss metrics plus EHB,
  repeated plural parameters, all 93 participants, raw values, player-record timestamps
  and observed −1 sentinel cases. Missing-response cases retain their prior controlled
  test evidence; the live response omitted no requested metric.
- [x] Step 11: the user confirmed independent KC for CoX/CM, ToB/HM, ToA/Expert,
  Gauntlet/Corrupted and Nightmare/Phosani. The temporary gate was removed and paired
  activity/cache recovery checks passed. This is explicit operator confirmation of
  semantics, not a claim that API documentation states the inclusion rule.
- [x] Step 11: populated the durable catalogue and isolated manual-test catalogue with
  195 API prices and 116 confirmed untradeable zeroes. Source support and preserved
  database validation exceptions are recorded in CURRENT_STATUS.md. No deployment.

### 7. Finalization last

- [x] Step 12: after all Live journeys, finalize/archive a supported fixture and exercise the
  legitimate unfinalization/correction path. Official completion/results, frozen prices,
  first-approved rates and original Luck checkpoint times retain their defined meaning.
  Do not manufacture retrospective Stats for the excluded reconstructed Sommerbingo import.
- [ ] Reconcile source review, applicable whole-slice release gates and this visual
  walkthrough with the planner. No packaging, push or deployment follows automatically.
