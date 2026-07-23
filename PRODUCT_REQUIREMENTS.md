# OSRS Community Bingo Platform

## Product Requirements Document

**Status:** Requirements draft v0.2; technical architecture approved  
**Last updated:** 2026-07-11  
**Audience:** Community bingo organizers, reviewers, captains, and developers

## 1. Product summary

The product is a private-purpose website for running Old School RuneScape bingo events for one Discord community. The community normally holds two or three events per year. The website replaces the current Google Sheets board and Discord evidence-submission workflow while leaving Discord as the community's communication platform.

An event may also include invited teams from outside the community, such as another clan in a clan-versus-clan bingo. Those teams may arrive with an internally selected roster and do not have to participate in the website draft.

The website will manage:

- Public event boards and live team progress
- Teams, rosters, captains, and co-captains
- Evidence submission and admin review
- Rankings, placements, and player contribution statistics
- Bingo board creation and EHB balancing
- Event drafting
- Historical events and evidence

The website is not intended to be a reusable public platform for unrelated Discord communities in version one.

## 2. Product goals

1. Make every team's official progress easy to follow in one place.
2. Make drop submission fast for captains and co-captains.
3. Give admins a clear review queue and safe correction tools.
4. Calculate tile, line, board, and ranking progress automatically.
5. Support the varied drop objectives used in real OSRS bingo boards.
6. Preserve previous events, results, rosters, boards, and approved evidence.
7. Help organizers estimate and balance boards using EHB.
8. Run the team draft on the website, initially under admin control.

## 3. Out of scope for version one

- A multi-community or public event-hosting platform
- Discord role, channel, or message automation
- Discord authentication
- Individual participant accounts
- Automated prize distribution
- RuneLite integration
- Automated verification of in-game drops
- Captain strategy tools, assignments, or private team notes
- Fully captain-operated drafting with automated turns and timers
- Automatic import being the only way to maintain OSRS data

These features may be reconsidered after the first event has been run successfully on the website.

## 4. Expected scale

A representative event has:

- 6 teams
- 14 participants per team
- Approximately 84 participants
- Usually a captain and co-captain for each team
- A 5x5 or 6x6 board
- A duration ranging from an extended weekend to approximately six days

The architecture should comfortably support modest growth, but administrative simplicity and reliability are more important than internet-scale optimization.

## 5. Roles and permissions

### 5.1 Public visitor and participant

Participants do not need website accounts. Participants and other visitors have the same website permissions.

They can:

- View the current event, rules, dates, and status
- View every team's board and approved progress
- View tile details and publicly visible approved screenshots
- View teams, rosters, captains, and the draft
- View team and player leaderboards
- View previous events and final results

They cannot:

- Submit evidence
- View pending or rejected submissions
- Change any event data

Players still exist as event records so drops can be credited to them and they can appear on rosters and contribution leaderboards.

### 5.2 Captain and co-captain

Captains and co-captains receive separate accounts tied to one team and one event. Accounts are generated automatically from finalized roster roles. The username is based on the player's in-game name plus random digits, and a generated temporary password is shown once to the admin for distribution.

They can:

- Submit evidence for their own team
- Credit the drop to a player on their team
- View their team's pending, approved, and rejected submissions
- Edit their team's pending submissions
- Withdraw their team's pending submissions
- Correct and resubmit evidence when requested by an admin
- Read admin feedback on their team's submissions

They cannot:

- Submit for another team
- See another team's pending or rejected submissions
- Approve or reverse submissions
- Directly change official tile progress
- Edit the event, board, catalogue, roster, or rules

### 5.3 Admin and reviewer

Admins use permanent accounts. An admin may also participate in the event or captain a team. Admins are trusted and may review their own team's submissions.

They can:

- Create, configure, publish, and archive events
- Create teams and manage rosters
- Add pre-formed internal or external teams before or after the website draft
- Automatically provision, disable, re-enable, reset, and expire captain accounts
- Manage bosses, activities, items, drops, rates, and EHB values
- Build and deliberately arrange bingo boards
- Operate and publish the draft
- Submit evidence for any team when necessary
- Review, approve, reject, and request corrections to evidence
- Correct submission metadata before approval
- Reverse approved submissions
- Hide an evidence image from public view
- Start, close, reopen, finalize, and unfinalize events
- Correct placements and progress
- View the complete audit log
- Manage other admins

## 6. Account lifecycle

### 6.1 Captain accounts

- Each captain and co-captain receives a separate account.
- Each account is restricted to one event and one team.
- Accounts are generated when rosters are finalized, or when a captain/co-captain role is assigned later to a finalized roster.
- Generated usernames use the player's in-game name plus random digits.
- Generated temporary passwords are shown once, stored only as hashes, and must be changed at first sign-in.
- Accounts become active when the event starts.
- When the submission cutoff passes, accounts enter correction-only mode.
- Accounts automatically disable 24 hours after the event ends.
- An admin can disable an account early.
- An admin can re-enable an account after expiry and optionally set a new expiry.
- An admin may leave a re-enabled account active until manually disabled.
- Password resets and account status changes are recorded in the audit log.

### 6.2 Admin accounts

- Admin accounts are permanent until disabled by another authorized admin.
- Admin actions that alter competitive data are recorded in the audit log.
- Strong password requirements are mandatory.
- Two-factor authentication will be evaluated during technical architecture planning.

## 7. Event lifecycle

An event has the following states:

1. **Draft:** Admins configure the event, catalogue, players, teams, rules, and board.
2. **Signup open:** The event signup page is public and accepts registrations. Teams, draft results, and the bingo board remain private.
3. **Signup closed:** Registration no longer accepts submissions while admins prepare captains, teams, the draft, and the board.
4. **Live:** The approved board and finalized teams are public. Drops obtained within the event window may be submitted.
5. **Awaiting final review:** The event has ended and the normal submission window has closed. A short grace period may remain available for evidence submission and corrections. Existing submissions can still be reviewed and corrected afterward.
6. **Finalized:** An admin confirms official placements and statistics.
7. **Archived:** The event remains publicly viewable as history.

The event does not finalize automatically. Admin confirmation is required.

### 7.1 Important timestamps

Each event records:

- Scheduled start
- Scheduled event end
- Scheduled submission cutoff
- Actual start, if manually controlled
- Actual submission closure
- Finalized time
- Captain account expiry

Admins can reopen submissions or undo finalization. These actions require a reason and create audit entries.

### 7.2 Post-cutoff behavior

The submission cutoff may be configured later than the scheduled event end to provide a short evidence-upload and correction grace period. Only drops obtained during the official event window are valid, even when their evidence is submitted during this grace period.

Admins can manually reopen submissions after the event or submission cutoff. Reopening requires an optional new cutoff, a reason, and an audit entry. Reopening submission access does not extend the valid in-game drop window unless an admin separately changes the official event end.

After the submission cutoff:

- Drops obtained after the official event end are invalid.
- Captains cannot submit newly obtained drops.
- Evidence submitted before the cutoff remains reviewable.
- Captains can make requested corrections to existing submissions.
- Public boards remain visible and are marked as awaiting final review.
- Admins finalize the event after resolving relevant submissions.

### 7.4 Final-review checklist behavior

The final-review checklist summarizes unresolved conditions. Admins should normally address the underlying records, but an edge-case override allows a blocker to be marked resolved when it cannot affect the clear result or does not require further action.

- Selecting **Pending submissions** opens the review queue filtered to the event's remaining pending submissions.
- Selecting **Changes requested** opens the event's submissions awaiting captain correction.
- Selecting **Completion-time check** opens the relevant teams, final-drop evidence, and editable obtained/completion times.
- The complete checklist row may be clickable on pointer devices, with a clearly labeled action available for keyboard and assistive-technology users.
- Opening a checklist item does not resolve it.
- A blocker normally clears automatically when its underlying count reaches zero or the required inspection/correction is completed.
- An admin may select **Mark resolved anyway**. A confirmation popup explains the unresolved condition and requires the admin to confirm that it does not affect the result.
- Manual resolution requires a reason, records the unresolved count and relevant records in the audit log, and does not alter the underlying submissions.
- Returning from the review or inspection view preserves the finalization workflow state.
- The finalization action remains unavailable while any competitive blocker remains.

### 7.3 Event creation and setup

An admin creates the event before signups, drafting, or board publication. The setup flow contains:

1. Event identity: name, description, banner/image, and URL identifier.
2. Schedule: signup opening/closing, draft time, event start/end, submission grace period, and timezone.
3. Rules: evidence requirements, verification code, ranking rules, buy-in/prize notes, and public rules text.
4. Signup form: required fields, optional questions, signup code, and participant-edit behavior.
5. Preliminary team configuration: optional estimates for team count and team size plus captain/co-captain defaults. Final values are chosen later in draft setup.
6. Preliminary board configuration: optional expected dimensions and EHB targets. Final dimensions and tile layout are controlled by the board editor.
7. Captain access: account activation and expiry settings.
8. Review and publish: validate required configuration and choose which public event information becomes visible.

The event remains a private admin draft until signups are explicitly opened. Opening the event makes only the signup page and the minimum signup information public, such as event name, signup deadline, expected event dates, and signup instructions.

Event setup is not a single-session process. Admins may create the event, open signups, and continue building or revising its private board throughout the signup and pre-draft period. The board does not need to be complete when signups open. Board validation and publication, rather than event creation or signup publication, determine when the competitive board becomes fixed and public.

The following have separate publication controls and are not exposed by opening signups:

- Participant list
- Team assignments and rosters
- Draft order or draft progress
- Finalized draft results
- Bingo board and tile details
- Captain accounts or admin-only configuration

Finalized draft results publish when the draft is finalized. The approved bingo board publishes at an admin-chosen time, normally when the live event begins.

Event-creation values for team count, players per team, and board dimensions are planning defaults rather than locked competitive configuration.

- Draft setup may change the number of teams and target team size based on confirmed participation.
- Admins may create pre-formed teams before or after the website draft and manage their rosters manually.
- A pre-formed team created before the draft is excluded from draft order, turns, team-size calculations, and draft picks unless an admin explicitly changes it to a draft-participating team before the first pick.
- A pre-formed team created after the draft joins the same event and competitive board without rewriting the completed draft history.
- The board editor may change board dimensions before board publication.
- Changes made in the draft setup and board editor become the authoritative values shown elsewhere.
- Changing team configuration after drafting has begun requires a warning because it can invalidate pick order and existing assignments.
- Changing board dimensions after tiles have been placed requires a preview of which positions or tiles are affected.
- Changing either configuration after it has been finalized or published requires an explicit admin confirmation and audit reason.

## 8. Board and ranking rules

### 8.1 Board layout

- Boards are rectangular, commonly 5x5 or 6x6.
- Event creation may contain an expected board size, but final board dimensions are configured and editable in the board editor.
- Board dimensions remain changeable before publication. When shrinking, the editor first compacts placed tiles toward the top-left while preserving their current relative order.
- If the smaller board still has enough positions for every placed tile, the compacted layout is previewed before applying it.
- If the smaller board has fewer positions than placed tiles, resizing is blocked and a popup tells the admin how many tiles must be removed first.
- Rows and columns count as lines.
- Diagonals do not count.
- A completed tile counts only once as a tile but contributes to both its row and column.
- Overlapping completed lines count as separate completed lines.
- A 5x5 board therefore has five rows, five columns, and ten possible completed lines.

### 8.2 Placement order

Ranking priority is:

1. Full-board completion, ordered by immutable submission time of the final qualifying submission
2. Most completed rows and columns
3. Most completed tiles
4. Highest configured EHB tie-break value

The first team to complete the entire board wins. The event and board remain open until the official event end and admin finalization. Other teams may continue for fun or for second- and third-place prizes.

Full-board completion is based on the immutable server submission time of the evidence that completed the final tile, not the time an admin reviewed it. Captains and admins do not edit this timestamp.

EHB is used for board estimation, line balancing, player contribution statistics, and final tie-breaking. It does not otherwise award team points.

## 9. Boss, activity, item, and drop catalogue

### 9.1 Boss or activity

A catalogue entry can represent a boss or a broader activity such as raids, skilling, or the Fortis Colosseum.

It contains:

- Name
- Image
- Category
- Active/inactive status
- Efficient kills or completions per hour, when applicable
- External data identifier, when available
- Notes
- Connected drops

### 9.2 Item

An item is a reusable record and can be connected to multiple bosses or activities.

It contains:

- Name
- Image
- Active/inactive status
- Optional external identifier
- Notes

### 9.3 Boss/activity drop

The connection between a boss/activity and an item contains:

- Display drop rate
- Optional normalized numeric probability
- Conditional-rate explanation
- Default EHB estimate
- Data source and last-updated time
- Active/inactive status

Drop rates may be conditional or unsuitable for automatic conversion. The system must support human-readable values such as `2 x 1/1,024` and allow manual notes.

Drop rate belongs to the connection between a boss/activity and an item, because the same item can have different rates from different sources. Selecting a drop for a tile therefore also selects the relevant source and its source-specific rate.

Admins can add any drop to a boss, including low-value or intentionally humorous "troll" drops. A drop does not need to be part of a unique drop table.

## 10. Tile model

### 10.1 Tile fields

A tile contains:

- Name
- Public description
- Image
- Board position
- One or more requirements
- Evidence instructions
- Estimated EHB
- Optional tie-break EHB value
- Active/published state

### 10.2 Requirement builder

The normal admin flow is:

1. Choose one or more bosses or activities.
2. Choose eligible drops from those sources.
3. Set the target contribution.
4. Choose whether duplicate drops are allowed.
5. Optionally change a drop's contribution value.
6. Optionally configure a maximum contribution for a selected drop.

Most tiles contain one requirement. A tile may contain multiple requirements when separate targets must all be met.

Example:

- Requirement A: Obtain 5 selected Barrows pieces.
- Requirement B: Obtain 5 selected Moons of Peril pieces.
- The tile completes only when both requirements are complete.

### 10.3 Duplicate behavior

Each requirement has a **Duplicates allowed** option.

- When enabled, repeated copies of the same eligible drop continue to contribute.
- When disabled, each selected drop can contribute only once unless an admin explicitly configures a different cap.

OSRS terminology must be presented carefully. A "unique drop" means an item from a unique drop table; it does not imply that duplicate copies are disallowed.

### 10.4 Contribution values

- An eligible drop contributes `1` by default.
- Each eligible drop has an optional board-defined **Higher contribution weight** setting.
- Every drop defaults to `1`. The board designer may assign a different fixed weight to individual eligible drops; for example, Theatre of Blood purples may count as `1` while Scythe of Vitur counts as `2` toward a target of `6`.
- Captains and other submitters cannot override the configured weight.
- Progress is capped at the requirement target and never carries to another tile.
- One obtained drop can contribute to only one tile.

### 10.5 Specific component objectives

For a full Voidwaker objective:

- Select the three relevant bosses.
- Select the hilt, blade, and gem.
- Set target contribution to `3`.
- Disable duplicates.

Each different component can then contribute once.

### 10.6 Manual objectives

The system should support a manual objective for tasks that cannot reasonably be represented as item drops. Manual objectives still require evidence and admin approval.

Examples include:

- Complete Theatre of Blood at four-player scale under a configured time.
- Complete an activity under a particular restriction.
- Complete a kill-count, speed, collection-log, or combat-achievement objective.

A manual objective contains its own completion description, target quantity, and evidence instructions rather than eligible item drops. Each approved completion normally contributes `1`, so an objective may require multiple completions, such as completing three Inferno runs.

## 11. EHB and board balancing

The board editor displays:

- Estimated EHB per tile
- Total estimated board EHB
- Estimated EHB for each row
- Estimated EHB for each column
- Lowest and highest line EHB
- Absolute and percentage spread between lines
- Warnings for unusually easy or difficult lines

Admins deliberately position tiles to balance rows and columns. Tiles are not randomly placed after creation.

EHB and efficient-rate data may be imported from useful external sources such as Wise Old Man where technically and legally appropriate. The preferred approach is a stable API or data endpoint rather than fragile HTML scraping.

For version one, relevant external data will be collected in a one-time import during initial setup. All imported values remain editable by admins. Automatic or repeat synchronization may be considered later.

For a simple single-drop requirement, expected EHB is calculated from a reviewed source-specific probability and efficient-completion-rate pair. For example, at 100 kills per hour and a `1/1,000` drop rate, the expected time for one qualifying drop is 10 EHB. Group content may pair an in-name probability with the relevant team completion rate, or a full-contribution probability with a rate normalized per invested player-hour; team size is applied exactly once. Catalogue rates may use numerators other than one and explicitly record per-completion rolls. Team, raid-scale, purple-table, points, and contribution assumptions are resolved before entry and retained as explanatory notes rather than calculator exceptions.

Complex requirements are estimated from possible completion outcomes rather than by blindly averaging bosses or adding rates. Weighted drops advance by their configured contribution, duplicate-restricted requirements track shared item identities, and alternative sources are chosen according to the lowest expected remaining person-hours. Multiple objectives are estimated separately and added. Missing or ambiguous rate mechanics require an administrator EHB override; they are never guessed.

Published boards store a snapshot of the rates, EHB values, and calculations used at publication time. Updating the central catalogue must not rewrite historical boards.

## 12. Evidence submission

### 12.1 Submission contents

Each submission represents:

- One kill or reward event
- One obtained drop
- One screenshot
- One tile requirement
- One credited player
- One team, derived from the captain account

It records:

- Event and team
- Tile and requirement
- Boss/activity
- Drop
- Credited player
- Credited weight copied from the board-requirement snapshot, defaulting to `1` and not editable by the submitter
- Total approved contribution, capped by the remaining requirement progress and confirmed by an admin
- Submitted time, generated automatically by the server and immutable
- Screenshot
- Optional captain note
- Status and admin feedback

Captains cannot select a player who is not on their event team.

### 12.2 Evidence expectations

A normal screenshot should show:

- Player name
- Game message identifying the drop
- Timestamp overlay when required by event rules
- Event-specific verification code when enabled by event rules

The verification code is an event setting with two modes: enabled or disabled. Admins normally enter a custom fun code, may generate one, and may activate a replacement immediately or schedule it. Every submission snapshots the code interval active at its immutable server submission time. Review is visual only; there is no OCR. An admin may approve a mismatch as an explicit exception.

The system does not store a captain-entered obtained time. The immutable server submission time is authoritative for ordering. Screenshot timestamps remain visual evidence that an admin may consider during an exceptional final tie review, without changing the stored submission timestamp.

### 12.3 Validation

Before accepting a submission, the system checks that:

- The captain account is allowed to submit or correct evidence.
- The selected tile and drop are valid for the event.
- The selected player belongs to the team.
- The submission contains one image.
- The contribution does not exceed the remaining allowed progress.
- Duplicate and per-drop cap rules are respected.
- The same approved submission cannot be allocated to multiple tiles.

## 13. Submission review lifecycle

Submission states are:

1. **Pending:** Awaiting admin review; editable and withdrawable by the team's captains.
2. **Changes requested:** Admin has requested a correction or replacement.
3. **Approved:** Contribution has been applied to official progress.
4. **Rejected:** Does not count; includes an admin reason.
5. **Withdrawn:** Removed by a captain before approval.
6. **Reversed:** Previously approved, then removed by an admin.

### 13.1 Admin review actions

An admin can:

- Approve
- Reject with a reason
- Request changes with a required explanatory note
- Mark as a duplicate submission
- Correct metadata and approve
- Hide the screenshot from public view
- Reverse a previous approval

Editable metadata includes:

- Tile or requirement
- Credited player
- Boss/activity
- Drop
- Contribution
- Immutable submission time
- Public image visibility

All corrections store the original and new values in the audit log. An admin should not silently replace the submitted evidence image. Replacement evidence should come from a captain or be recorded as a clearly identified admin attachment.

### 13.2 Reversal

Reversing an approval:

- Deducts the exact contribution created by that submission
- Recalculates tile, row, column, board, leaderboard, and placement state
- Reallocates newly available capacity to later approved evidence up to its original eligible claim
- Records the admin, time, and reason
- Preserves the submission and its history

## 14. Evidence visibility

- Pending, changes-requested, withdrawn, and rejected evidence is visible only to admins and captains of that team.
- Approved evidence is publicly visible from the relevant tile.
- Other teams do not see pending progress.
- Admins may hide an approved screenshot from public view for privacy or sensitive content.
- Captains may request public privacy when submitting or correcting evidence. Approval then hides the screenshot and credited player automatically.
- Admins may restore visibility when a privacy request is unnecessary or was selected by mistake.
- A hidden screenshot still counts and remains visible to admins.
- When an approved screenshot is hidden, the credited player's identity is also hidden everywhere outside the admin submission view. This includes the public tile view and views available to other teams' captains.
- Public viewers see that qualifying evidence exists but that its image and player identity were hidden by an admin.

The public tile view should show approved drop, player, team, submission time, contribution, and evidence unless hidden.

## 15. Progress calculations

When evidence is approved or reversed, the system recalculates:

- Requirement progress
- Tile completion
- Row completion
- Column completion
- Full-board completion
- Team ranking and placement
- Player contribution statistics

Pending evidence does not count toward official progress or standings.

When a team first reaches full-board completion, it is visibly marked as a finisher. The first finisher is marked as the provisional winner, but official placements remain subject to outstanding reviews and admin finalization.

## 16. Player contribution leaderboard

Because captains submit on behalf of players, every submission must credit the player who received the drop.

The leaderboard may display:

- Estimated EHB contribution
- Approved qualifying drops
- Approved submissions
- Tile-finishing drops
- Team

The primary player ranking is estimated EHB contribution, matching the community's existing practice. Team and player EHB use the same allocation: each approved contribution receives its proportional share of the tile's combined expected EHB snapshot. Completing a tile credits exactly its expected EHB and completing a board credits exactly the board's expected EHB. Do not sum standalone time-to-specific-drop values for alternative eligible drops, because the same kills roll those alternatives together. The product must label this clearly as an estimate; it measures credited expected objective effort rather than actual time played.

## 17. Drafting

### 17.1 Version-one draft flow

Admins can:

- Create or import the participant list
- Mark players who volunteered as captain candidates
- Select captains and co-captains
- Create teams
- Set or change the final number of teams and target team size before starting the draft
- Randomly scramble the participating teams to determine the initial picking order
- Run a snake draft, reversing pick order on alternating rounds
- Assign drafted players to teams
- Undo an incorrect pick
- Pause and resume draft entry
- Finalize and lock rosters
- Automatically publish the completed draft board and team rosters when the draft is finalized
- Keep every participant visible throughout the draft, including already drafted players
- Mark drafted players with their assigned team instead of removing them from the participant list
- Sort the participant list by EHB by default while allowing admins to sort by player name, signup time, or draft status

Captains make their selections through the community's normal voice or text communication. An admin records the selections on the website.

Before finalization, the draft board and team assignments are visible only to admins in version one. Captains and public visitors do not see draft state on the website until it is finalized and automatically published.

The full participant pool remains visible during drafting so captains can maintain an overview. Available players are visually distinct from drafted players, and the current team's turn is clearly identified.

Team count and target size shown during initial event creation are only estimates. Draft setup calculates and displays the effect of the final values on roster sizes and any remainder before the draft begins.

### 17.2 Concurrent administration

- Board editing uses optimistic concurrency. Each editing form is tied to the version of the complete board aggregate it loaded, so tile and layout changes cannot silently cross.
- If another admin saves a newer version first, a stale save is rejected instead of overwriting it. The admin is told to reload and review the newer content.
- Opening the board is view-only by default so several administrators can inspect it together during voice discussions.
- An administrator explicitly enters edit mode and receives one renewable editing lease. Other administrators remain live viewers and see who is editing.
- Editing control can be released or explicitly taken over after confirmation. Navigating away attempts to release it immediately; it otherwise renews only through actual board activity and expires after five inactive minutes, including when an editor simply leaves the tab open.
- A running draft has one active controller with server-enforced authority to start, pick, undo, pause, resume, and finalize.
- Other administrators can follow the draft in a live read-only view.
- An administrator can explicitly take over control after a confirmation. The transfer is audited and immediately removes write authority from the previous controller.
- Concurrent requests must never create two picks for one turn, assign one participant twice, or silently overwrite board content.

### 17.3 Deferred draft enhancements

- Captain-controlled picks
- Automated turns
- Draft timers
- Alternative draft-order systems
- Automatic turn advancement
- Player availability notes

## 18. Signups and participant import

Signups were previously collected through Google Forms and stored in a Google Sheets response sheet. Version one will move the primary signup workflow onto the bingo website.

### 18.1 Website signup

Admins configure and publish a signup form for an event. Public participant accounts are not required.

Each event has a configurable signup window:

- Signup opening date and time
- Signup closing date and time
- Event timezone
- Maximum number of confirmed participants

Admins can extend or shorten the signup window and increase the participant cap. The participant cap cannot be lowered. Changes are recorded in the audit log. The public signup page clearly shows the closing time, capacity, confirmed signup count, and whether new submissions currently enter the waiting list.

The standard form contains:

- Primary OSRS in-game name, required
- Optional second OSRS account name
- EHB snapshot, required unless the website can retrieve it successfully from the configured data source
- Free-text comments for availability, scheduling limitations, and team requests
- Captain-volunteer choice
- Optional Discord name or ID
- Event-specific custom questions

To limit unwanted submissions, an admin may protect the form with an event-specific signup code distributed through Discord. Admins can close and reopen signups at any time.

After submitting, the participant receives a private edit link or confirmation code so they can correct their signup without creating an account. Admins can edit, invalidate, or remove submissions. For security, an existing private link cannot be displayed again. An admin can create a replacement link, which immediately invalidates the previous link and displays the replacement once for copying.

The system records signup time automatically. Buy-in/payment status is not collected from the public participant form; it is managed by admins on the participant record.

Custom questions support common field types such as short text, long text, yes/no, and single choice. Admins may change custom questions whenever signups are closed, until the draft starts. Answers remain attached to that event's signup record even if the form changes later.

Signup forms may therefore differ between participants in the same event. Admin and public team/roster views must treat every custom answer as optional historical data. A participant who signed up before a question was added has no answer record for that question; the page must show a neutral fallback such as **Not answered** and must never fail because an answer is missing.

Required system fields such as primary account and EHB cannot be removed from the form when they are necessary for drafting and balancing. Optional custom questions can be added, reordered, edited while signups are closed and the draft has not started, or disabled for future submissions.

### 18.2 Capacity and waiting list

Signup records have one of these participation states:

- **Confirmed:** Within the current participant cap and eligible for the draft.
- **Waiting list:** Valid signup received after the confirmed-participant cap was reached.
- **Withdrawn:** Participant withdrew or an admin recorded that they can no longer participate.
- **Removed:** Admin removed an invalid, duplicate, or otherwise ineligible signup.

When the confirmed-participant cap is reached, the signup form remains open until the signup deadline. Additional valid signups join the waiting list in signup-time order.

The public confirmation page tells a participant whether they are confirmed or waiting-listed. A waiting-listed participant can see their current waiting-list position through their private signup link. The public event page shows the number of people waiting but does not expose their identities.

When an admin increases the participant cap:

- The earliest waiting-listed participants are promoted automatically until the new capacity is filled or the waiting list is empty.
- Promotion order uses the original valid signup timestamp.
- Promoted participants retain their original signup data and edit link.
- Each promotion is recorded in the audit log.

Example: if the cap is 50 and seven people are waiting, increasing the cap to 60 automatically confirms all seven and leaves three additional confirmed places available.

When a confirmed participant withdraws or is removed before the draft is locked, the earliest waiting-listed participant is automatically promoted into the open place.

Admins can remove or withdraw participants because they can no longer attend, signed up twice, did not pay a required buy-in, or are otherwise ineligible. The action requires an admin reason. The record remains auditable instead of being permanently erased through the normal interface.

Automatic waiting-list promotion normally stops once the draft is locked. After that point, replacements and roster changes require an explicit admin action so teams are not changed unexpectedly.

### 18.3 CSV fallback

CSV import remains an admin fallback for migrating existing Google Forms responses or performing bulk registration.

Version one uses a downloadable, documented CSV template with stable system columns rather than attempting to infer any arbitrary spreadsheet automatically.

The import flow should:

- Download the current event's CSV template.
- Upload a completed CSV file.
- Show a preview before making changes.
- Require the template's primary-account and EHB columns.
- Accept optional second-account, comments, captain-volunteer, Discord identity, payment-status, and custom-answer columns.
- Ignore unknown additional columns only after warning the admin.
- Reject the import when a required system column is missing, while explaining how to correct it.
- Detect likely duplicate players.
- Show invalid or missing required values.
- Allow rows to be corrected, skipped, or imported.
- Record the import and source filename in the audit log.

The example Google Forms response sheet contains the following source columns:

| Source column | Intended imported field |
| --- | --- |
| `Tidsstempel` | Signup timestamp |
| `In game Name` | Primary OSRS account |
| `EHB - ses inde på` | EHB at signup |
| `Kommentarer` | Availability, requests, or other organizer notes |
| `Hvad hedder din 2. Account` | Optional second OSRS account |
| `Betalt buyin` | Buy-in/payment status |
| `Kolonne 1` | Signup/order number, subject to confirmation during import |

Imported EHB is a signup-time snapshot used to inform drafting and team balancing. It does not replace the separate boss/activity EHB data used to estimate tile difficulty.

Buy-in/payment status is sensitive administrative data. It is visible to admins but should not be shown on public participant lists, draft results, or team rosters.

Free-text signup comments are admin-only before and during drafting by default because they may contain personal schedules or requests. An admin can use them while forming teams without publishing them.

The legacy Google Forms headings listed above can be handled by a one-time migration mapping, but future imports should use the website's template.

## 19. Page inventory

### 19.1 Public pages

- Current event overview
- Event signup form and signup confirmation/edit page
- Bingo board with team selector
- Tile details and approved evidence
- Team overview and roster
- Published draft results
- Team leaderboard
- Player contribution leaderboard
- Event rules and evidence requirements
- Previous events and results

### 19.2 Captain pages

- Submit evidence
- Team submissions
- Pending submissions
- Changes requested
- Rejected, withdrawn, and approved submission history

### 19.3 Admin pages

- Review queue
- Event editor
- Guided event creation and setup
- Signup form builder and signup manager
- Board builder and EHB balancing view
- Tile and requirement editor
- Boss/activity catalogue
- Item and drop catalogue
- EHB/rate import and change review
- Event participant manager
- Team and roster manager
- Captain account manager
- Draft control
- Participant CSV import and validation
- Submission corrections
- Results and placement finalization
- Audit log
- Admin account manager

## 20. Audit requirements

The audit log records at minimum:

- Submission creation and edits
- Review decisions and reasons
- Metadata corrections
- Approval reversals
- Manual progress or placement corrections
- Event state changes
- Submission reopening and event unfinalization
- Board changes after publication
- Captain account activation, expiry, disabling, and re-enabling
- Catalogue imports and manual overrides

Audit entries identify the acting account, time, affected record, previous value, and new value where applicable.

Historical audit data must not be casually deletable through the normal admin interface.

## 21. User experience direction

- The bingo board is the main visual focus.
- The default public board experience uses an overview-and-zoom interaction inspired by macOS Mission Control.
- The overview displays every team's board as a compact preview together with its team name, rank, completed tiles, completed lines, and full-board status.
- Selecting a board expands it into the primary full-size board view for that team.
- The full-size view provides a clear return to the all-team overview and quick previous/next team navigation.
- The transition should preserve spatial context so users understand which team board they opened, while remaining optional or reduced for users who prefer reduced motion.
- A board's overview preview shows progress and status but does not need to make every tile label readable. Detailed tile content belongs to the expanded board.
- OSRS boss and item PNGs supply most of the game identity.
- The surrounding interface should be readable and modern rather than copying the OSRS interface exactly.
- Status colors must be consistent for not started, in progress, pending, completed, rejected, and reversed states.
- Desktop should support whole-board viewing, board editing, and rapid evidence review.
- Mobile should prioritize evidence submission, individual tile details, and standings.
- On small screens, the board may become a scrollable grid or readable tile list rather than shrinking into illegibility.
- On mobile, the overview becomes a vertical or two-column collection of team board previews. Opening a preview navigates to the full team board instead of relying on a desktop-style zoom animation.
- Important status information must not rely on color alone.

### 21.1 Board-builder interaction

- On desktop, admins arrange the board by dragging one tile onto another; dropping swaps their positions and immediately recalculates row and column EHB.
- Dragging must not be the only arrangement method. Keyboard users and touch devices need an accessible select-and-swap or move control.
- Hovering or focusing a row or column in the EHB balance summary highlights the corresponding tiles on the board.
- Hovering or focusing a row/column EHB total next to the board provides the same highlight.
- The highlight must identify the row or column with more than color alone, such as a visible outline and label.
- Tile editing should preserve board context. On wider screens, the preferred interaction is a side drawer or inspector alongside the board so admins can see how edits affect EHB totals.
- On smaller screens, the tile editor may become a full page or full-width sheet.
- A centered blocking modal is reserved for short confirmations and destructive actions rather than the complete tile editor.

Detailed visual design and wireframes will be produced after this requirements document is reviewed.

## 22. Non-functional requirements

### 22.1 Reliability and data integrity

- Approved progress must be reproducible from the submission ledger.
- Reversals must restore the correct previous state.
- Concurrent admin reviews must not apply one submission twice.
- Historical event calculations must use their published snapshots.
- Image upload failure must not create an incomplete submission.

### 22.2 Security

- Public access is read-only.
- Captain authorization is enforced on the server, not only hidden in the interface.
- Passwords are stored using an appropriate modern password hash.
- Uploads are validated and served safely.
- Sensitive admin actions require authorization and auditing.
- Rate limiting protects login and upload endpoints.

### 22.3 Performance

- Public boards and team switching should feel immediate at expected event scale.
- Approval should update official progress without requiring a manual spreadsheet refresh.
- Large screenshots should be resized or optimized for viewing while preserving original evidence when needed.
- Expected event scale includes 100 simultaneous connected viewers, not only 100 registered participants.
- Live updates should avoid synchronized full-page reloads across all connected viewers.
- External integrations such as Wise Old Man must be cached or synchronized in the background rather than called once per viewer request.
- The release candidate must pass a production-sized load rehearsal covering public viewing, SignalR updates, evidence uploads, evidence viewing, and admin review.

### 22.4 Accessibility and compatibility

- Core workflows support current desktop and mobile browsers.
- Controls are usable by keyboard.
- Text and controls meet reasonable contrast standards.
- Images include meaningful alternative text where appropriate.

### 22.5 Backup and recovery

- Event data and submission metadata require regular backups.
- Evidence images require durable storage and a recovery plan.
- Recovery procedures should be tested before a live event.

## 23. Version-one acceptance criteria

Version one is ready for a live event when:

1. Admins can create an event, teams, players, captain/co-captain assignments, and a board; captain accounts are generated automatically.
2. Admins can model every objective on the provided example 5x5 board without custom code.
3. The editor calculates tile, line, and total EHB while arranging the board.
4. Captains can submit one screenshot and drop for a player on their team.
5. Captains cannot submit for or inspect pending evidence from another team.
6. Admins can approve, reject, request changes, edit metadata, and reverse approvals.
7. Approval and reversal correctly update all related progress and standings.
8. Public visitors can inspect all teams, completed tiles, and non-hidden approved evidence.
9. Full-board finish time and provisional placement use immutable server submission time.
10. Submissions close at the cutoff while existing evidence remains reviewable.
11. Event finalization requires an admin action.
12. Captain accounts disable 24 hours after the event ends and can be re-enabled.
13. Admins can operate a private snake draft and publish its completed results.
14. Admins can create and publish an event and its website signup form.
15. Participants can submit and privately edit a signup without creating an account.
16. Signups received within the cap become confirmed and later valid signups enter an ordered waiting list.
17. Increasing capacity or freeing a place automatically promotes waiting-listed participants in signup-time order before the draft is locked.
18. Admins can extend the signup window, change capacity, and remove or withdraw participants with an auditable reason.
19. Admins can import and validate participants using the documented CSV template.
20. Finalized events remain publicly viewable in event history.
21. Competitive corrections and administrative changes are auditable.
22. Concurrent board edits cannot silently overwrite one another.
23. Only the active draft controller can mutate a running draft; other admins can observe or explicitly take over with an audit trail.

## 24. Decisions deferred to later planning

The following do not block requirements approval:

- Frontend and backend technology stack
- Database and hosting provider
- Image storage provider and retention policy
- Exact backup schedule
- Whether Wise Old Man data is available through a suitable API
- Which OSRS Wiki data should be imported and how often
- Exact visual theme, typography, and board styling
- Detailed wireframes and interaction design
- Whether two-factor authentication is mandatory for admins
- Exact EHB formula and rounding behavior
- Exact tie procedure if screenshot evidence cannot establish obtained order

## 25. Next planning deliverables

No application code should begin until the following planning work is reviewed:

1. Review and correct this product requirements document.
2. Produce low-fidelity wireframes for the public board, tile detail, captain submission, admin review queue, board builder, and draft pages.
3. Define the detailed data model and calculation rules using representative tiles from the upcoming bingo.
4. Define the technical architecture, hosting, authentication, storage, backup, and deployment approach.
5. Divide version one into implementation milestones with tests and acceptance checks.
