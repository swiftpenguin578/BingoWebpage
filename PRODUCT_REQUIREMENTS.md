# OSRS Community Bingo Platform

## Product Requirements Document

**Status:** Planning Pass 2 target requirements v0.3; selected capability decisions recorded
**Last updated:** 2026-07-25
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
- Automated prize distribution
- RuneLite integration
- Automated verification of in-game drops
- Captain strategy tools, assignments, or private team notes
- Fully captain-operated drafting with automated turns and timers
- Automatic import being the only way to maintain OSRS data

These remaining features may be reconsidered after the first event has been run successfully on the website. Discord authentication and participant accounts were moved into the active Planning Pass 2 functional expansion.

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

**Planning Pass 2 identity direction:** The website account is the durable person/actor identity. Public username/password remains its required authentication method after onboarding; an optional current Discord association provides another method. Event participation and OSRS character names remain separate records. One website account may participate once per event and in many different events; it may register several OSRS characters, and the same character may be linked to several website accounts because borrowing and swapping are legitimate. Within one event, however, each OSRS character is assigned to exactly one participant. Never infer website authority or exclusive ownership from an OSRS character name.

### 5.1 Public visitor and participant

Public visitors do not need accounts and retain read-only access to public information.

Public visitors can:

- View the current event, rules, dates, and status
- View the event's signup board and selected public fields before draft finalization; afterward non-admin requests for that route redirect to the published team-roster view
- View every team's board and approved progress
- View tile details and publicly visible approved screenshots
- View teams, rosters, captains, and the draft
- View team and player leaderboards
- View previous events and final results

Public visitors cannot:

- Submit evidence
- View pending or rejected submissions
- Change any event data

Initial website-account creation requires Discord authentication, but Discord-server membership is not required. Onboarding adds a required password. Returning users may authenticate with Discord or public username/password, and either method opens the same website account. That account owns at most one event-participant record in the event. Participants manage their signup through the account only while signup is open and do not receive a private edit link for a new normal signup.

The first account journey after Discord authentication requires the user to enter their exact OSRS account name as a unique public website username plus a password. The page states that spelling must be correct, but the application trusts the entry and does not verify OSRS syntax, current availability, ownership, Wise Old Man membership, or existence. It trims surrounding whitespace and compares names case-insensitively for website uniqueness without otherwise rewriting the submitted spelling. Username and OSRS character are separate records created from the same initial input: the username becomes both the public site label and password-login username, while the character is created/linked and marked preferred in My accounts. Neither the username nor link proves character ownership. Public-username uniqueness does not prevent another website account from linking or legitimately borrowing the same OSRS character; it only prevents two public profiles from presenting or logging in with the same username.

A participant may later change their public username to another linked OSRS character if the normalized public name is available. Open event signups update to the new username. Signup records already closed, drafted, live, or historical retain their previous public-username snapshot.

An authenticated participant can:

- View and manage their own open signup
- View their event and team access
- Submit evidence for themselves when the event/evidence workflow allows it
- View, edit, or withdraw their own pending evidence through cutoff and read their own rejection feedback
- Manage a flexible global **My accounts** list with optional personal labels and one preferred character
- Register several available characters for an event while keeping exactly one active and drop-eligible at a time
- Answer additional admin-configured Account or support-alt questions when present
- Swap without limit among the playing accounts locked into that event signup
- Withdraw through a separate confirmed action after signup closes and before the draft begins

Event-participant records still exist independently so admins can create or import external/pre-formed rosters whose members do not need public signups or website access.

### 5.2 Captain and co-captain

Captain and co-captain are event/team roles on the participant's existing website account. Assigning or removing the role changes permissions without creating a separate normal login and without depending on an OSRS character name or current Discord ID.

They can:

- Before/during the website draft, view the confirmed draft-pool signup table with participant-submitted answer columns expanded
- Submit evidence for their own team
- Credit the drop to a player on their team
- View their team's pending, approved, and rejected submissions
- Edit their team's pending submissions
- Withdraw their team's pending submissions
- Read admin feedback on their team's submissions
- Highlight tiles, rows, and columns as non-authoritative team focus

They cannot:

- Submit for another team
- See another team's pending or rejected submissions
- Approve or reverse submissions
- Directly change official tile progress
- Edit the event, board, catalogue, roster, or rules

Captain and co-captain have the same website permissions. Their expanded draft table includes participant-submitted answers hidden from the public board but excludes paid/unpaid status, private admin notes, identity-recovery/security data, and audit history. External/pre-formed-team captains do not see the internal draft pool.

At draft finalization, the signup page remains a signup page and stays available to enabled administrators for historical and operational use. Public, participant, and captain requests for that route redirect to published team rosters; roster pages never expose the old signup answers, including a team's expanded draft answers.

After event end and before submission cutoff, captains/co-captains may continue creating and editing evidence submissions for in-window drops, including on behalf of unlinked external teammates. Submission cutoff closes those mutations unless an admin reopens submissions.

Team focus may target a tile, full row, or full column. It is shared by that team's captains, visible to current team members, and invisible to opponents, the public, and ordinary admins who are not team members. A participating Super Admin sees their own team's focus normally. Another team's focus stays hidden until the Super Admin explicitly enables a visibly identified, read-only inspection mode for that team; the data is not loaded before opt-in and the choice is not retained as a persistent show-all preference. Super Admin status never grants focus editing without the target team's captain/co-captain role. Focus has no effect on official board state, evidence, progress, or ranking.

### 5.3 Admin and reviewer

Admins use permanent accounts. An admin may also participate in the event or captain a team. Admins are trusted and may review their own team's submissions.

They can:

- Create, configure, publish, and archive events
- Cancel populated events that will not take place before live play begins
- Create teams and manage rosters
- Add pre-formed internal or external teams before or after the website draft
- Assign, revoke, and recover website-account captain/co-captain roles
- Explicitly provision, enable, reset, disable, and expire emergency captain credentials when required
- Manage bosses, activities, items, drops, rates, and EHB values
- Build and deliberately arrange bingo boards
- Operate and publish the draft
- Submit evidence for any team when necessary
- Review, approve, and reject evidence
- Correct submission metadata before approval
- Reverse approved submissions
- Start, close, reopen, finalize, and unfinalize events
- Correct placements and progress
- View the complete audit log
- Disable/re-enable normal User accounts; Admin-account access remains Super-Admin-only

Ordinary admins cannot grant or revoke the global Admin role. That owner-level action belongs only to the designated Super Admin.

### 5.4 Super Admin

The product has exactly one active global owner-level role distinct from ordinary event administration. Super Admin is assigned to a normal website account and inherits ordinary Admin capabilities. The initial owner is selected during controlled setup or migration; first public signup, first login, and onboarding can never claim ownership.

Only the Super Admin can grant or revoke ordinary Admin access, and any normal website account may become an Admin. Emergency/legacy captain credentials cannot become Admin or Super Admin. These global roles remain independent of event participation, Discord-link state, and captain/co-captain membership.

Admin grant/revoke uses an explicit confirmation view naming the account and before/after role, plus automatic actor/time/target/before-after history, but no typed username or written reason. Revocation returns the target to `USER`; it does not disable the account or remove event participation, captain roles, characters, or history. Grant and revoke invalidate the target's existing authenticated sessions immediately. The current owner cannot be demoted or disabled through ordinary role management.

Super Admin ownership can move only through an atomic transfer to another active normal website account. The recipient becomes Super Admin and the former owner becomes ordinary Admin in the same transaction, preserving the invariant that exactly one Super Admin exists. Transfer requires the current owner's password and the typed destination public username, plus automatic history but no written reason. It invalidates affected sessions. Lost-owner recovery is an explicit deployment/operator procedure rather than a first-user election or second owner.

Cross-team focus access is an exceptional support view, not passive visibility. Another team's focus is absent by default until the Super Admin explicitly enables read-only inspection for that team. The UI must identify inspection clearly, keep it team/page-session scoped, avoid loading or broadcasting the data beforehand, and remove it when disabled. Editing still requires an ordinary captain/co-captain role on the target team.

## 6. Account lifecycle

### 6.1 Captain accounts

- Normal captain/co-captain access uses the participant's website account and event/team role, regardless of whether the current session used Discord or password.
- Role scope is restricted to one event and team and changes when the authoritative roster role changes.
- The same person may have different roles in different events.
- Character-name changes and active-character swaps do not alter the person's role.
- Submission cutoff and correction rules restrict allowed actions, not the underlying Discord identity.
- Temporary password-based captain accounts remain available only as an explicitly enabled emergency fallback and are disabled by default.
- Emergency credentials are individual rather than shared identities. Any enabled Admin may create several for one event/team, each with its own globally unique login username.
- Creation exposes one hashed, single-use setup link valid for 60 minutes so the intended captain chooses their own password. Reset supersedes any prior setup/reset link and uses the same one-time flow; the Admin never chooses or sees the lasting password.
- Setup does not enable access. An Admin must explicitly enable the initialized credential, and submission cutoff disables it automatically without silently re-enabling it if submissions reopen.
- Emergency-account creation, setup/reset, enablement, disablement, expiry, and use are audited.

### 6.2 Admin accounts

- Normal Admin and Super Admin access belongs to normal website accounts; emergency password credentials cannot receive either role.
- Admin access remains until revoked by the Super Admin.
- Admin actions that alter competitive data are recorded in the audit log.
- Global role grant, revoke, and ownership transfer use strong confirmation and automatic history.
- Admin revocation increments the account authorization version and invalidates current authenticated sessions.

Normal website-account disabling is separate from role revocation. An enabled Admin may disable a `USER`; only the Super Admin may disable or restore an `ADMIN`. Nobody may disable their own current account or the active Super Admin. Disable requires strong confirmation and a reason, immediately invalidates every session, and preserves all event roles, participants, characters, evidence, historical snapshots, username reservation, and Discord association. A disabled account cannot authenticate and its identifiers cannot be registered by another account. Re-enable requires confirmation and automatic history but no written reason; it does not restore authority that event lifecycle or membership rules have ended.

Version one provides neither website-account merging nor permanent website-account deletion. Development identities are removed through the approved clean reset/deployment policy rather than account-level deletion.

### 6.3 Normal account sign-in and recovery

- Every normal account is initially created through Discord, then requires a unique public username and password during onboarding.
- First-time Discord authentication creates only protected onboarding state valid for 15 minutes; no website account is persisted until the complete onboarding transaction succeeds.
- Discord and public-username/password login authenticate the same account. Changing the public username changes the password-login username and is explicitly confirmed.
- Passwords require at least 10 characters, permit passphrases and printable characters, and have no composition or periodic-expiry rule.
- A non-persistent sign-in ends with the browser session and has a 12-hour maximum ticket lifetime. **Remember me** has a 30-day absolute maximum that activity cannot extend indefinitely.
- Version one does not collect email for recovery.
- The Forgot password page is static, asks for no username, and directs the person to contact an administrator. After verifying them outside the site, an Admin may generate a single-use reset link valid for 60 minutes for a normal user; only the Super Admin may generate one for another Admin.
- A Super Admin recovers through their linked Discord account or the operator-controlled recovery procedure.
- Admins cannot inspect or choose the replacement password. Reset-link generation/use is automatically recorded without requiring a written reason, and completing a reset invalidates previous password sessions without unnecessarily invalidating a separately Discord-authenticated session.
- A person may unlink Discord at any time after fresh password confirmation and continue using public username/password. They may later link an unused Discord account, or change directly from the current Discord account to another unused one, after password confirmation and OAuth.
- Link, unlink, and change invalidate other sessions and preserve all event participants, roles, signups, My accounts links, evidence, and history. Super Admin receives an operator-recovery warning before unlinking but may proceed.

## 7. Event lifecycle

An event has the following states:

1. **Draft:** Admins configure the event, catalogue, players, teams, rules, and board.
2. **Signup open:** The event signup page is public and accepts registrations. Teams, draft results, and the bingo board remain private.
3. **Signup closed:** Registration no longer accepts submissions while admins prepare captains, teams, the draft, and the board.
4. **Live:** The approved board and finalized teams are public. Drops obtained within the event window may be submitted.
5. **Awaiting final review:** The official event/drop-eligibility window has ended. A separately configured submission grace period may remain open for in-window evidence, while existing submissions can continue through review.
6. **Finalized:** An admin confirms official placements and statistics.
7. **Archived:** The event remains publicly viewable as history.
8. **Cancelled:** A populated pre-live event that will not take place is preserved without continuing scheduled or participant activity.

The event does not finalize automatically. Admin confirmation is required.

Only one current/public operational event may exist in production across Signup open, Signup closed, Live, Awaiting final review, or Finalized. Unlimited private drafts and archived/cancelled history are allowed. Normal transition commands enforce this and identify the blocking event. Development-only seeded scenarios and automated fixtures may deliberately contain several labelled lifecycle states; they use explicit IDs/slugs and never weaken production commands.

A finalized event remains the current event until an admin archives it. Archiving requires strong confirmation but no written reason, preserves all public URLs and official snapshots, and moves the event to previous events as read-only history. Unfinalizing archived results uses the existing exceptional reasoned correction workflow and is blocked if another production current event exists.

Discard remains restricted to empty experimental events. A populated event that has never entered Live may instead be cancelled after strong confirmation and a required reason. Cancellation is terminal through the ordinary UI, closes signup and all event-scoped mutations, suppresses scheduled transitions, and preserves all records. It never publishes private content. If the event was already public, its existing route shows a generic cancellation status while the written reason remains private admin/audit information. Live events use early end and finalization instead.

### 7.1 Important timestamps

Each event records:

- Scheduled signup opening, when automatic opening is used
- Signup closing
- Optional informational draft time
- Scheduled start
- Scheduled event end
- Scheduled submission cutoff
- Actual start, if manually controlled
- Authoritative actual/effective event end
- Actual submission closure
- Finalized time

Admins can reopen submissions or undo finalization. These actions require a reason and create audit entries.

Schedule is optional when saving an initial private draft. Before signup opens, the event requires a future signup closing time plus valid event start and end times. Event end must follow event start, and signup closing cannot be later than event start.

Signup may open automatically at a configured instant or manually. Manual opening records the opening time as now. It preserves a valid explicit future closing time; when closing is absent, the system supplies the earlier of three months after opening and event start and shows it before confirmation. An invalid explicit closing time blocks opening rather than being silently replaced.

Submission cutoff defaults to 30 minutes after event end, may be edited, and cannot be earlier than event end. Draft time is optional planning information and never starts the draft automatically; an admin explicitly starts it.

The configured event-start instant may trigger an automatic start attempt, but never bypasses readiness. If the draft is not finalized, the board is not published, a team lacks its required Captain/emergency access, or another start invariant fails, the event remains pre-live and displays **Automatic start postponed** with the exact blockers. The scheduled instant remains historical and live eligibility is never backdated. Clearing the blockers does not trigger a delayed automatic retry; an admin uses **Start event now** with strong confirmation. No written reason is required after the scheduled instant, while an early start before it requires one.

The event ends automatically at its configured event-end instant and enters `AWAITING_FINAL_REVIEW`. If processing occurs late because the application was unavailable, the configured instant remains the effective end. An enabled administrator may end a live event early after strong confirmation and a required reason; the early confirmation time becomes the authoritative end while the original schedule remains visible. Ending early does not silently alter the separately configured submission cutoff.

### 7.2 Post-cutoff behavior

The submission cutoff may be configured later than the scheduled event end to provide a short evidence-upload grace period. Only drops obtained during the official event window are valid, even when their evidence is submitted during this grace period.

The application validates immutable server submission time against the active upload cutoff. The screenshot itself is the evidence of when the drop was received: administrators visually compare its clan-event plugin UTC timestamp with the authoritative event end and relevant active-character history. The application does not request a second typed drop time, use OCR, or encode a fixed “upload within N hours” rule.

Admins can manually reopen submissions after the event or submission cutoff. Reopening requires an optional new cutoff, a reason, and an audit entry. Reopening submission access does not extend the valid in-game drop window unless an admin separately changes the official event end.

After the submission cutoff:

- Drops obtained after the official event end are invalid.
- Captains cannot submit newly obtained drops.
- Evidence submitted before the cutoff remains reviewable.
- Public boards remain visible and are marked as awaiting final review.
- Admins finalize the event after resolving relevant submissions.

### 7.4 Final-review checklist behavior

The final-review checklist summarizes unresolved conditions. Admins should normally address the underlying records, but an edge-case override allows a blocker to be marked resolved when it cannot affect the clear result or does not require further action.

- Selecting **Pending submissions** opens the review queue filtered to the event's remaining pending submissions.
- Selecting **Completion-time check** opens the relevant teams, final-drop evidence, and editable obtained/completion times.
- The complete checklist row may be clickable on pointer devices, with a clearly labeled action available for keyboard and assistive-technology users.
- Opening a checklist item does not resolve it.
- A blocker normally clears automatically when its underlying count reaches zero or the required inspection/correction is completed.
- An admin may select **Mark resolved anyway**. A confirmation popup explains the unresolved condition and requires the admin to confirm that it does not affect the result.
- Manual resolution requires a reason, records the unresolved count and relevant records in the audit log, and does not alter the underlying submissions.
- Returning from the review or inspection view preserves the finalization workflow state.
- The finalization action remains unavailable while any competitive blocker remains.

Reaching the submission cutoff automatically records submission closure and prevents new uploads. Existing pending evidence remains reviewable, and admins may approve or reject it after cutoff. Every pending submission blocks finalization; approved, rejected, withdrawn, and reversed records do not block solely because of their status.

Normal finalization requires strong confirmation but no written reason. It revalidates blockers, recalculates progress/rankings, stores official placement and statistic snapshots, records the actor/time, and publishes official results. Unfinalization requires strong confirmation and a reason, preserves the superseded snapshot, returns the event to provisional final review, and does not reopen submissions.

Website-account captain/co-captain roles remain historical rather than expiring. Event state and submission-window authorization remove their mutation permissions. Assigning a role or finalizing a draft/event never generates another account or credential. Password-based emergency captain credentials are created/enabled only through an explicit audited admin fallback, are disabled automatically at submission cutoff, and are not silently re-enabled if submissions later reopen.

### 7.3 Event creation and setup

An enabled admin creates the event before signups, drafting, or board publication. Initial creation requires only an event name and timezone. Description, schedule, signup configuration, custom questions, capacity, team estimates, and board estimates may all be entered during the creation journey, but they do not block saving the initial private draft.

The system generates a unique URL identifier from the event name. Admins may edit it until the event first becomes public; it remains stable afterward. Duplicate event display names are allowed. Every enabled admin may continue configuring every event, and audit history identifies the actor for each change.

The display name may change after publication with an audit entry while the public URL remains stable. The public description is optional while the event is private and required before signup opens. Event banner/artwork is always optional: admins may add it during creation or add, replace, or remove it later without affecting readiness.

The timezone defaults to `Europe/Copenhagen`. Admins choose another supported timezone through a controlled selector rather than entering an arbitrary identifier. A private event may change timezone normally. After signup opens, the change requires confirmation showing how every configured UTC timestamp will display in the new timezone; after the event starts it additionally requires an audit reason. A timezone change never moves the stored UTC instants—changing the schedule is a separate action.

The complete setup flow contains:

1. Event identity: name, description, banner/image, and URL identifier.
2. Schedule: signup opening/closing, draft time, event start/end, submission grace period, and timezone.
3. Event-specific settings: evidence requirements, verification code, ranking rules, and buy-in/prize notes. General public rules are maintained once on the global Rules page rather than copied into each event.
4. Signup form: required fields, optional questions, signup code, and participant-edit behavior.
5. Team configuration is completed in the team/draft workflow by creating the actual drafted and pre-formed teams; event creation does not ask for a team count or roster size.
6. Preliminary board configuration: optional expected dimensions and board-EHB estimates for team count and size. Final dimensions, EHB planning inputs, and tile layout are controlled by the board editor.
7. Captain access: event/team roles on website accounts and explicitly managed disabled-by-default emergency credentials.
8. Review and publish: validate required configuration and choose which public event information becomes visible.

The event remains a private admin draft until signups are explicitly opened. Opening the event makes the signup page, signup board, and minimum signup information public, such as event name, signup deadline, expected event dates, and signup instructions. The signup board is distinct from later team rosters.

Event setup is not a single-session process. Admins may create the event, open signups, and continue building or revising its private board throughout the signup and pre-draft period. The board does not need to be complete when signups open. Board validation and publication, rather than event creation or signup publication, determine when the competitive board becomes fixed and public.

Saving a draft and opening signups use separate readiness rules. Before signups open, the system validates the configuration required to accept and manage signups and distinguishes blocking omissions from warnings and work that is intentionally deferred until a later event phase.

Readiness is evaluated separately for saving the draft, opening signup, starting the draft, starting/publishing the event, and finalization. Each gate classifies unmet work as a blocker, an overridable warning, or a later task that is irrelevant to the current transition.

An admin may explicitly discard an accidental or experimental event when it has no participants, teams, event-scoped accounts, or evidence. Board content, signup questions, and other event-owned setup data do not prevent discard and are removed with it. Discard preserves a minimal audit/tombstone record and the old URL identifier, removes the event from active and public listings, and requires confirmation. Once protected participant, team, account, or evidence data exists, discard is blocked and the normal cancellation/archive workflow must preserve the event's history.

The following have separate publication controls and are not exposed by opening signups:

- Team assignments and rosters
- Draft order or draft progress
- Finalized draft results
- Bingo board and tile details
- Captain accounts or admin-only configuration

Finalized rosters and the effective pick order publish when the draft is finalized. After that succeeds, a popup or short follow-up page asks **Publish board?** and provides a separate **Publish board** action only when the board is grid-complete, explicitly approved, and still private. Dismissing it—or having an incomplete/unapproved board—does not affect draft finalization; the board stays private with a prominent admin action and can be approved/published later.

Board dimensions remain editable planning configuration before board publication. Estimated team count and players per team, when needed to estimate board EHB, are entered in the board editor rather than event creation or draft setup.

- Drafted-team count is derived from the drafted teams admins actually create, and roster sizes are derived from confirmed included participants.
- Admins may create pre-formed teams before or after the website draft and manage their rosters manually.
- A pre-formed team created before the draft is excluded from draft order, turns, team-size calculations, and draft picks unless an admin explicitly changes it to a draft-participating team before the first pick.
- A pre-formed team created after the draft joins the same event and competitive board without rewriting the completed draft history.
- The board editor may change board dimensions before board publication.
- Board-editor team estimates affect only board-EHB planning and never create teams, set roster sizes, or become draft constraints.
- Recording the first pick permanently locks drafted-team creation, removal, and formation conversion because those changes invalidate pick order and assignments.
- Changing board dimensions after tiles have been placed requires a preview of which positions or tiles are affected.
- Changing published board structure requires an explicit admin confirmation and audit reason. Team display metadata instead follows its separate pre-event edit lock.

Application-owned images use managed upload rather than arbitrary URL entry. Event banners, team artwork, custom board/tile artwork, and participant evidence are selected as local files, uploaded through the application, validated server-side, and stored as managed assets. Decorative images use their own authorization and retention rules even when they share the evidence uploader's file-selection and validation mechanics. External source-image URLs are accepted only in the global OSRS catalogue, where catalogue infrastructure fetches and caches them.

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

Any enabled Admin may create, edit, deactivate, or reactivate catalogue records. Routine changes are audited without requiring a written reason. Only the Super Admin may permanently delete a catalogue record, and only after strong confirmation and a complete dependency check proves that no source drop, board, asset/cache, import review, or historical record references it. Referenced records must be deactivated instead.

Bulk catalogue preview and apply are Super-Admin-only. Apply requires a reviewed change preview and strong confirmation but no typed reason. A stale preview or unresolved conflict aborts the complete import.

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
- Estimated EHB
- Optional tie-break EHB value
- Active/published state

Tiles belong to one event board. The editor supports moving/swapping them but does not duplicate tiles, copy them from earlier events, import them, or expose a reusable tile-template workflow.

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

A manual objective contains its own completion description, objective-specific criteria, and target quantity rather than eligible item drops. General screenshot and submission instructions belong to the global public Rules and how-to pages. Each approved completion normally contributes `1`, so an objective may require multiple completions, such as completing three Inferno runs.

## 11. EHB and board balancing

The board editor displays:

- Estimated EHB per tile
- Total estimated board EHB
- Estimated EHB for each row
- Estimated EHB for each column
- Lowest and highest line EHB
- Absolute and percentage spread between lines
- Warnings for unusually easy or difficult lines
- A **Preview board** action that renders the actual responsive public-board treatment without admin controls and without approving or publishing

Admins deliberately position tiles to balance rows and columns. Tiles are not randomly placed after creation.

EHB and efficient-rate data may be imported from useful external sources such as Wise Old Man where technically and legally appropriate. The preferred approach is a stable API or data endpoint rather than fragile HTML scraping.

For version one, relevant external data will be collected in a one-time import during initial setup. All imported values remain editable by admins. Automatic or repeat synchronization may be considered later.

For a simple single-drop requirement, expected EHB is calculated from a reviewed source-specific probability and efficient-completion-rate pair. For example, at 100 kills per hour and a `1/1,000` drop rate, the expected time for one qualifying drop is 10 EHB. Group content may pair an in-name probability with the relevant team completion rate, or a full-contribution probability with a rate normalized per invested player-hour; team size is applied exactly once. Catalogue rates may use numerators other than one and explicitly record per-completion rolls. Team, raid-scale, purple-table, points, and contribution assumptions are resolved before entry and retained as explanatory notes rather than calculator exceptions.

Complex requirements are estimated from possible completion outcomes rather than by blindly averaging bosses or adding rates. Weighted drops advance by their configured contribution, duplicate-restricted requirements track shared item identities, and alternative sources are chosen according to the lowest expected remaining person-hours. Multiple objectives are estimated separately and added. A catalogue-backed/drop tile must always produce an automatic estimate. Missing or ambiguous rate mechanics block board approval and must be corrected in the catalogue or requirement configuration; they are never guessed and cannot be bypassed with a manual override. Only a custom/manual objective accepts an explicitly entered manual EHB value.

Unapproved boards derive catalogue names, images, rates, variants, and EHB from the current global catalogue. Relevant catalogue changes automatically invalidate and recalculate their tile, row, column, total, and per-player estimates.

**Approve board** is the snapshot boundary. Approval transactionally revalidates the complete board and captures an immutable version of the catalogue values, board configuration, rates, EHB values, artwork references, and calculations. Later catalogue changes do not alter an approved board. Unapproving or editing approved unpublished competitive content returns the board to Draft, retains the superseded approval snapshot/history, and resumes live catalogue derivation. Publication uses the active approval snapshot without recalculation.

Draft preview uses current live derived data; approved preview uses the frozen approval snapshot. Preview never changes board state. Published boards remain historically stable.

Board approval is an explicit admin action available only when every grid position is filled and every tile validates. Any enabled admin may approve; a second approver is not required. An approved unpublished board may be unapproved, and editing any tile or competitive board content automatically returns it to Draft while preserving the prior approval in history. These private pre-publication changes require no written reason. Event start is blocked until an approved board has actually been published.

The application provides one permanent, global, public **Rules** page. Any enabled administrator may edit it at any time through an **Edit rules** action. Rules changes use normal authorization, validation, concurrency protection, and automatic history, but do not require a written reason, notify participants, or affect event readiness.

Public how-to pages, including **How to submit drops**, are source-controlled application content maintained through the development workflow and have no in-application editor. Event dashboards and submission surfaces may link to these global pages. Tile approval does not require or inspect per-tile evidence instructions because those fields do not exist. Custom/manual tiles retain only their objective-specific completion criteria.

## 12. Evidence submission

### 12.1 Submission contents

Each participant or captain submission represents:

- One kill or reward event
- One obtained drop
- One screenshot
- One tile requirement
- One credited participant: the authenticated participant themselves, or a current teammate selected by a captain/co-captain
- The credited participant's active playing account at server submission time
- One team, derived from current event/team membership

It records:

- Event and team
- Tile and requirement
- Boss/activity
- Drop
- Credited player
- Credited playing account
- Credited weight copied from the board-requirement snapshot, defaulting to `1` and not editable by the submitter
- Total approved contribution, capped by the remaining requirement progress and confirmed by an admin
- Submitted time, generated automatically by the server and immutable
- Screenshot
- Optional submitter note
- Status and admin feedback

Participants cannot select another player. Captains/co-captains cannot select a player who is not on their current event team.

An ordinary participant sees no credited-player or credited-account selector. A captain/co-captain selects a current teammate, not an account. The server derives and snapshots that participant's currently active/drop-eligible playing account. Submitters cannot credit an inactive registered playing account or informational account; they must submit a drop before swapping away from the account that received it.

### 12.2 Evidence expectations

A normal screenshot should show:

- Player name
- Game message identifying the drop
- Timestamp overlay when required by event rules
- Event-specific verification code when enabled by event rules

The verification code is an event setting with two modes: enabled or disabled. Admins normally enter a custom fun code, may generate one, and may activate a replacement immediately or schedule it. Every submission snapshots the code interval active at its immutable server submission time. Review is visual only; there is no OCR. An admin may approve a mismatch as an explicit exception.

The clan event plugin renders the evidence time in UTC inside the screenshot. That value is not transcribed into another form field or extracted through OCR. The reviewer visually confirms that it falls inside the official event window and an interval in which the credited playing account was active. Immutable server submission time remains authoritative for upload-cutoff validation and every ordering rule that explicitly uses submission time, and it is never editable.

When the credited participant has swapped accounts, evidence review shows the latest relevant UTC transition and provides complete swap history only as an admin/audit detail when needed.

### 12.3 Validation

Before accepting a submission, the system checks that:

- The authenticated actor's event/team role is allowed to submit or correct evidence.
- The selected tile and drop are valid for the event.
- The credited participant is the submitter or, for a captain/co-captain, a current teammate.
- The server-derived credited character is that participant's active `PLAYING` assignment at submission time and is not an informational/support account.
- The submission contains one image.
- Immutable server submission time is no later than the active upload cutoff.
- The contribution does not exceed the remaining allowed progress.
- Duplicate and per-drop cap rules are respected.
- The same approved submission cannot be allocated to multiple tiles.

## 13. Submission review lifecycle

Submission states are:

1. **Pending:** Awaiting admin review; editable and withdrawable by the team's captains.
2. **Approved:** Contribution has been applied to official progress.
3. **Rejected:** Does not count; includes an admin reason.
4. **Withdrawn:** Removed by a captain before approval.
5. **Reversed:** Previously approved, then removed by an admin.

### 13.1 Admin review actions

An admin can:

- Approve
- Reject with a reason
- Correct metadata and approve
- Reverse a previous approval

Editable metadata includes:

- Tile or requirement
- Credited playing account, from which the event participant is derived
- Qualifying drop and its derived boss/activity

These material corrections require a written reason, revalidate the complete submission, and store the original and new values. Credited participant is not independently editable. Immutable server submission time, snapshot contribution weight, calculated contribution, and the submitted evidence image are not administrator-editable.

Every review shows:

- **Submission time:** immutable server submission time.

Only when submission occurred after the authoritative event end, the review additionally shows the calculated number of minutes after event end and **Latest clan event time:** the authoritative end formatted in UTC. The administrator compares the timestamp visible in the screenshot with that boundary. A drop shown after it is ineligible even though the website still accepts uploads during grace. The submission cutoff is enforced by the application but need not be repeated in this compact visual comparison.

A duplicate, unusable screenshot, or other invalid attempt is rejected with the required reason. There is no request-changes or special duplicate review state. While the active upload window remains open, the rejected-submission view offers **Resubmit**. It creates a new submission, prefills the rejected attempt's structured values and note, and requires a newly uploaded screenshot. The submitter may correct ordinary structured choices such as tile/requirement or qualifying drop, but the originally credited participant and playing account are copied and read-only even if that participant has since swapped. The new record links to the rejected record, receives its own immutable server submission time and review history, and undergoes normal validation. Rejection never reopens or extends the upload window, and the rejected record remains historical.

Rejection creates an idempotent in-site notification containing the reason for the linked credited participant and every current linked captain/co-captain on the team. It does not notify the whole roster. When the credited participant is unlinked, captains/co-captains remain the notification recipients.

### 13.2 Reversal

Reversing an approval:

- Deducts the exact contribution created by that submission
- Recalculates tile, row, column, board, leaderboard, and placement state
- Reallocates newly available capacity to later approved evidence up to its original eligible claim
- Records the admin, time, and reason
- Preserves the submission and its history

## 14. Evidence visibility

- A participant may view only their own pending, withdrawn, and rejected evidence and rejection feedback.
- Captains/co-captains may view all pending, withdrawn, and rejected evidence for their own team.
- Approved evidence metadata, credited player, and screenshot are publicly visible from the relevant tile so the community can inspect accepted evidence.
- Other teams do not see pending progress.
- Participants and captains cannot request that approved evidence or the credited player be hidden. Submitters are responsible for concealing private messages or other information before upload.
- There is no hidden-but-still-approved evidence state. If an approved image should no longer be public, an admin reverses its approval with a reason; the team may submit a corrected or redacted screenshot through the normal resubmission workflow while the upload window permits it.
- Version one has no public evidence-report, bug-report, or general-feedback form. Community reports and feedback use the Discord feedback channel; admins handle a valid evidence concern through reversal/resubmission.

After draft finalization, the signed-in participant's primary event destination is their published roster until the board is published, then the existing team-board view. That view shows event/team, role, planned/current active account, event end, and submission cutoff. It adds the participant's own evidence actions and private team-focus projection without creating a separate participant board.

Before event start, the built-in primary account is only the planned starting account. It becomes active at event start. Participant/captain swaps are available only during `LIVE`, remain unlimited under the approved next-whole-UTC-minute rule, and close at event end. Evidence creation and pending edit/withdraw remain open through the submission cutoff for in-window drops; cutoff then makes participant history read-only while admin review continues.

Current team members see team focus read-only on the existing team board; captains/co-captains receive mutation controls there. Public/opponent projections retain the approved board without focus. If the authorized view becomes crowded, a view-only focus visibility toggle or compact summary may hide/show the private layer without changing focus state.

The public tile view should show approved drop, player, team, submission time, contribution, and evidence.

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
- Derive the final drafted-team count from the active drafted teams
- Derive balanced roster sizes from the confirmed internal participants included in the website draft
- Preview the larger roster size and how many teams, if any, will have one fewer player
- Randomly scramble the participating teams to determine the initial picking order
- Run a snake draft, reversing pick order on alternating rounds
- Assign drafted players to teams
- Undo incorrect picks repeatedly in reverse pick order, including back to zero picks
- Pause and resume draft entry
- Finalize and lock rosters
- Automatically publish completed team rosters and effective pick order when the draft is finalized, then offer a separate **Publish board?** prompt/page only when the board is complete and approved
- Keep every participant visible throughout the draft, including already drafted players
- Mark drafted players with their assigned team instead of removing them from the participant list
- Sort the participant list by EHB by default while allowing admins to sort by player name, signup time, or draft status

Captains make their selections through the community's normal voice or text communication. An admin records the selections on the website.

Before finalization, the draft board and team assignments are visible only to admins in version one. Captains and public visitors do not see draft state on the website until it is finalized and automatically published.

The full participant pool remains visible during drafting so captains can maintain an overview. Available players are visually distinct from drafted players, and the current team's turn is clearly identified.

The real draft does not ask for a team count or target team size. Its team count is the number of active drafted teams, and its participant count is every confirmed internal participant who is available to draft or already assigned to a drafted team. Captains and co-captains occupy normal roster positions. If the participant total does not divide evenly, final team sizes may differ by exactly one; the preview reports the larger size and how many teams will have one fewer player. The snake turn plan accounts for preassigned members and is blocked if those assignments make a balanced result impossible.

The board editor may retain independent estimated team-count and team-size inputs solely for planning board EHB. Those estimates do not create teams, reserve places, or constrain the real draft.

Waiting and withdrawn participants, pre-formed teams, and their assigned roster members are excluded from the website draft. Finalization requires every included confirmed participant to have a drafted-team membership and publishes all completed rosters plus the effective pick order together. Every drafted team must have an assigned member with the actual Captain role before the draft begins; co-captain alone is insufficient. Every team must have a Captain or enabled emergency credential before event start.

Drafted teams are created individually with an event-unique name, optional uploaded image, and optional affiliation. Their stable URL identifier does not change with the display name. Team metadata remains editable through draft finalization but locks at event start. Pre-formed teams may be created and their rosters corrected before event start, including after website-draft finalization. Once any draft pick has been recorded, drafted teams cannot be added, removed, or converted, even if the pick stack is later completely undone.

All preassigned captains and co-captains occupy normal roster positions. A team whose current roster is larger because it has more preassigned captain/co-captain members is skipped until lower-count teams catch up. Each team card's compact `current/final` counter shows its derived final capacity, including which teams will finish one player smaller.

Public finalized draft results show the effective pick sequence with pick number, participant, and team. Undone attempts, internal controller identity, and correction details remain private to admins.

Before event start, an admin may reopen a finalized draft after strong confirmation and a required written reason. This temporarily withdraws the public roster/pick-order projection and allows repeated stack undo plus new picks, but never reopens drafted-team creation/removal/formation. All finalization cycles and superseded picks remain historical. An already published immutable board remains public because roster correction does not modify it.

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

Admins configure and publish a signup form for an event. An authenticated website account is required for a new normal public signup; initial account creation starts through Discord, but a returning account may be password-authenticated and currently have Discord unlinked. Admin-created, imported, and external-team roster records may exist without a linked participant account until access is claimed or recovered.

Each event has a configurable signup window:

- Signup opening date and time
- Signup closing date and time
- Event timezone
- Maximum number of confirmed participants

Admins can extend or shorten the signup window and increase the participant cap. The participant cap cannot be lowered. Changes are recorded in the audit log. The public signup page clearly shows the closing time, capacity, confirmed signup count, and whether new submissions currently enter the waiting list.

The standard form contains:

- One OSRS playing account, required
- EHB snapshot for that playing account, required
- Captain-volunteer choice, always present
- Event-specific custom questions

Discord identity comes from authentication and is not a participant-entered signup answer. OSRS character registrations do not grant website authority and do not assert exclusive account ownership.

The signup character selector shows the authenticated participant's globally linked characters and allows another trusted or borrowed character to be linked. A character already assigned to another participant in the same event cannot be selected. This event-level uniqueness is enforced transactionally; the same character may be assigned to a different participant in another event.

The built-in required Account question creates the participant's first playing account and includes its required event-specific EHB snapshot. Admins may add more custom Account questions and configure whether each named answer is another playing account or informational-only account such as a support alt. Secondary Account questions are always optional. When an optional playing account is answered, its own EHB becomes required. An informational account has no EHB, cannot become active, cannot receive drop credit, and does not enter Wise Old Man standings. If only the existence of a support alt matters, the admin uses a Yes/No question instead. While signup is open, the participant may edit these answers. The built-in primary account automatically becomes the initial active/drop-eligible account at event start; there is no separate initial-active choice during signup. Closing signup freezes the account set and roles, and live swaps may then choose only among the frozen playing accounts.

The primary Account answer's EHB is the participant's draft sorting/balancing value. Secondary EHB values are not summed or substituted. Once the Wise Old Man integration is delivered, every playing-account EHB control provides an explicit **Fetch from Wise Old Man** action using that account name; this is not an event-level option. Manual entry remains available, a failed or rate-limited fetch shows accurate feedback without clearing the current value, and the submitted value is stored as the event snapshot. The application must review and follow the current WoM API usage rules before implementing this action and must not issue requests on page render or every keystroke.

To limit unwanted submissions, an admin may protect the form with an event-specific signup code distributed through Discord. Admins can close and reopen signups at any allowed pre-draft time. Reopening a form that already has responses requires confirmation and automatic history but no written reason.

The participant authenticates to a website account through Discord or public username/password before normal signup; first-time account creation itself begins through Discord. The resulting event-participant record belongs to that account, and the participant may edit it only while signup is open. New normal signups do not receive private edit links. Admins retain audited correction, withdrawal, restoration, and identity-recovery authority. Unclaimed legacy or imported records may temporarily retain a private edit link; claiming the record invalidates it, and the fallback is removed after migration.

The system records signup time automatically. Buy-in/payment is not collected from the public participant form. Admins manage one private binary `Unpaid`/`Paid` value on the participant record; it defaults to `Unpaid`. Events without a buy-in may ignore it. No `Unknown`, `Waived`, or `Not required` states remain in the target model.

Custom questions support short text, long text, number, yes/no, single choice, and Account. An Account answer uses the participant's My accounts selector or lets them add another trusted OSRS character. The question configuration determines whether that answer is playing/drop-eligible or informational-only. Comments and availability are not fixed fields; organizers add Long Text questions when needed.

A published form must be closed before its definition changes. Before the first accepted/imported response, custom questions may be edited or removed freely while private or closed. The first response locks question type, Account role, choice options, and answer shape. Later questions must be optional. Until the draft starts, admins may still edit label, help text, order, and public visibility while signup is closed. Structural replacement disables the old question and creates a new stable question; it never rewrites existing answers. Draft start freezes ordinary form metadata and visibility changes. A separate privacy control can hide a question and its answers after draft start; restoring visibility is separately confirmed. These privacy controls retain automatic configuration history but require no written explanation.

Signup forms may therefore differ between participants in the same event. Admin and signup-board views must treat every custom answer as optional historical data. A participant who signed up before a question was added has no answer record for that question; the page must show a neutral fallback such as **Not answered** and must never fail because an answer is missing. Disabled questions retain existing answers and remain publicly visible only if their separate signup-board visibility setting remains enabled.

The public signup board has separate Confirmed and Waiting list sections and shows the public usernames in both. The built-in primary playing account and its EHB are public. Every visible secondary playing Account answer shows its own EHB, with the question visibility setting controlling both together. Captain volunteer is always public. Every newly created custom question defaults to **Show on signup board**, while admins may turn that off; admin-only answers can never be public. Informational account answers may be shown there but not on team rosters, evidence, board progress, or leaderboards.

Required system fields such as primary account/EHB and captain volunteer cannot be removed. Every form-definition mutation creates a new form version and an audit entry.

Signup readiness is mode-specific. **Open now** does not require or use a scheduled-opening value and records the actual opening instant. Scheduled opening requires a valid future scheduled instant. Both modes require a public description, positive capacity, valid event start/end, a valid closing instant no later than event start, an allowed pre-draft state, configured system-wide Discord sign-in, intact system questions, valid custom-question definitions, and a signup code only when code protection is enabled. A secondary Account question must remain optional and identify its answer as playing/drop-eligible or informational-only.

Waiting-list support being disabled, a public short/long-text answer, and reopening a populated signup are warnings requiring acknowledgement rather than blockers. Missing banner art, board setup, teams, custom questions, draft time, or signup-code protection is not a warning. Current Wise Old Man availability does not affect readiness because manual EHB entry remains available.

The scheduler reruns the same readiness contract transactionally at the scheduled instant. If configuration became invalid, signup stays closed and admins receive a visible failure alert. Temporary Discord or Wise Old Man service failure does not rewrite event state; participant-facing operations provide accurate unavailable/retry feedback.

### 18.2 Capacity and waiting list

Signup records have one of these participation states:

- **Confirmed:** Within the current participant cap and eligible for the draft.
- **Waiting list:** Valid signup received after the confirmed-participant cap was reached.
- **Withdrawn:** The participant or an admin marked the signup inactive before the draft.

When the confirmed-participant cap is reached, the signup form remains open until the signup deadline. Additional valid signups join the waiting list in signup-time order.

After a successful create or edit, the authenticated confirmation page shows whether the participant is confirmed or waiting-listed, their exact waiting-list position when applicable, playing accounts and EHB snapshots, separately identified informational accounts, captain-volunteer choice, all submitted custom answers, and whether editing or withdrawal is currently available. The public signup board is a separate, narrower projection; it also identifies waiting-listed participants in a section separate from confirmed participants and shows their exact numbered waiting-list positions.

Confirmed and waiting-list signups both reserve every named Account answer against use by another participant in that event. Withdrawal before draft start releases those reservations while preserving historical assignment records. The released character may then be registered by another participant in the same event.

Signup creation and editing are atomic across the participant, answers, character links created in the submission, event assignments, EHB snapshots, first-response marker, and capacity/status decision. If any selected character is already reserved, nothing from the attempted create/edit is committed. The form marks the conflicting Account answer, asks for another account, and retains all other entered values. A failed edit leaves the last successfully saved signup unchanged.

When an admin increases the participant cap:

- The earliest waiting-listed participants are promoted automatically until the new capacity is filled or the waiting list is empty.
- Promotion order uses the original valid signup timestamp.
- Promoted participants retain their original signup data and authenticated ownership.
- Each promotion is recorded in the audit log.

Example: if the cap is 50 and seven people are waiting, increasing the cap to 60 automatically confirms all seven and leaves three additional confirmed places available.

When a confirmed participant is withdrawn before the draft is locked, the earliest waiting-listed participant is automatically promoted into the open place.

Editing a signup does not change its original signup time, deterministic sequence, confirmed/waiting status, or waiting-list position. While signup is open, a participant may cancel through a separate confirmed action. If they sign up again during the same open window, the existing record is reactivated at the end of the queue with a new signup timestamp and sequence.

After signup closes and before the draft starts, a participant may still withdraw but cannot restore themselves. An admin may restore them before draft start only if their required account reservations remain available. Restoration uses current capacity or the end of the waiting list and never displaces someone who has already been promoted.

Promotion creates a durable in-site notification for the linked participant and every enabled administrator. The admin notification identifies the event, promoted participant, and promotion trigger. The authenticated confirmation page always reflects current authoritative status. Automated Discord messages are not part of this workflow; admins may communicate promotions manually.

Admins use the same confirmed **Withdraw participant** action whether the person can no longer attend, signed up incorrectly, or is otherwise ineligible. The record remains available in the Withdrawn section rather than being permanently erased. Automatic history records the actor and timestamp; a private admin note is optional and no written reason is required.

Automatic waiting-list promotion normally stops once the draft is locked. After that point, replacements and roster changes require an explicit admin action so teams are not changed unexpectedly.

Participant self-service withdrawal ends when the draft starts. An admin may still withdraw a drafted participant, including during a live event. This ends the current team membership and future eligibility while retaining the draft pick, former membership, registered accounts, evidence, and contributions as history. It creates a visible vacancy and never triggers automatic promotion.

Admins contact waiting-list participants manually to confirm continued availability. The replacement picker shows original waiting-list order but allows the admin to select the person who is actually available. A normal replacement comes from the waiting list, already has frozen registered accounts/EHB, becomes confirmed, and joins the vacant team without rerunning or rewriting the draft. Post-draft accounts belonging to the departed participant remain reserved. Filling the vacancy is optional. If nobody on the waiting list is available, an admin may optionally create a new internal replacement with the same required participant/account/EHB validation and place them directly on the vacant team.

A live withdrawal ends drop eligibility at the first full UTC minute after confirmation; the old participant remains eligible through the displayed request minute. A live replacement's primary account activates at the first full UTC minute after replacement confirmation. A later replacement therefore leaves an honest eligibility gap. Existing evidence from before withdrawal remains reviewable, and the replacement receives no retroactive eligibility.

Admins may assign, promote, demote, or revoke captain/co-captain roles for current team members after the draft and during the live event. A withdrawn captain loses authority immediately. Event start requires every team to have a current Captain or an explicitly enabled team-scoped emergency credential; co-captain alone is insufficient. Losing the final captain during live play creates an urgent warning but does not stop the event. The system never chooses a captain automatically. Role changes use the member's website account, take effect for future actions, survive Discord relinking, preserve history, and notify the affected participant.

A post-draft vacancy notifies all enabled admins and remaining linked team captains/co-captains. A confirmed replacement notifies the linked replacement and current linked team captains/co-captains. These are in-site notifications; Discord availability coordination remains manual.

An admin may transfer an event-participant record to a different website account when it was attached to the wrong or duplicate global account. Lost Discord access normally uses password login and self-service Discord relinking instead. The transfer destination cannot already participate in that event. Transfer immediately revokes the previous account's event access and preserves signup order/status, accounts, team, evidence, and history. It does not merge global website accounts or My accounts lists. Strong confirmation and automatic before/after history are required; a written reason is not.

Before draft start, participant administrators work from one event-level workspace covering Confirmed, Waiting list, and Withdrawn records. It supports search and filters for status, paid/unpaid, linked identity, captain volunteer, source, and team; shows all playing/informational accounts, per-account EHB, custom-answer visibility, private notes, and queue/team state; and links to identity recovery.

Admins may correct answers and accounts after signup closes but before draft start. Corrections reuse normal validation and atomic reservation behavior and never change queue/status. Admins may also add an internal participant while public signup is closed, bypassing only the public opening window and signup code. Admin-created internal participants obey normal capacity/waiting rules; external/pre-formed roster members remain outside that pool.

A linked participant receives an in-site notification when an admin withdraws or restores them or changes their registered event accounts. Payment, private-note, and ordinary non-account answer changes do not notify them.

### 18.3 CSV fallback

CSV import remains an admin fallback for migrating existing Google Forms responses or performing bulk registration.

Version one uses a downloadable, documented CSV template with stable system columns rather than attempting to infer any arbitrary spreadsheet automatically.

The import flow should:

- Download the current event's CSV template.
- Upload a completed CSV file.
- Show a preview before making changes.
- Require the template's primary-account and EHB columns.
- Accept optional legacy account, comments, captain-volunteer, Discord identity, paid/unpaid, and custom-answer columns.
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
| `Betalt buyin` | Private paid/unpaid value |
| `Kolonne 1` | Signup/order number, subject to confirmation during import |

Imported EHB is stored as a signup-time snapshot for the corresponding playing account and is used to inform drafting and team balancing. A legacy row with one account/EHB pair maps to the primary playing account. It does not replace the separate boss/activity EHB data used to estimate tile difficulty.

The private paid/unpaid value is sensitive administrative data. It is visible to admins but must not be shown on public participant lists, draft results, team rosters, evidence, or leaderboards.

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

Archived events keep the same public board/team/tile/result routes. Signed-in former participants may also read their own rejected and withdrawn submission history, but every event mutation is removed. There is no separate archived-participant dashboard.

### 19.2 Captain pages

- Submit evidence
- Team submissions
- Pending submissions
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
- Emergency captain credential creation, activation, automatic cutoff disablement, and explicit re-enabling
- Catalogue imports and manual overrides

Audit entries identify the acting account, time, affected record, previous value, and new value where applicable.

Automatic audit history does not imply that the administrator must type a reason. Routine configuration and lifecycle actions record structured before/after facts without demanding an explanation. A written reason is required only for exceptional competitive or historical overrides where the rationale materially matters, such as ending an event early, overriding an unresolved finalization blocker, rejecting/reversing accepted evidence, or changing a result.

Historical audit data must not be casually deletable through the normal admin interface.

Every enabled Admin may read the audit log. It is a newest-first, server-paginated table with 25 entries per page and filters for event, actor, action, entity, and date range. Pagination retains filters and reaches the complete retained history; the 25-row page size is not a retention limit. Entries cannot be edited or deleted, and version one has no audit export.

Audit-entry details present structured before/after data in human-readable form while retaining the immutable structured record. Routine successful website-account login updates `last_login_at` but does not create a main-audit entry. Failed attempts and throttling remain security logs. Successful emergency-credential use and security-sensitive password, Discord-link, role, disable/restore, ownership, and emergency-access mutations remain durable audit events.

The global **Accounts** area keeps normal website accounts and emergency credentials in two clearly separated logical sections because their fields and actions differ; the later UI pass may choose tabs or another shared responsive presentation. Each section uses server-side search/filtering and 25-row pagination.

Website-account search/filtering covers public username, global role, active/disabled state, Discord linked/unlinked state, and event participation. Its rows show username, role, state, Discord-link state, last login, and current event-role summary. Details show every linked OSRS character, the last-known Discord display name clearly labelled non-authoritative, event participation, current/historical event/team roles, and disable history.

Emergency-credential rows show login username, event, team, setup state, enabled/disabled state, last login, cutoff state, and permitted setup/reset/enable/disable actions. Every account action is projected from current server authority: ordinary Admins may manage User reset/disable and emergency access, while only the Super Admin receives Admin reset/disable, global-role, and ownership-transfer actions.

The overview never exposes password/OAuth/setup/reset-token data or unnecessary raw Discord identifiers. Version one has no website-account merge or permanent account deletion.

Granting/revoking Admin and restoring an account creates a personal in-site notification for the affected website account. A disabled account receives neutral contact-an-admin guidance rather than exposing the private disable reason at login; the administrator communicates that reason separately.

Lost-owner recovery has no web action. An operator-only command may create a 60-minute owner password-reset link or atomically transfer ownership to a specified active website account when necessary. It requires explicit source/destination identifiers and confirmation and records a system audit event.

Personal notifications and unresolved Admin actions are separate. Opening a personal notification marks it read. Pending reviews, postponed starts, waiting-list follow-up, vacancies, and missing-captain conditions remain visible until the authoritative underlying condition is resolved.

## 21. User experience direction

- Every newly created page follows the approved shared UI rules immediately, including common components, density, responsive behavior, accessibility, mutation feedback, and no-JavaScript fallbacks. The later complete UI pass remains responsible for final cross-site consistency.
- Every page reachable by anonymous visitors, normal Users, captains/co-captains, or emergency captains supports both English and Danish through the existing language switch. All visible states and feedback on such a page are localized. Pages restricted entirely to Admin/Super Admin may remain English-only.
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

1. Admins can create an event, teams, players, captain/co-captain assignments, and a board; normal captain access uses website-account event/team roles and emergency credentials can be enabled explicitly.
2. Admins can model every objective on the provided example 5x5 board without custom code.
3. The editor calculates tile, line, and total EHB while arranging the board.
4. Captains can submit one screenshot and drop for a player on their team.
5. Captains cannot submit for or inspect pending evidence from another team.
6. Admins can approve, reject with a reason, make reasoned metadata corrections, and reverse approvals.
7. Approval and reversal correctly update all related progress and standings.
8. Public visitors can inspect all teams, completed tiles, and non-hidden approved evidence.
9. Full-board finish time and provisional placement use immutable server submission time.
10. Submissions close at the cutoff while existing evidence remains reviewable.
11. Event finalization requires an admin action.
12. Website-account captain/co-captain roles remain historical while lifecycle authorization removes mutations; explicitly enabled emergency credentials are disabled at submission cutoff and can be re-enabled only through an audited admin action.
13. Admins can operate a private snake draft and publish its completed results.
14. Admins can create and publish an event and its website signup form.
15. Participants authenticate to one website account through Discord or public username/password, submit at most one signup per event, and edit it only while signup is open; new normal signups do not receive private edit links.
16. Signups received within the cap become confirmed and later valid signups enter an ordered waiting list.
17. Increasing capacity or freeing a place automatically promotes waiting-listed participants in signup-time order before the draft is locked.
18. Admins can extend the signup window, change capacity, correct/add participants, and withdraw or restore them with confirmation and automatic history.
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
