# OSRS Community Bingo — Application Atlas

**Generated:** 2026-08-03
**Accepted source commit:** `2301d63ae166873a750266ce5ee6a087f3039054`
**Scope:** Post-functional, read-only product/code audit after accepted Slices 1–10
**Primary review surface:** [`APPLICATION_ATLAS.html`](APPLICATION_ATLAS.html)

> **Nothing is authorized by this Atlas.** It records the accepted application, inconsistencies, questions, and UI-overhaul priorities. It does not authorize removal, redesign, new functionality, production changes, data correction, migration work, or cleanup.

## 1. How to use this Atlas

This document is the durable source of truth. The standalone HTML contains the same review model in a filterable form. Evidence priority for this audit was:

1. accepted code at the source commit;
2. final accepted Slice 1–10 implementation plans;
3. current product, workflow, data-model, architecture, roadmap, and UI-overhaul documents;
4. rendered navigation and Development seed code.

Where these sources conflict, this Atlas records the conflict instead of silently selecting one. “Verified” means directly established from the accepted code or final accepted plan. “Inferred” means a consequence of those facts that was not executed as a user journey. “Manual/visual uncertainty” means behavior or presentation requiring later human review.

## 2. Executive map

### 2.1 Product purpose

OSRS Community Bingo is an event platform for one Discord community. It coordinates website accounts and OSRS characters, event signup and waiting lists, drafted and pre-formed teams, a private board/catalogue workflow, live participant and Captain operation, evidence review, public progress, official results, archive history, and optional cached Wise Old Man activity.

The primary end-to-end event journey is:

```text
Admin creates private event
  → configures identity, schedule, signup form, teams and board
  → opens signup (exact-link public journey)
  → closes signup
  → runs/finalizes draft and publishes rosters
  → approves and separately publishes board
  → starts event when readiness clears
  → participants/Captains operate Live accounts, focus and evidence
  → event ends; upload grace and Admin review continue
  → final-review cycle resolves blockers
  → official result version is finalized
  → event is archived as read-only history
```

### 2.2 Event state is not feature state

`EventState` is the authoritative whole-event lifecycle: `Draft`, `SignupOpen`, `SignupClosed`, `Live`, `AwaitingFinalReview`, `Finalized`, `Archived`, `Cancelled`, and `Discarded`. It is enforced by `EventStatePolicy`, lifecycle services, route filters, and feature services.

The following independent substates narrow behavior without creating another event state:

| Substate | Authority | What it locks or exposes |
| --- | --- | --- |
| First public exposure | `BingoEvent.FirstPublicAt` | Locks the slug; affects cancelled visibility and public destination eligibility. |
| Signup form/response | `SignupForm`, `SignupQuestion`, first-response/version facts | Locks question shape after first response; draft lock later freezes ordinary definition changes. |
| Participant admission | `SignupStatus`, cap, waiting order, assignment reservations | Controls Confirmed/Waiting/Withdrawn and deterministic promotion. |
| Draft | `DraftSession.State`, first-pick lock, controller lease | Separately moves Setup/Running/Paused/Finalized; publication is the ordinary irreversible roster boundary. |
| Roster publication | `DraftPublicationCycle`, active roster projection, event publication flags | Exposes roster without publishing the board or starting the event. |
| Board | `Board.State`, active `BoardApprovalSnapshot`, publication and correction facts | Private Draft → approved/Validated → separately Published; correction replaces immutable snapshots. |
| Live account | `EventParticipantCharacterSwap` | Append-only active-account transitions; valid only while Live and before configured end. |
| Team focus | `TeamFocusMarker` | Private team-scoped focus; completed tiles are excluded/cleared. |
| Evidence | `Submission.Status`, active asset, predecessor, cutoff | Pending/Approved/Rejected/Withdrawn/Reversed lifecycle with linked resubmission. |
| Upload closure | ordinary/reopened cutoff plus `SubmissionsClosedAt` | A feature lock. It does **not** make the whole event read-only; Admin review continues. |
| Final review | current `AwaitingFinalReview` transition ID | Creates an immutable cycle for acknowledgements, corrections and overrides. |
| Official results | `EventFinalizationSnapshot` version and `OfficialPlacementSnapshot` | One active immutable official version; unfinalization supersedes, never deletes. |
| WoM activity | `EventCompetitionSynchronization` generation | Optional cached projection; never blocks lifecycle. Stops refreshing outside Live. |

**Feature-lock rule:** a closed upload window, frozen signup form, finalized draft, published board, or stopped WoM worker is not a declaration that the whole event is read-only. The relevant feature policy and role must be evaluated inside the current event state.

## 3. Roles and authoritative navigation

| Role/persona | Visible global navigation | Principal actions and authoritative routes | Authority notes | Navigation assessment |
| --- | --- | --- | --- | --- |
| Anonymous/public | Public boards `/`; language; Privacy `/Privacy`; Sign in `/Account/Login` | Published roster `/Events/{slug}/Teams`; board `/Events/{slug}/Board`; team board `/Events/{slug}/Board/{teamSlug}`; tile `/Events/{slug}/Board/{teamSlug}/Tiles/{tileId}`; approved evidence `/Evidence/{id}`; exact-link signup/table when published | Public projections only; no private, pending, rejected, withdrawn, focus, or Admin data. | Required and generally reachable. Signup/table are deliberately unlisted exact-link journeys. |
| Ordinary website account / participant | Public boards; Settings `/Account/Settings`; My accounts `/Account/MyAccounts`; My events `/Account/MyEvents`; Change password; Notifications | Signup `/Events/{slug}/Signup`; confirmation/edit `/Events/{slug}/Signup/Confirmation`; owned current/history destination chosen by `EventDestinationPolicy`; own Live team context; own non-public evidence history within scope | Explicit `EventParticipant.AccountId` ownership only. OSRS/Discord name matches never infer ownership. | Required. My Events is the long-term event entry point. |
| Captain | All ordinary website-account navigation plus Captain board `/Captain` | Team-wide submit `/Captain/Submit/{tileId}`; submission history/details `/Captain/Submissions/{id}`; team board/focus and limited unlinked pre-formed teammate swaps | Current linked Captain membership plus participant ownership. Authority is re-derived; not granted by volunteer answer or names. | Required. Current shell labels both Captain and Co-captain authority as “Captain board.” |
| Co-captain | Same rendered Captain navigation and routes | Same evidence/team-focus authority as Captain where policy allows; does **not** satisfy Captain-only draft-start readiness | Implemented through Captain-role claims plus current membership checks. | Awkward terminology: role distinction exists in data/readiness but the global entry is labelled Captain board. |
| Enabled emergency credential | Public boards; Captain board; Settings/change password; notifications; no My accounts/My events | Exact event/team Captain routes, evidence submission, team history and public projections within `AccountEventAccess` | Individual emergency account; exact event/team scope; initialized, enabled, active, within access window. Automatically disabled at cutoff and never silently re-enabled. | Required edge-case fallback. Setup/manage path is Admin Accounts, linked from Draft team cards. |
| Admin | Public boards; Admin tools `/Admin`; ordinary website-account settings; notifications/action queue | Events `/Admin/Events`; Manage `/Admin/Events/Manage/{id}`; Identity, Schedule, Questions, Draft, Board, Finalize, Participant; Catalogue; Review; Accounts; Audit | Must be active website account with `Admin` or `SuperAdmin`; every command reauthorizes inside its service/transaction. | Required. One verified missing-Captain resolution link is broken; see F-01. |
| Disabled Admin | No valid authenticated workflow after cookie validation/sign-in rejection | Access denied/sign-in recovery only; operator or authorized Admin restoration | `AccountCookieEvents` rejects inactive accounts and authorization-version changes invalidate old sessions. Private disable reason is not exposed. | Required security state; intentionally no operational navigation. |
| Super Admin | All Admin navigation; ownership transfer link in Accounts | Admin grant/revoke, Super Admin transfer `/Admin/Accounts/Transfer`, dependency-safe permanent catalogue deletion, owner-only boundaries | Exactly one active Super Admin; service/transaction authorization remains authoritative. | Required rare role. Operator-only owner recovery is deliberately CLI-only, not a web route. |

### 3.1 Manually constructed or awkward destinations

- `/Events/{slug}/Signup` and `/Events/{slug}/Signups` are intentionally exact-link/unlisted. Admin Manage exposes the signup route, and signup/confirmation expose the table as applicable.
- Direct Admin child pages contain GUIDs but are normally reached through Admin Events → Manage and breadcrumbs.
- Development manual acceptance uses named fixture slugs and credentials by design; those are not production navigation.
- Operator-only owner password recovery has no web action by design and is documented in `README.md`.
- **Verified gap:** both the lifecycle start-readiness blocker and Manage’s blocker-to-action mapping emit `/Admin/Events/Teams/{eventId}`, but no `/Admin/Events/Teams` Razor page exists. The actual team/authority workspace is `/Admin/Events/Draft/{eventId}`.

### 3.2 Development review reachability

Development reset provides bounded role/journey entry points without being production demonstration data:

| Fixture | Review use | Limitation |
| --- | --- | --- |
| Existing local enabled Admin/Super Admin (reset prerequisite) | Owner/Admin navigation, reset, account and event administration | Username is installation-specific, not a deterministic seed identity. |
| `SeedAdminTwo` | Second enabled Admin, concurrent administration, disable/restore target | Disabled-Admin state requires an explicit Admin action; it is not left disabled by reset. |
| `SeedEvidenceParticipant` | Ordinary website participant, My Accounts/My Events, own evidence/history | Scoped to the seeded accepted scenarios. |
| `SeedEvidenceCaptain` / `SeedEvidenceCoCaptain` | Linked Captain and Co-captain authority in TEST 15 | Global shell wording remains “Captain board” for both. |
| Generated team emergency usernames returned by reset | Enabled exact event/team emergency operation | Names are derived per team/scenario; use reset output rather than constructing them. |
| `SeedEvidenceEmergencyDisabled` | Disabled/expired emergency-access coverage | Deliberately cannot demonstrate an enabled journey. |
| `SeedReplacement` | Waiting-list and Live replacement journey | Starts as the TEST 15 waiting replacement. |
| `test-13-dkl-board`, `test-15-dkl-live`, `test-16-signup-lookup`, `test-62-board-publication-setup`, `test-84-evidence-history` | Board draft, Live, signup lookup, board publication and finalized evidence-history states | These focused fixtures do not seed all nine lifecycle states simultaneously; other states require authorized transitions. |

No seed password is duplicated in this Atlas. `DevelopmentScenarioSeeder` and reset output remain authoritative for local-only credentials and generated Captain usernames.

## 4. Event lifecycle consistency audit

### 4.1 State dossiers

| State | Whole-event position | Participant/Captain actions | Admin actions | Timestamps and cutoff | Main blockers/recovery | Stale/concurrency behavior |
| --- | --- | --- | --- | --- | --- | --- |
| `Draft` | Private; not current; may be incomplete | No signup or event-scoped mutation | Identity/schedule/signup/form/board/team setup; schedule/open signup; cancel/discard | Scheduled values may be null. Scheduled opening requires a future `SignupOpensAt`. | Signup readiness: description, capacity, start/end, closing, Discord, form integrity, code/warnings, non-overlap. Resolve through Manage/Schedule/Questions. | Versioned forms/schedule; lifecycle transaction and current-boundary lock reject stale/conflicting requests. |
| `SignupOpen` | Exact-link public signup; not singleton current when windows do not overlap | Create/edit/withdraw/rejoin while open; reserve all selected accounts | Close; manage participants; continue board/team setup; cancel/discard if eligible | `ActualSignupOpenedAt` records actual opening; scheduled value remains history. Closing occurs at configured instant or Admin action. | Capacity/waiting, account reservations, form validation. Close before draft/event start. | Signup response version and event locks prevent lost updates/reservation races. |
| `SignupClosed` | Signup history remains; roster/board publication independently changes destination | No ordinary field edits; pre-draft self-withdrawal only; no restore | Reopen before draft lock; run/finalize/reopen draft; approve/publish board; start; cancel/discard | `ActualSignupClosedAt` records actual closure. Submission cutoff exists but is not active until start/end workflow. | Start requires valid schedule, finalized draft, published board, unambiguous Playing assignment, usable Captain/emergency per team, no singleton current event. | Event version plus serializable/advisory locks; scheduled start attempt is unique per event/scheduled instant. |
| `Live` | Singleton current; public board/roster according to publication facts | Active-account swaps before configured end; private team focus; participant/Captain/emergency evidence; own/team history | Evidence review/correction/reversal; live withdrawal/replacement; evidence codes; WoM correction/refresh; end early | `ActualStartedAt` is actual transition. Scheduled end remains `EventEndsAt`. Normal cutoff remains end +30m. | Ending enters final review. Missing Captain creates an Admin action but does not change state. | Feature services use participant/team/event locks, expected versions and fenced WoM leases. Stale posts fail without residue. |
| `AwaitingFinalReview` | Singleton current; no new gameplay eligibility; public projections remain | Evidence upload/edit/resubmit only through active cutoff; no swaps/focus mutation/new gameplay | Continue review; reasoned reopen uploads; resume premature end before any official history; resolve cycle blockers; finalize | `ActualEndedAt` is effective end (scheduled instant or early actual). `SubmissionsClosedAt` closes at active cutoff. | Resume needs future replacement end, reason, singleton/overlap clearance and no official history. Finalize needs all current-cycle blockers resolved. | Event version + current review-cycle ID required. Identical persisted retry may be idempotent; stale/conflicting writes reject. |
| `Finalized` | Singleton current until archive; official snapshot published | Read-only results/evidence/public history | View history; archive; reasoned unfinalize | `FinalizedAt`; cutoff remains closed. | Archive is normal. Unfinalize rechecks singleton current and supersedes active result version. | Finalization advisory lock, version/readiness recheck and active-snapshot checks prevent duplicate official versions. |
| `Archived` | Not current; read-only public history | Public results; former participant may see only own rejected/withdrawn evidence history | Read audit/history; exceptional reasoned unfinalize | `ArchivedAt`; all submission mutation remains closed. | Unfinalize only when singleton-current policy permits; returns to a new final-review cycle, not Live. | Same finalization locks/version checks; history is never rewritten. |
| `Cancelled` | Terminal. Never-public stays private; previously public retains generic cancelled projection | None | Read preserved setup/history and private reason | `CancelledAt`, actor, private reason; schedulers ignore event. | No ordinary recovery. Protected history is retained. | Destructive lifecycle service locks/rechecks; repeated cancellation is safe/no duplicate effects. |
| `Discarded` | Terminal tombstone; absent from public and active Admin lists | None | Global audit/tombstone only | `DiscardedAt`, actor; slug reserved; removable setup data cleaned | No recovery. Allowed only without protected participant/team/access/submission/evidence history. | Preflight plus transaction; banner cleanup uses durable outbox. |

### 4.2 Every valid transition and recovery

| From → to | Initiator and rendered action | Route | Authoritative service/policy | Scheduled vs actual time | Submission cutoff behavior | Blockers and direct resolution | Stale/retry behavior |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Draft → SignupOpen` | Admin Open now or configured scheduled opening | `/Admin/Events/Schedule/{id}` and Manage handoff | `IEventSignupLifecycleService` / `EventSignupLifecycleService`; `EventStatePolicy` | Manual sets actual opening now. Scheduled retains `scheduled_for` and attempt time. | Not yet an upload authority. | Mode-specific readiness; Schedule/Questions/Manage routes. Failed scheduled opening disables retry and notifies Admins. | Event version, serializable transaction, `(event, scheduled_for)` attempt uniqueness. |
| `SignupOpen → SignupClosed` | Admin Close or scheduled closing | `/Admin/Events/Schedule/{id}` / Manage | `EventSignupLifecycleService`; `EventStatePolicy` | Actual closure records Admin/worker time; transition retains scheduled effective time for worker close. | Unchanged/inactive. | No separate blocker beyond current state/version. | Event lock/version; repeated worker tick sees state changed. |
| `SignupClosed → SignupOpen` | Admin Reopen before draft lock | `/Admin/Events/Schedule/{id}` | `EventSignupLifecycleService`; `BingoEvent.OpenSignups` | New actual opening action; original history retained. | Unchanged/inactive. | Draft lock, readiness/warnings, future valid close, overlap. | Version/current-boundary lock; rejected stale requests leave no mutation. |
| `SignupClosed → Live` | Admin Start event now or one scheduled attempt | `/Admin/Events/Manage/{id}` | `IEventLifecycleService` / `EventLifecycleService`; `EventStatePolicy` | Manual/successful scheduled start uses actual transition time; scheduled attempt retains configured instant separately. | Normal cutoff remains configured end +30m. | Finalized draft, published board, valid Playing assignments, usable Captain/emergency, valid schedule, no current event. Direct routes are Schedule, Draft, Board, Participant; missing-Captain route is currently broken (F-01). | Event version, singleton advisory lock, unique scheduled attempt. Blocked scheduled start never auto-retries; Admin resolves and starts manually. |
| `Live → AwaitingFinalReview` | Worker at end or Admin End event now; early end requires reason | `/Admin/Events/Manage/{id}` | `EventLifecycleService`; `EventStatePolicy` | Scheduled effective end is configured end even if persisted late. Manual early end uses confirmation time; schedule remains visible. | Upload grace remains until active cutoff; gameplay/drop eligibility ends at effective end. | Current state/version only. Review destinations: `/Admin/Review` and `/Admin/Events/Finalize/{id}`. | Lock/version; repeat tick/post sees non-Live. Transition history keeps performed and effective time. |
| `AwaitingFinalReview → Live` | Admin Resume event; confirmation + reason + replacement future end | `/Admin/Events/Manage/{id}` | `EventLifecycleService.ResumePrematureEndAsync`; `EventStatePolicy` | Actual resume is current time; replacement scheduled end is future. Prior end transition stays immutable. | Re-derives ordinary cutoff; clears reopened cutoff and closure. No retroactive eligibility during review interval. | No official finalization history; singleton-current; non-overlap; valid replacement end. | Version/current-boundary lock; repeated/stale/conflicting resume returns safe feedback. |
| `AwaitingFinalReview → Finalized` | Admin Finalize after checklist; strong confirmation | `/Admin/Events/Finalize/{id}` | `IEventFinalizationService` / `EventFinalizationService`; `EventStatePolicy` | Records finalization time and version; consumes current cycle inputs/results. | Does not reopen uploads; cutoff must be resolved/overridden as checklist allows. | Pending evidence, unresolved competitive blockers, completion inspection/corrections. Direct links in Finalize checklist. | Serializable transaction, global advisory lock, expected event version/current cycle, active snapshot idempotency. |
| `Finalized → Archived` | Admin Archive; confirmation, no reason | `/Admin/Events/Finalize/{id}` | `EventFinalizationService.ArchiveAsync`; `EventStatePolicy` | Records `ArchivedAt`; official version unchanged. | Remains closed. | Only current Finalized state. | Serializable/global lock; repeat archive is idempotent. |
| `Finalized → AwaitingFinalReview` | Admin Unfinalize; confirmation + reason | `/Admin/Events/Finalize/{id}` | `EventFinalizationService.UnfinalizeAsync`; `EventStatePolicy` | Active finalization snapshot is marked superseded; a new review cycle becomes authoritative. | Does not reopen uploads. | Singleton-current boundary; active official snapshot required. | Expected version/global lock; stale/conflicting requests reject. |
| `Archived → AwaitingFinalReview` | Admin exceptional Unfinalize; confirmation + reason | `/Admin/Events/Finalize/{id}` | `EventFinalizationService.UnfinalizeAsync`; `EventStatePolicy` | Same immutable version-history behavior; clears archive marker on event aggregate. | Remains closed. | Singleton-current boundary; active official snapshot. | Same locks/version checks as Finalized recovery. |
| `Draft|SignupOpen|SignupClosed → Cancelled` | Admin Cancel; confirmation + reason when protected history exists | `/Admin/Events/Manage/{id}` | `IEventDestructiveLifecycleService` / `EventDestructiveLifecycleService`; `EventStatePolicy` | Records cancellation actor/time; stops scheduled opening. | All event mutation closes. | Requires protected history; otherwise Admin is directed to Discard. | Transactional recheck; generic recipient notifications are part of commit. |
| `Draft|SignupOpen|SignupClosed → Discarded` | Admin Discard; strong confirmation | `/Admin/Events/Manage/{id}` | `EventDestructiveLifecycleService`; `EventStatePolicy` | Records discard actor/time and tombstone. | Not applicable. | Must have no protected participant/team/access/submission/evidence history; otherwise Cancel. | Transactional preflight; cleanup outbox preserves retryable storage deletion. |

### 4.3 Guard and wording consistency

- `EventStatePolicy` is the whole-event capability baseline. `EventMutationCapabilityPageFilter` adds a Razor POST/read-only boundary, while application/domain services reauthorize and recheck transactions. These security/transaction layers are intentional defense in depth, not obsolete duplication.
- User-facing lifecycle classification is nevertheless spread across the filter, `BingoEvent`, PageModels, display projections and services. F-03 recommends one guidance projection without weakening those checks. The `TEAM_ACCESS_MISSING` destination is duplicated between the lifecycle service and Manage mapping; F-01 shows the concrete maintenance failure.
- `EventDisplayPhaseProjection` deliberately renders “Signups closed,” “Draft finalized,” “Board published,” “Event ready,” or “Start postponed” without changing `EventState`. These labels are feature phases, not lifecycle states.
- Current code permits `BingoEvent.EnsureIdentityEditable` through `Live`, while `EventStatePolicy.ConfigureIdentityOrSchedule` and the Admin route filter stop ordinary Live identity POSTs. This internal mismatch is verified but presently fail-closed through the route/service path; see F-04.

## 5. Workflow swimlanes

| Workflow | Participant / public lane | Captain / emergency lane | Admin / system lane | Persistent authority and handoff |
| --- | --- | --- | --- | --- |
| Account/authentication/My Accounts | Password or Discord login; first Discord callback completes website username/password/first OSRS character; manage links, preferred character, labels, saved EHB, rename username | Emergency user consumes one-time setup/reset link and can change password; no My Accounts | Admin creates disabled emergency credential, generates link, enables/disables; Super Admin manages roles/ownership | `Account`, `AccountOsrsCharacter`, token hashes, Discord transition history, account versions; principal routes are `/Account/MyAccounts` and `/Admin/Accounts`. |
| Event creation/schedule/readiness | No private visibility | None | Five-step Create → Manage; configure identity/banner, schedule, signup readiness; worker opens/closes/starts/ends | `BingoEvent`, `EventStateTransition`, scheduled attempt records; `EventSignupLifecycleService` and `EventLifecycleService`. |
| Signup/edit/withdraw/waiting promotion | My Accounts selection → `/Events/{slug}/Signup` → confirmation/edit; withdraw/rejoin; deterministic waiting position | Drafted Captain/Co-captain may inspect expanded pool before finalization | Questions builder; cap/waiting settings; correction/withdraw/restore; promotion notifications/follow-up | `SignupForm`, questions/answers, participant/version, assignments and unique reservations; `SignupService`. |
| Participant correction/internal creation/ownership transfer | Owner access follows explicit participant ownership; old/new owner receive generic transfer notification | No implied ownership from role/name | Participant workspace corrects answers/accounts, creates owned/unowned internal participant, transfers ownership with repeated username | Serializable event lock; response version; participant/account uniqueness; audit and notifications commit atomically. |
| Teams/pre-formed/CSV/draft/finalize/reopen | Public roster only after publication | Linked Drafted leaders may view pool; pre-formed leadership cannot | Create Drafted/Pre-formed teams; manual/CSV external roster; assign roles; run private snake draft; finalize/reopen publication | Team/membership/role transition, draft controller/turn/pick, immutable publication cycles. `/Admin/Events/Draft/{id}`. |
| Catalogue/board build/approve/publish/correct | Published board and managed artwork only | Uses published snapshot | Manual source-drop catalogue; private board derives live catalogue; approve immutable snapshot; publish separately; exceptional published correction | Catalogue versions, `BoardApprovalSnapshot`, active pointer, managed asset IDs, recalculation/audit. |
| Live active accounts/team focus | Owned participant sees planned/current account and swaps among Playing accounts before end | Leaders may swap unlinked pre-formed member; team privately focuses tile/row/column and clears focus | Super Admin may inspect focus read-only; Admin handles roster authority | Append-only swaps and `TeamFocusMarker`; `ParticipantLiveService`, `TeamFocusService`, team SignalR invalidation. |
| Evidence submit/reject/resubmit/approve/reverse | Participant submits for self, edits/withdraws Pending, resubmits Rejected with new image, reads own history | Current leader submits for team; enabled emergency is exact-scoped | Admin Approves or reasoned Rejects; reasoned metadata/credit correction; reasoned reversal | `Submission`, immutable credited snapshot, active asset, predecessor, review actions/contributions; `SubmissionService` and `EvidenceAuthority`. |
| End/grace/cutoff/final review/finalize/unfinalize/archive | No new drop after effective end; uploads may continue through cutoff; public results/archive history | Submission mutation ends at cutoff; emergency auto-disables | Worker/Admin ends; Admin reviews, optionally reopens upload window, resolves cycle, finalizes versions, unfinalizes, archives | Effective transition time, `SubmissionsClosedAt`, review cycle/resolutions/corrections, finalization/placement snapshots. |
| WoM manual lookup/cached competition sync | Explicit player fetch only; manual value remains valid; public pages read local cache only | Same cached team view; no direct upstream call on render | Link/change/clear competition within lifecycle rules; manual refresh shares cooldown; worker refreshes Live events | Signed lookup token, process cache/limiter; persisted sync generation, fingerprint, fenced lease, activity rows; never a lifecycle blocker. |

## 6. Capability-by-state matrix

Legend: **Public** is anonymous read; **Participant** means explicit owned website participant; **Leader** means current linked Captain/Co-captain or enabled scoped emergency where stated; **Admin** means enabled Admin/Super Admin reauthorized by the command service.

| Capability | Draft | SignupOpen | SignupClosed | Live | AwaitingFinalReview | Finalized | Archived | Cancelled | Discarded |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Configure identity/schedule | Admin; normal route policy | Admin; confirmations/locks | Admin; confirmations/locks | No ordinary route (internal domain mismatch F-04) | No | No | No | No | No |
| Configure signup/form | Admin; before response shape flexible | Admin; response locks apply | Admin; presentation/optional replacement until draft lock | No | No | No | No | No | No |
| Participant signup/edit | No | Owned website account; exact link; cutoff/validation/reservations | No ordinary edit; pre-draft withdrawal only | No | No | No | No | No | No |
| Reopen signup | No | — | Admin only before draft lock/readiness | No | No | No | No | No | No |
| Team/draft operation | Admin setup | Admin setup; no active pick until approved preconditions | Admin private draft/finalize/reopen before start | Current roster corrections only through approved Live participant flow | Current vacancy/final-review operational actions only | Read history | Read history | Read-only preserved data | None |
| Board edit/approve/publish | Admin private | Admin private | Admin; publish after roster finalization | Only confirmed published-correction workflow | Published correction/review-safe actions as explicitly supported | Read official board; correction route blocked | Read | Prior projection only if public | None |
| Start event | No | No | Admin or scheduled system; full readiness | — | No | No | No | No | No |
| Active-account swap | No | No | No | Owned participant; leader only for unlinked pre-formed teammate; before end | No | No | No | No | No |
| Team focus | No | No | No | Current team members/leaders; Super Admin inspect-only | Projection may remain but mutation ends with event time/state | No mutation | No mutation | No | No |
| New evidence | No | No | No | Participant/leader/emergency through active cutoff | Same only through active cutoff; gameplay eligibility still ends at effective end | No | No | No | No |
| Admin evidence review | No | No | No | Admin | Admin, including after cutoff | Read-only unless unfinalized | Read-only unless unfinalized | No mutation | None |
| Competition sync | Configure Admin pre-Live; no worker | Configure Admin pre-Live | Configure Admin pre-Live | Admin correction/manual refresh + worker | Frozen cache | Frozen cache | Frozen cache | Read-only/no refresh | None |
| Finalize | No | No | No | No | Admin after current-cycle blockers | Idempotent view | No | No | No |
| Resume premature end | No | No | No | — | Admin, reasoned, no official history, future end | No | No | No | No |
| Archive | No | No | No | No | No | Admin confirmed | — | No | No |
| Unfinalize | No | No | No | No | — | Admin reasoned + singleton check | Admin reasoned + singleton check | No | No |
| Cancel/discard | Admin according to protected history | Admin according to protected history | Admin according to protected history | No | No | No | No | — | — |
| Public read | None until explicitly public | Exact signup/table | Exact signup/table; roster/board when separately published | Published board/roster/evidence | Same plus grace/review state-safe labels | Official results | Official history | Only previously public generic projection | None |

## 7. Scheduled and automatic behavior

| Behavior | Trigger | Persisted idempotency fact | Retry/failure behavior | Human recovery |
| --- | --- | --- | --- | --- |
| Signup opening | 30-second lifecycle worker; Draft + enabled due `SignupOpensAt` | `ScheduledSignupOpeningAttempt` unique for `(event, scheduled_for)` plus state transition/audit | Full readiness rerun. Blocker/unacknowledged warning records one failed attempt, disables delayed retry, notifies enabled Admins. | Fix Manage/Schedule/Questions and open manually or reschedule. |
| Signup closing | Worker; SignupOpen + due close | Event state and transition/audit | Catch-up closes once; later ticks see non-open state. | Admin can reopen before draft lock. |
| Event start | Worker; pre-Live + due start | `ScheduledEventStartAttempt` unique for `(event, scheduled_for)` | Success starts once. Failure records blocker codes/action and never surprise-retries after resolution. | Follow resolution links, then **Start event now**. |
| Event end | Worker; Live + due end | Event state plus effective transition history | Late catch-up uses configured end as effective time. | Admin enters review; premature end may be reasoned-resumed before official history. |
| Submission closure/emergency cutoff | Worker; active cutoff due | `BingoEvent.SubmissionsClosedAt`; emergency access cutoff-disable facts | Repeated checks are no-ops after persisted closure. | Admin may reasoned-reopen uploads; emergency credential must be explicitly re-enabled. |
| Postponed diagnostics | Failed scheduled signup opening/start | Attempt row, blocker codes, unresolved/resolved time; Admin action derived from unresolved state | Start is not automatically retried; opening failure disables scheduled intent. | Manage action routes to current readiness and explicit manual command. |
| Banner cleanup outbox | Event/banner removal leaves storage cleanup work | Durable `EventBannerCleanup` row/status/attempt fact | `EventBannerCleanupService` retries pending cleanup; already-missing object completes safely. | Inspect logs/outbox and correct storage availability; retry worker. |
| Evidence asset cleanup | Failed pre-commit create/resubmit after the asset was stored | No persisted cleanup outbox/idempotency record; the absence of a committed `EvidenceAsset` is authoritative | `SubmissionService` makes one non-cancelled best-effort delete in its failure path; there is no background retry family. | Operator storage inspection and direct object cleanup for an exceptional orphan. |
| WoM normal sync | Dedicated 30-second worker; configured Live sync with normal/retry due | Per-event generation, due times, retry count, assignment fingerprint, lease owner/expiry | One initial + up to 3 retries; honor Retry-After or ~1/2/4 min. Separate 2h normal anchor. Permanent invalid/not-found fails closed. | Admin views exact status/missing names; fixes competition link or uses cooldown-respecting manual refresh. |
| WoM pause/resume | Leaving/returning to Live | Persisted normal/retry schedule and latest generation | No refresh outside Live. Future due waits; overdue return permits prompt fetch. | Resolve lifecycle legitimately; never force via page view. |
| Personal notifications | Transactional domain/service triggers | Durable `PersonalNotification`; deterministic IDs for retry-sensitive transitions | Reading only sets `ReadAt`; it never changes source workflow state. | Follow notification destination; authoritative record controls availability. |
| Admin action queue | Shell query over unresolved authoritative records | Pending submission, follow-up completion, unresolved start attempt, open vacancy, missing Captain | Projection disappears only when underlying work resolves; not when a notification is read. | Follow direct action route and complete the domain operation. |
| SignalR invalidations | Progress approval/reversal, Admin draft/board changes, team focus changes | Database state is authoritative; hub message contains invalidation context only | Client refreshes; public progress also has 30-second fallback. | Reload/follow route if realtime connection fails. |

## 8. Data and history ownership

| Data family | Owner / authority | Current vs immutable/derived | Visibility | Lifecycle retention |
| --- | --- | --- | --- | --- |
| Website account, Discord link, password/security history | `Account`; stable Discord ID is a login link, not profile authority | Current role/state on account; Discord transitions, password tokens and audits are historical | Account/Admin; secrets and hashes never public | Retained through disable/role changes; no website-account deletion/merge in v1. |
| OSRS character and My Accounts links | Global `OsrsCharacter`; per-account `AccountOsrsCharacter` metadata | Character identity shared; link active/preferred/label/saved EHB is current; unlink history retained | Owner/Admin; event pages use assignment snapshots, not website username | Global unlink never deletes event assignments/evidence/history. |
| Participant, signup answers and form | Event owns participant/answers/form definitions | Participant/status/current response version; prior question/answer shape and disabled replacements retained | Exact-link public answers excluding identity/payment/notes; full Admin; owned private controls | Participant remains across withdrawal, transfer, draft, finalization and archive. |
| Event assignments and swaps | Event participant owns registered assignment; event enforces unique current reservation | Assignments release but remain history; swaps append immutable activation intervals | Participant/team/Admin; credited snapshot may become public when evidence approved | Preserved through replacements, role changes, finalization and archive. |
| Teams, memberships, role transitions, draft picks/publications | Event/team aggregate | Current membership separated from immutable picks, role transitions and publication cycles | Current roster public after publication; controller/audit/private draft excluded | Withdrawal/replacement never rewrites draft/publication ledger. |
| Catalogue and board | Global source-drop catalogue; event owns board | Draft derives live catalogue; approval/published snapshots immutable; active pointer selects current | Catalogue Admin; approved/published board public | Catalogue edits never rewrite historical approved boards. |
| Evidence, assets, reviews, contributions | Event/team submission; credited participant and character are immutable snapshots | Submission status/current asset; review actions, predecessor, contributions append/retain; progress derived from active approved contributions | Pending/rejected/withdrawn private by authority; active Approved public; Admin complete | Finalized/Archived readable in allowed scope; reversal retains prior evidence/review. |
| Final-review cycles and results | Event owns current cycle and every official version | Cycle resolutions/corrections and result snapshots immutable; active official version selected | Admin detail; official placements public in Finalized/Archived | Unfinalization supersedes; never deletes prior versions. |
| WoM cache/generations | Event integration state and current Playing-assignment fingerprint | Latest matching generation is display authority; rows are derived cache, older generations retained internally | Privacy-safe totals/coverage public/team; exact missing accounts Admin only | Stops mutating outside Live; latest state remains readable but is not an official-results snapshot. |
| Personal notifications/Admin actions | Recipient owns notification; underlying workflow owns action resolution | Notification read state mutable; Admin actions derived from current unresolved records | Recipient; Admin action projection only to enabled Admin/Super Admin | Notifications retained; reading cannot resolve operational work. |

## 9. Notification and action map

### 9.1 Personal notifications

| Trigger | Recipient | Destination | Content boundary | Idempotency owner |
| --- | --- | --- | --- | --- |
| Admin grant/revoke/restore | Target website account | `/notifications` → account-safe target | Generic role/state message; private disable reason excluded | Account administration transaction; some rows use ordinary GUID because mutation itself is single/locked. |
| Event cancellation | Explicit account-owned participants | Best remaining event route via notification redirect | Generic cancellation only; Admin reason excluded | Destructive lifecycle transaction. |
| Waiting promotion / restore / withdrawal | Owned participant; enabled Admins for promotion | Confirmation or Admin Manage/Participant route | Participant status and event; no custom answers/payment/notes | Signup lifecycle transaction; retry-sensitive transitions use deterministic target/recipient IDs where implemented. |
| Ownership transfer | Old and new owners | Participant/event route | Generic access-changed message; no private form/history payload | Serializable transfer transaction. |
| Team role change | Explicitly owned member | `/Events/{slug}/Teams` | Generic role change; no draft-controller/audit detail | Team Captain authority transaction. |
| Live withdrawal / replacement | Departed/replacement and current linked leadership/Admin recipients as applicable | Corrected Participant route with event + participant IDs | Private-safe event/team context; no predecessor credit/private notes | `SignupService` deterministic purpose/target/recipient ID. |
| Evidence rejection | Credited owner and current linked Captain/Co-captains | `/Captain/Submissions/{id}` through scoped notification flow | Event/tile/drop/reason; no unrelated private evidence | `SubmissionService` deterministic recipient/review-transition ID. |
| Official results | Every explicitly owned participant | `/Events/{slug}/Board` | Public result availability only | `EventFinalizationService` deterministic event/recipient ID. |
| Scheduled opening/start failure | Enabled Admin/Super Admin | `/Admin/Events/Manage/{id}` | Blocker descriptions needed for operation; never participant/private-answer payload | Unique scheduled attempt plus transactional notification. |

### 9.2 Admin action queue

| Action | Trigger/query | Destination | Resolution |
| --- | --- | --- | --- |
| Evidence review | `SubmissionStatus.Pending` in active event | `/Admin/Review/Details/{submissionId}` | Approve/Reject/Withdraw state change; reading does not resolve. |
| Waiting-list follow-up | Incomplete `WaitingListPromotionFollowUp` | `/Admin/Events/Participant/{eventId}/Participants/{participantId}` | Explicit **Mark follow-up complete** stores actor/time. |
| Postponed start | Unresolved failed scheduled-start attempt | `/Admin/Events/Manage/{eventId}` | Successful manual start resolves attempt after blockers clear. |
| Open vacancy | Withdrawn current member with no replacement membership | Correct Participant route | Leave open or fill through waiting/internal replacement. |
| Missing Captain | Active team without linked current Captain or enabled emergency credential | Currently `/Admin/Events/Manage/{eventId}` in shell; readiness blocker incorrectly emits `/Admin/Events/Teams/{id}` | Assign current Captain in Draft workspace or explicitly enable team emergency credential. |

**Document conflict:** `DATA_MODEL.md` still describes a richer `AccountNotification` shape with explicit event/participant/type/path fields and universal recipient-transition uniqueness. Accepted code uses `PersonalNotification(Id, RecipientAccountId, Title, Detail, Route, CreatedAt)` and applies deterministic IDs selectively. The code/final Slice plans are current evidence; the older data-model wording should be reconciled in a later authorized documentation pass.

## 10. Complete feature inventory and classification

Classifications describe current product review, not permission to change anything.

| Capability | Classification | Evidence and rationale |
| --- | --- | --- |
| Password + Discord website authentication | Required | Primary normal-account entry; secure cookie/version validation and no persisted OAuth tokens. |
| My Accounts and event character assignments | Required | Explicit identity separation, saved EHB default, reservation and history authority. |
| Emergency Captain credential | Rare/edge-case; required | Approved fallback when no linked Captain can operate a team; exact scope and cutoff disablement. |
| Operator-only owner recovery | Rare/edge-case; required | Deliberately CLI-only high-risk recovery, not missing web navigation. |
| Event creation/schedule/readiness | Required | Entry to all event workflows and automated lifecycle. |
| Derived display phases | Awkward but required | Prevent state proliferation, but labels can be mistaken for lifecycle states without guidance. |
| Signup questions/capacity/waiting | Required | Core participant intake with public-answer/private-admin boundary. |
| Unlisted exact-link signup table | Required; intentionally awkward discovery | Privacy/product decision: not homepage-listed; Admin copies exact link. |
| Participant correction/internal creation/ownership transfer | Rare/edge-case; required | Operational recovery with concurrency/privacy protection. |
| Drafted and pre-formed teams | Required | Both community-organized and external roster models are approved. |
| Pre-formed roster CSV | Rare/edge-case; required | Team-scoped operational import; deliberately not ordinary signup CSV. |
| Private snake draft/controller lease | Required | Core competitive roster workflow and multi-Admin protection. |
| Immutable roster publication cycles | Required | Preserves public/history corrections. |
| Manual source-drop catalogue | Required | Version-one board source authority. |
| Non-web Wiki import implementation | Possibly unnecessary (retained, zero web route) | Web import route was removed; retained service has no approved version-one web journey. Keep as deferred cleanup/product decision, not an Atlas defect. |
| Board approval separate from publication | Required | Accepted product rule preventing accidental publication. |
| Published board correction workflow | Rare/edge-case; required | Exceptional retained-history correction, including Live. |
| Active-account swaps | Required | Live participant identity/credit boundary. |
| Team focus | Required | Private coordination feature; public/opponents see none. |
| Super Admin cross-team focus inspection | Rare/edge-case | Explicit read-only troubleshooting; no persistent inspection state. |
| Participant/Captain evidence submission | Required | Authoritative source of progress. |
| Rejected linked resubmission | Required | Replaces obsolete Request Changes/same-record resubmission. |
| Active Request Changes workflow | Obsolete | Removed by accepted Slice 8; only retained historical metadata may remain. |
| Active duplicate classification | Obsolete | New unusable evidence is reasoned Rejected; historical duplicate audit metadata only. |
| Evidence privacy request/hidden-approved state | Obsolete | Removed; public active Approved or reasoned reversal is authoritative. |
| Admin evidence upload | Obsolete | Removed by approved Slice 8 decision. |
| Public approved evidence | Required | Progress transparency with scoped private states. |
| Final-review cycle/checklist | Required | Integrity gate for official results. |
| Premature-end resume | Rare/edge-case; required | Approved recovery before any official history, without erasing the end interval. |
| Live withdrawal/replacement | Rare/edge-case; required | Preserves history and supports operational vacancies. |
| Official result versions/unfinalization/archive | Required | Immutable competitive history and recovery. |
| Personal notifications | Required | Durable recipient information; not workflow authority. |
| Admin action queue | Required | Operational source-of-work projection separate from read state. |
| SignalR progress/Admin/focus channels | Required progressive enhancement | Invalidation only; routes/database remain authoritative. |
| Explicit WoM player EHB lookup | Required optional integration | User action only; manual fallback retained. |
| Cached WoM competition activity | Required optional integration | One snapshot request, local projection, never lifecycle authority. |
| WoM competition create/edit/delete/update-all | Missing/deferred, not required | Explicit Slice 10 non-goal. Existing competition ID only. |
| Global Rules document and source-controlled how-to pages | Required (missing functionality tracked separately) | Roadmap lists the selected public-guidance slice, but accepted code has no such routes; event `PublicRules` compatibility remains. Needs a separately authorized functional slice, not UI work by implication. |
| `/Admin/Events/Teams/{id}` missing-Captain route | Unreachable | Emitted independently by `EventLifecycleService` and the Manage blocker mapping, but no route exists; see F-01. |
| `ApplicationDependencies` wrapper and listed no-op dependencies | Possibly unnecessary | Deferred Ponytail cleanup only; no demonstrated product defect. |

## 11. Findings and decision ledger

### 11.1 Verified findings, ranked by user impact

| ID | Impact | Finding | Evidence | Recommended decision/UI priority |
| --- | --- | --- | --- | --- |
| F-01 | High | Missing-Captain start-readiness resolution route is unreachable. | `EventLifecycleService.EvaluateStartAsync` and Manage’s blocker-to-action mapping both emit `/Admin/Events/Teams/{id}`; route inventory has only `/Admin/Events/Draft/{id}` for teams/draft. | Approve a small functional correction before or within the relevant UI pass: route to Draft team section (preferably anchored) and verify rendered action navigation. |
| F-02 | Low (resolved here) | Roadmap/status discoverability summaries were materially stale at the accepted source commit. | The baseline roadmap header said implementation not started; `CURRENT_STATUS.md` “Remaining work” still said package Slice 10 although the source commit was already accepted on main; README said only Milestones 1–7. This Atlas handoff corrects those current-summary surfaces while retaining historical evidence. | No product decision remains. Keep the concise current status authoritative and consolidate historical prose only in a later authorized documentation pass. |
| F-03 | Medium | Admin progression guidance is distributed across Manage, Schedule, Draft, Board, Finalize, `EventDisplayPhaseProjection`, the route filter, and service blocker strings. | Accepted code has several correct direct links, but no single normalized progression model; one link already diverged (F-01). | Milestone 9 Pass 3 should render a single state/substate checklist driven by authoritative projections, retaining service guards. |
| F-04 | Medium | Ordinary Live identity edit is blocked by policy/filter, while `BingoEvent.EnsureIdentityEditable` still permits Live. | `EventStatePolicy.ConfigureIdentityOrSchedule` permits only pre-Live states; `EventMutationCapabilityPageFilter` uses it; domain method includes Live. | Product decision: either explicitly approve a narrow Live identity correction workflow or align the domain guard with the ordinary route policy. Do not change silently. |
| F-05 | Medium | Notification source-of-truth documentation no longer matches accepted persistence/idempotency shape. | `DATA_MODEL.md` describes `AccountNotification` with richer fields/universal idempotency; code uses `PersonalNotification` and selective deterministic IDs. | Reconcile data-model wording in an authorized docs pass; keep current privacy/idempotency behavior unchanged until reviewed. |
| F-06 | Medium | The selected permanent Rules/how-to functionality is not implemented, while event-owned `PublicRules` compatibility still exists. | Roadmap selected public-guidance slice vs route/code inventory. | Decide whether this functional slice must precede Milestone 9; do not hide it inside UI overhaul. |
| F-07 | Low | Co-captain authority is presented through a global “Captain board” label. | Layout checks the Captain claim, while membership distinguishes Captain/Co-captain and readiness treats them differently. | Milestone 9 terminology review: use a neutral “Team workspace” label while keeping Captain-only readiness wording explicit. |

### 11.2 Inferred concerns

| ID | Concern | Why it is inferred | Review question |
| --- | --- | --- | --- |
| I-01 | An Admin can see a generic Manage link to Public signup even when the event phase will make that route unavailable or redirect elsewhere. | Markup shows the link unconditionally; route behavior is state-aware, but the full rendered state matrix was not manually walked in this audit. | Should Manage show the best current public destination instead of always labelling the entry “Public signup”? |
| I-02 | Action-queue ordering may bury critical work because five sources are concatenated and only the first eight are shown. | `SharedShellService` takes per-source rows and then `items.Take(8)`; no cross-source severity/time ordering exists. | Should Milestone 9 define priority/age ordering and “view all” grouping? |
| I-03 | The optional WoM cache is visible in finalized/archive history but is not part of official-result snapshots. | Explicit approved design; could still surprise readers who assume every final page number is official history. | Should UI label cached Activity EHB as an external frozen auxiliary metric distinct from official placements? |

### 11.3 Manual/visual uncertainties

- Verify all nine lifecycle states and the derived display phases at desktop and narrow widths during Milestone 9; this audit did not run the application.
- Verify keyboard/focus behavior for the notification popover, dense Admin event controls, lifecycle confirmation forms, drawer/overlay/fallback transitions, and Atlas-related future UI proposals.
- Slice 10 manual evidence already records visually broken Activity EHB presentation and table layout; presentation is a UI-overhaul priority, not a functional defect.
- Archived evidence currently opens the raw image directly; manual acceptance approved function, while viewer presentation remains deferred.
- Danish review for some late participant/Captain surfaces remains deferred to the approved UI-overhaul boundary.

### 11.4 Decision ledger

No implementation decision is made here. Product review should explicitly decide:

1. whether to correct F-01 before Milestone 9 or as the first bounded functional correction inside the affected pass;
2. whether Live identity correction is intended (F-04) or the domain allowance is stale;
3. whether the selected global Rules/how-to slice (F-06) must be completed before UI overhaul;
4. whether Admin progression and action-queue prioritization should be normalized in Milestone 9;
5. which obsolete/possibly unnecessary capabilities may be removed only after separate approval.

## 12. Deferred Ponytail cleanup (non-blocking; not Atlas defects)

These findings are deliberately deferred. They do not block the Atlas or Milestone 9 and do not authorize cleanup:

1. Remove 51 unreferenced Bootstrap/jQuery distribution variants/source maps while retaining referenced assets and licenses.
2. Remove the retired seven-argument `EventParticipant` constructor after updating stale test fixtures.
3. Remove the legacy integer `SnakeDraftOrder.GetNextEligibleTurn` test-only overload.
4. Remove `Team.ImageUrl` (always null) and ignored `Update` slug/imageUrl parameters.
5. Remove zero-caller `DraftSession.ConfigureTargetSize`.
6. Remove the unreferenced `ApplicationDependencies` wrapper.
7. Remove constructor dependencies retained only through no-op assignments in Manage/Participant/Finalize.

The approximately 82,540-line cleanup estimate is overwhelmingly vendor distribution files and does **not** represent architectural complexity.

## 13. Evidence register and limitations

### Verified evidence

- Clean accepted source commit `2301d63ae166873a750266ce5ee6a087f3039054`.
- Complete Atlas gate in `IMPLEMENTATION_ROADMAP.md`.
- `EventState`, `EventStatePolicy`, `BingoEvent`, route filter, lifecycle/signup/finalization/destructive services and workers.
- Razor `@page` route inventory, shared layout, Admin/Captain/My Events navigation, breadcrumbs and direct action links.
- Final accepted Slice 1–10 plans and Development seed identities/states.
- Account, signup, team/draft, board, evidence, finalization, notification, and WoM code boundaries named in this Atlas.

### Limitations

- This was a read-only documentation/code audit. The application test suite and build were intentionally not run.
- The application was not connected to PostgreSQL or external providers.
- The standalone HTML received static validation; visual browser rendering was unavailable because the in-app browser blocks local `file:` URLs. No application page/state was re-rendered for this audit.
- A code route’s existence proves addressability, not visual clarity or a complete manual journey; such uncertainty is labelled.
- Source documents contain historical/superseded sections. Final accepted plans and accepted code were treated as current evidence, with conflicts recorded above.

## 14. Source identifiers

Key policies/services cited and verified in the accepted tree:

- `EventStatePolicy`, `EventDestinationPolicy`, `EventDisplayPhaseProjection`, `EventMutationCapabilityPageFilter`
- `IEventSignupLifecycleService` / `EventSignupLifecycleService`
- `IEventLifecycleService` / `EventLifecycleService`
- `IEventDestructiveLifecycleService` / `EventDestructiveLifecycleService`
- `IEventFinalizationService` / `EventFinalizationService`
- `ISignupService` / `SignupService`
- `IParticipantLiveService` / `ParticipantLiveService`
- `ITeamCaptainAuthorityService` / `TeamCaptainAuthorityService`
- `ITeamFocusService` / `TeamFocusService`
- `ISubmissionService` / `SubmissionService`
- `IEvidenceAuthority` / `EvidenceAuthority`
- `IEventCompetitionSynchronizationService` / `EventCompetitionSynchronizationService`
- `EventLifecycleWorker`, `EventCompetitionSynchronizationWorker`, `SharedShellService`

The Atlas should be regenerated or amended when approved product behavior, authoritative routes, lifecycle policies, or major workflow ownership changes.
