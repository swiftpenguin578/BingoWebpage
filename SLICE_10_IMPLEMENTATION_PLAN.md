# Slice 10 Implementation Plan

## Final restricted remediation contract (2026-08-03)

The final restricted re-review requires three bounded corrections. The competition mapper and Development transport use the official `GET /competitions/:id` shape: `participations[].player`, `deltas[].metric`, `deltas[].values.gained`, and top-level `updatedAt`; no competition write DTOs or endpoints are added. A performed manual refresh is successful only when the upstream result is successful. Rate-limited, unavailable/transport, not-found, invalid/malformed, and other performed failures return `Succeeded=false`, retain persisted retry/cooldown state, and expose the known retry/reset time to localized Admin feedback. Valid manual signup data remains independent of optional WoM failure.

Development reset remains idempotent and makes no WoM calls. It now seeds one minimal `TEST 16 — Signup lookup` event in `SignupOpen` with an empty roster, links `SeedAdminTwo` to `Rasmus Zebak` with saved EHB `12.5`, and makes TEST 15's local complete cache three hours old so manual refresh is due. The Development-only fake setting `AutomaticSynchronizationEnabled=false` pauses the due worker for deterministic manual checks; it is ignored outside Development. The existing TEST 15 Manage surface has a fixture-scoped Development-only action that makes only the next manual refresh due and is safe to repeat. No new persistence, route family, production job, or generalized simulator is introduced.

## Approved Slice 10 change control — partial competition generations (2026-08-03)

The user explicitly approved replacing the earlier all-or-nothing missing-account rule before implementation of this correction. A successful competition response remains a successful generation even when expected current `PLAYING` accounts are absent. Every matched expected account gets a fresh current-generation activity row; a missing account gets no row and is never written or displayed as zero, and older-generation values are never carried forward. A generation with one or more missing accounts is `Partial`/provisional: available participant/team totals, deterministic provisional ranks, and participant/account coverage remain visible when at least one expected account matches; participant totals include only matched current `PLAYING` accounts, team averages divide only by participants with a matched account, and zero matches show partial/unavailable with no rankings. Manage retains the exact missing names for enabled Admin/Super Admin accounts; public Board/TeamBoard show only privacy-safe partial coverage/count. No notifications, schema/table/migration/job/route/service framework, or generalized completeness system is added. Assignment fingerprint fencing, expected-account filtering, Alt/informational/released exclusion, and all other Slice 10 behavior remain unchanged.

## Approved outcome

Slice 10 adds a deliberately small, read-only Wise Old Man integration for two purposes only:

1. An explicit user-requested EHB lookup for a regular OSRS account in My accounts or an event signup/edit form.
2. A cached event-competition EHB activity projection, synchronized while the event is `Live` and displayed as participant and team activity.

Wise Old Man is supplementary. Manual EHB entry remains authoritative for signup snapshots, cached activity remains informational, and no Wise Old Man outage or incomplete competition membership may block an event lifecycle action.

The user approved this product behavior on 2026-08-03. The official Wise Old Man v2 API documentation was reviewed on the same date. The independent read-only implementation-readiness review identified four contract gaps and two calculation decisions; the user approved the recommended resolutions below. This plan is the authoritative implementation baseline and is pending only a restricted read-only recheck of those resolved findings.

## Approved product decisions

- An Admin links an event to one existing Wise Old Man competition by entering its competition ID. The application does not create, edit, start, end, or verify Wise Old Man competitions and stores no Wise Old Man verification code.
- During event creation, an optional competition ID may validate the competition and prefill the event schedule with its exact UTC start/end. Linking a competition to an existing pre-Live event compares both schedules and explicitly offers to update the event schedule to the exact competition instants through the existing schedule validation/audit boundary; nothing changes silently.
- Without schedule synchronization, the competition start/end must match the configured Bingo start/end within five minutes. During `Live`, a competition correction is allowed only when the replacement competition already matches that schedule; schedule synchronization is unavailable. AwaitingFinalReview, Finalized, Archived, and Cancelled integration configuration is read-only.
- Manual account lookup is available only through an explicit **Fetch from Wise Old Man** action in My accounts and regular-account EHB controls in signup/edit. It is never triggered by typing, selecting an account, rendering a page, saving an unrelated form, or viewing a public page.
- Manual lookup uses `GET /players/{username}`. The application never calls the active player-update `POST /players/{username}` endpoint.
- A successful lookup fills the current control. Manual editing remains available. My accounts persists only the numeric saved-EHB default; it does not retain durable WoM source/time. A later signup using that saved default is Manual. Only a fresh signed lookup submitted from the signup form can persist `WiseOldMan` plus fetch time on the event assignment; a changed value is Manual.
- During `Live`, one event-level synchronization requests that competition's details with EHB deltas at most once every two hours by default. The initial automatic fetch is first due two hours after the event’s initial transition into `Live`; subsequent successful normal fetches remain two hours apart. Leaving `Live` stops automatic calls without resetting the persisted schedule, and a legitimate return to `Live` preserves that schedule: a future due time waits, while an overdue due time permits one prompt fetch before the normal two-hour anchor resumes. Lifecycle switching itself does not fetch. One competition response supplies all relevant participant data; the application does not make a separate request for every player or team.
- Every event assignment whose role is `PLAYING` contributes its full competition EHB delta to its Bingo participant, regardless of which playing account was active for evidence at a particular time. Participant totals are then summed into the current internal Bingo team. No minute-by-minute swap/replacement attribution is introduced.
- `INFORMATIONAL`/Alt accounts and Yes/No support-alt answers are never sent to Wise Old Man and never enter standings.
- If any expected regular account is absent from a successful competition response, the generation is partial/provisional. Matched current-generation rows remain authoritative; missing accounts have no row, are never treated as zero, and older-generation values are not carried forward. Public/team pages show available totals and deterministic provisional rankings when at least one account matches, with privacy-safe missing-count and participant/account coverage; Admin status identifies the exact missing accounts. Zero matches show partial/unavailable with no rankings.
- A newer partial generation replaces older displayed data just as a newer complete generation does. Older rows remain retained internally for recovery/diagnosis, but the latest generation is authoritative and its matched subset is frozen when synchronization stops.
- When a retryable failure follows a partial success without a generation, assignment-fingerprint, or configuration change, the matching generation’s rows remain the last available cache while the projection reports temporary unavailability and preserves retry facts. Permanent invalid/not-found failures do not retain a partial projection.
- Team average is the average of participant totals for current team members with at least one matched account in the latest generation, not the average of OSRS accounts. Every participant tied for the highest available total shares the provisional MVP; tied names are displayed deterministically.
- Synchronization stops outside `Live`. Retained cached results remain readable after event end/finalization/archive but are not refreshed. If an event is legitimately resumed to `Live`, normal due synchronization resumes.
- No Wise Old Man notification family is added. Admin integration status and structured logs carry failures; participant/public pages receive accurate cached/incomplete/unavailable states.

## Official API contract used by this plan

- Base URL: `https://api.wiseoldman.net/v2`.
- Unauthenticated allowance documented at review time: 20 requests per 60 seconds; an optional API key may raise the allowance to 100.
- Observe `RateLimit-Limit`, `RateLimit-Remaining`, and `RateLimit-Reset`; honor `Retry-After` on `429`.
- Use a contactable configured `User-Agent`. An optional API key is configuration/secret state only and is never stored in PostgreSQL or committed.
- Player lookup reads `GET /players/{username}` and its top-level `ehb`.
- Competition synchronization reads `GET /competitions/{id}` with `metric=ehb` and consumes `participations[].player`, `deltas[].metric`, `deltas[].values.gained`, and top-level `updatedAt`, not deprecated progress/levels fields.
- Normal automated and load tests use a fake or recorded client. A real-API smoke check, if performed, is a separate single bounded request and not part of the normal suite.

## Request budget, monitoring, and retry policy

All manual lookups and automatic competition requests pass through one application-wide Wise Old Man client and limiter.

- Count manual and automatic requests together for the current server process. Version one deliberately runs one application replica, so no distributed limiter or Redis dependency is added.
- Retain a reserve of the final three requests in the current Wise Old Man window. Manual fetch is rejected locally once the observed remaining allowance reaches that reserve; no external request is sent.
- The manual message is **Wise Old Man is temporarily busy. Try again in about 1 minute.** When a later reset is known, show the accurate reset time instead.
- Track and expose to enabled Admins the observed limit, remaining allowance, reset time, last request/success/error, last `429`, event's last successful synchronization, next permitted attempt, and current cached-result status. Structured logs retain the same operational facts without character secrets beyond the account name required to diagnose the request.
- A manual refresh of competition activity obeys the same two-hour event cooldown as automatic refresh. When cached data is still fresh, return the cache timestamp without contacting Wise Old Man.
- One event refresh may be in flight at a time. A short database-backed event synchronization lease prevents duplicate automatic/manual work and survives concurrent worker ticks.
- A local rate-limit block never counts as a successful automatic synchronization. Each scheduled cycle has one initial attempt plus at most three retries for temporary failures—local rate-limit admission, `429`, timeout/connection failure, or `5xx`.
- Respect `Retry-After`; otherwise schedule retries after failures 1–3 at approximately 1, 2, and 4 minutes. Failure 4 exhausts that cycle. Persist the next retry and the separate next normal-cycle time anchored to the cycle start; never derive the next normal cycle solely from a retry or allow exhaustion to re-admit immediately. Retry scheduling may add small deterministic jitter so several due events do not burst simultaneously.
- Invalid/not-found/inaccessible competition responses are permanent configuration errors and are not retried in a tight loop.
- After the initial attempt and three retries fail, retain the last successful cache, show the failure to Admins, and try again at the next normal two-hour cycle. A later success resets the failure count.
- The limiter fails closed when the remaining/reset state is uncertain near the reserve. It never attempts to maximize use of the last requests.
- Limiter admission is serialized. With no observed window after process start, admit exactly one bootstrap request and block other callers until that request completes. Release bootstrap admission on every outcome. With trustworthy headers, enforce the observed window and final-three reserve for both manual and automatic traffic. After timeout, connection failure, or malformed/missing headers, fail closed for at least the documented 60-second window before admitting exactly one new bootstrap request. Interpret documented reset/retry values as seconds from the observation time.

## Core invariants and enforcement ownership

| Invariant | Single best enforcement layer |
|---|---|
| Only explicit user actions fetch account EHB | WoM application service/handler command boundary |
| Only `PLAYING` assignments enter activity | Competition synchronization query/service |
| Manual values and event snapshots are never silently rewritten | Existing signup/My accounts save services |
| WoM provenance cannot be forged by hidden form fields | Signed short-lived lookup result verified by the save service |
| Public/page traffic never calls WoM | Shared cached projection services; no WoM client dependency in public PageModels |
| One event synchronization runs at a time and stale requests cannot overwrite newer data | Fenced database lease token plus final synchronization transaction |
| Global request reserve and remote reset are honored | One shared WoM client/limiter |
| Missing accounts have no row, are not counted as zero, and partial coverage is explicit | Cached generation missing diagnostics and projection policy |
| WoM failure never blocks lifecycle | Lifecycle/readiness services remain independent of WoM state |
| Finalized/Archived activity does not change | Worker eligibility query plus synchronization transaction recheck |

## Pass 10.1 — explicit account EHB lookup

- Add one typed Wise Old Man HTTP client with configured base URL, contactable User-Agent, optional API key, bounded timeout, shared limiter, response parsing, and accurate not-found/unavailable/rate-limited results.
- Add explicit fetch actions to My accounts and each regular-account EHB control in signup/edit. Do not add the action to Alt accounts, Admin correction/internal participant creation, CSV/external rosters, catalogue pages, or ordinary page loads.
- My accounts saves only the numeric default. Reuse existing event-assignment `EhbSnapshot`, `EhbSource`, and `EhbFetchedAt` persistence for signup. A short-lived signed signup lookup result binds purpose, normalized character identity, exact decimal EHB, and fetch/issue/expiry times through the existing request contract and is verified inside `SignupService`; changing the value makes it Manual.
- Cache successful player lookups for five minutes in the process-local normalized-character cache so repeated explicit requests can reuse a safe recent result without contacting Wise Old Man. Cache reuse must still display the original fetch timestamp.
- Preserve all validation-failure form values and manual fallback. Fetch failure never clears the current EHB control.
- Add localized user-facing feedback and the smallest Admin-visible request-budget status needed to verify throttling.

Pass 10.1 is independently deployable and requires no schema migration. Its bounded normalized-name lookup cache is process-local and owned by the WoM lookup/client service; no general cache abstraction is added.

## Pass 10.2 — event competition link and cached synchronization

User approval clarification (2026-08-03): retain the existing Admin “Refresh cached activity” action. It performs the same single competition snapshot synchronization as the automatic worker on demand, while the existing production cooldown, shared limiter/final-three reserve, retry fencing, Live-only synchronization, and cached-only public projections remain authoritative. The Development-only “Make next refresh due (Development TEST 15)” action remains limited to that fixture and environment.

- Add one Admin event integration section on existing event management for an existing competition ID, validation status, last/next synchronization facts, request-budget facts, missing accounts, and a cooldown-respecting manual refresh. Recheck enabled Admin/Super Admin authority in the command service and audit link/change/clear; automatic refresh ticks are not audited.
- Validate the competition ID, public competition identity/title/window, and EHB delta availability without mutating Wise Old Man. Apply the approved exact schedule-prefill/sync or five-minute match rule.
- Allow create/change/clear before `Live`. During `Live`, allow only an audited correction to a schedule-matching competition; atomically invalidate displayed old cache and begin a new cycle. AwaitingFinalReview, Finalized, Archived, and Cancelled configuration is read-only. Bind every generation to the competition ID. Synchronization may resume only through an existing legitimate transition back to `Live`.
- Add the minimum persistence for one event integration/synchronization state and one cached per-event-character activity result. Include event/character identity, competition/generation identity, gained EHB, fetched-at time, optional upstream freshness facts, assignment-set fingerprint, latest completeness/error state, normal-cycle due time, retry due/count, and an opaque short synchronization lease owner/expiry. Do not mutate Bingo event versions or authoritative assignment rows during background refresh.
- Add a separate lightweight hosted synchronization worker/service. Acquire the fenced lease in a short transaction, commit, perform HTTP outside any database transaction, then finalize in a new transaction only if the event is still `Live` and competition ID, lease owner token, and assignment-set fingerprint still match. Otherwise discard the response and schedule the appropriate later attempt.
- Use one initial attempt plus three retries as specified above. A later complete or partial generation remains authoritative for display while retaining older rows internally.
- A successful response with missing expected accounts stores the diagnostic/missing-account state and matched rows atomically, publishes provisional partial rankings when possible, and never fabricates zero or carries forward an old account value.
- Automatic and manual synchronization share the exact same service, limiter, lease, cooldown, retry, and error semantics.

Pass 10.2 should be independently deployable after Pass 10.1. Linking a competition remains optional and never changes readiness.

## Pass 10.3 — cached participant and team activity projections

- Replace the existing Board/TeamBoard activity placeholders with locally cached Wise Old Man activity.
- Show participant combined gained EHB and a per-regular-account breakdown, team total, matched-participant average, deterministic tied MVPs, and explicit participant/account coverage for complete and partial generations.
- Show **Fetched from Wise Old Man** time—not “WoM updated”—and accurate not-configured, waiting-for-first-sync, partial, incomplete, zero-match, stale, and temporarily unavailable states. Admin diagnostics may additionally show upstream player/participation freshness facts where supplied.
- Public and team-private routes use only cached local data and existing public/team authorization. Admin-only diagnostics and missing-account names do not leak to public projections.
- Retain the latest authoritative generation state after synchronization stops, without further synchronization. If that frozen latest generation is partial, retain only its matched rows and explicit coverage; if it has zero matches, rankings stay hidden. Older rows remain internal. Resume synchronization only if the lifecycle legitimately returns to `Live`.
- Add no new notification type, ranking history/version system, or finalization snapshot unless the readiness review identifies a direct contradiction with existing immutable-history requirements.

Pass 10.3 should be independently deployable after Pass 10.2 and completes Slice 10.

## Manual-test reachability

Development reset should reuse the existing TEST 15 Live event and named website accounts, adding only deterministic fake-client configuration/data needed to demonstrate:

- My accounts explicit lookup and manual override;
- signup/edit explicit lookup without page-load requests;
- rate reserve feedback without contacting the external service;
- one linked TEST 15 competition with complete participant/team activity;
- one named TEST 15 website participant with a second current regular/`PLAYING` assignment for multi-account aggregation;
- a partial competition response that omits one expected account, keeps fresh matched values and provisional coverage/rankings public, and names only the missing account to Admins;
- two-hour cooldown and cached manual refresh;
- one temporary failure/retry/recovery sequence;
- stopped synchronization after event end and retained cached display.

Normal Development reset and automated tests must not contact the real Wise Old Man API. Exact accounts, routes, fake response controls, and steps belong in `MANUAL_TEST_CHECKLIST.md` before manual handoff.

## Complexity budget

- Persistence: at most one event-integration/synchronization-state table and one per-event-character activity-cache table, with one forward migration if required. Reuse existing event-character EHB provenance for manual lookup.
- Services: one typed WoM client/limiter, one account lookup service, one competition synchronization service, and one small hosted worker. Do not introduce a generic external-integration, scheduler, retry, workflow, or job framework.
- Routes/UI: extend My accounts, signup/edit, existing Admin event management, Board, and TeamBoard. No parallel dashboard, separate SPA, or UI overhaul.
- Jobs: one bounded due-sync worker only. It must schedule retries rather than sleeping and must not run external calls inside the critical event lifecycle worker.
- Notifications: none.

## Minimal proportional verification

- Fake-client account lookup scenarios covering success, cache reuse, manual modification/provenance, not found, unavailable, local reserve, `429`/`Retry-After`, and preservation of the current value.
- One PostgreSQL competition workflow covering schedule matching/synchronization, configuration lifecycle, one-request aggregation, current `PLAYING` inclusion, Alt/released exclusion, partial matched-row publication without zero/old carry-forward, zero-match no-ranking behavior, assignment fingerprint fencing, atomic cache replacement, two-hour normal-cycle anchor, manual/automatic shared behavior, lease fencing/concurrency/assignment change, one initial attempt plus three retries, and lifecycle stop/resume.
- One rendered authenticated/public projection workflow covering My accounts/signup controls, Admin missing-name diagnostics, participant multi-account aggregation, provisional partial totals/average/MVP/coverage, zero-match privacy, timestamps, and cached-only page behavior.
- One clean/retained migration rehearsal if Pass 10.2 adds persistence.
- Focused pass build/format/diff/EF checks; complete suite and consolidated manual acceptance only at the final Slice 10 gate.
- No ordinary test contacts the real Wise Old Man service.

## Explicit non-goals

- Calling `POST /players/{username}` or automatically updating/tracking WoM players.
- Creating, editing, starting, ending, or verifying Wise Old Man competitions.
- Storing a Wise Old Man verification code or API key in PostgreSQL.
- Wise Old Man group APIs or identity/ownership proof.
- Requests on render, keystroke, account selection, save, public page view, or per viewer.
- A separate request for every competition participant or team.
- Fetch controls for Alt accounts, Admin-created/external roster entries, CSV imports, or Admin participant correction.
- Minute-by-minute activity attribution across account swaps, withdrawal, or replacements.
- Treating missing accounts as zero, carrying forward old rows, fabricating zero-match rankings, or publishing partial rankings without explicit provisional coverage.
- Retroactively rewriting signup EHB snapshots.
- WoM-based readiness/finalization/lifecycle blockers or WoM notifications.
- Activity version history/finalization snapshots without a concrete approved requirement.
- Multiple-replica/distributed rate limiting, Redis, a generic job system, or speculative integration abstractions.
- OSRS Wiki/catalogue changes, UI-overhaul work, or the post-Slice-10 Application Atlas.
- Optional future Wise Old Man competition management remains deferred: competition create/edit/delete, participant/team mutation, and `update-all` are outside Slice 10 and are not implemented.

## Change control and review gate

This plan is the authoritative Slice 10 scope/non-goal baseline. Any material user-approved change during implementation or manual remediation must be recorded here and in affected source-of-truth documents before implementation. The post-implementation independent review must compare the exact base-to-current diff against the final plan and block missing approved behavior, unapproved material behavior, changed non-goals, or unbudgeted complexity.

Pass 10.1 implementation state (2026-08-03): complete in this worktree. The typed player-only WoM client, singleton limiter/cache, Admin request status, explicit My Accounts/signup fetch controls, signed signup lookup token, server-side provenance verification, manual fallback preservation, configuration, and focused fake/PostgreSQL/rendered route tests are implemented. Verification passed: fake transport/cache/limiter/token `5/5`, affected authenticated signup class `16/16`, rendered explicit-fetch route `1/1`, Release Web build with 0 warnings/errors, solution formatting verification, and `git diff --check`. No schema migration or EF model change was needed. The real WoM API was never called.

Pass 10.2 implementation state (2026-08-03): implemented in this worktree. Existing Admin event management now supports validated competition link/change/clear, creation-time exact-window prefill, service-bound Admin/Super Admin authority, audited configuration, request-budget facts, cached synchronization state, missing-account diagnostics, cooldown-respecting manual refresh, generation-bound per-character activity cache, fenced DB lease/finalization, lifecycle stop/resume eligibility, retries with a separate normal-cycle anchor, and one due worker. The typed shared WoM client requests competition details once with `metric=ehb`; it never mutates WoM. Migration `20260803085202_AddWiseOldManCompetitionSynchronization` adds only the approved event synchronization and per-event-character cache tables; refresh does not mutate event versions or assignment rows; no Pass 10.3 projection or notifications were added.

Verified: typed competition-client fake transport `1/1`; PostgreSQL fake-client workflow `3/3`; retained migration rehearsal `Slice1MigrationRehearsalTests` `6/6`; combined focused Slice 10.1/10.2 filter `9/9`; Release Web build with 0 warnings/errors; solution formatting verification; EF pending-model check reported no pending model changes; and `git diff --check`. The clean migration path was exercised by each focused PostgreSQL workflow. No real WoM request was made. The complete solution suite, manual acceptance, and independent review remain unrun; Pass 10.3 has not started. Nothing is staged, committed, merged, or pushed.

Pass 10.3 implementation state (2026-08-03): implemented only the cached participant/team activity projection. Board and TeamBoard consume a small local query boundary over the newest synchronization generation and per-character cache; they never depend on the Wise Old Man client. Current non-released `PLAYING` assignments aggregate by participant, including multiple regular accounts; team averages divide by participants with at least one matched account, and tied provisional MVPs are deterministically ordered. Complete, partial, stale, temporarily unavailable, waiting, zero-match, and not-configured states are explicit; a newer partial generation replaces older displayed rows while retaining older rows internally. The Development reset clears the two existing Slice 10 tables, seeds due local TEST 15 activity, and adds only the minimal TEST 16 signup-lookup fixture and linked lookup character required for manual reachability. The existing TEST 15 Manage surface also has one Development-only, fixture-scoped control that makes the next manual refresh due without changing production cooldown/retry scheduling. No new tables, production jobs, notifications, lifecycle behavior, finalization snapshots, route families, or UI-overhaul work were added.

Verified: `Slice10Pass103ActivityProjectionTests` `3/3` and the affected Slice 10 integration filter `12/12`, including PostgreSQL calculations/state gating and rendered public Board/TeamBoard cached-only rendering with zero competition-client calls; focused Release Web build with 0 warnings/errors; solution formatting; EF pending-model check; and `git diff --check`. The real WoM API was never called. The complete solution suite, manual acceptance, and independent review remain unrun. Nothing is staged, committed, merged, or pushed.

Final restricted remediation verification (2026-08-03): the new initial-Live scheduling and Development due-control/reset tests passed `2/2`; official client/fixture coverage passed `7/7`; performed-refresh success/failure feedback passed `1/1`; reset/public cached-only reachability passed `1/1`; the retained Development reset regression passed `1/1`; and the complete Slice 10.1–10.3 filter passed `18/18`, with zero failures/skips. Release solution build passed with 0 warnings/errors, formatting verification passed, and `git diff --check` passed. No EF check was needed because no model changed. The complete solution suite, manual acceptance, and independent review remain unrun; nothing is staged, committed, merged, or pushed.

Approved partial-generation correction verification (2026-08-03): the corrected Development make-due/real-refresh test passed `1/1`; complete Slice 10.1–10.3 coverage passed `18/18`, including matched-row publication, no zero/old carry-forward, deterministic provisional totals/ranks/coverage, Admin/public privacy, zero-match no-ranking, and unchanged assignment-fingerprint fencing. Release solution build passed with 0 warnings/errors, formatting verification passed, and `git diff --check` passed. No schema/model change, EF check, full suite, real WoM call, manual acceptance, independent review, or packaging was performed.

Consolidated manual acceptance (2026-08-03): S10-01 through S10-06 passed. S10-01 had no visible feedback after the successful action at step 8; cached/team Activity EHB is functionally correct, but its current presentation is visually broken and deferred to the UI overhaul. S10-02 passed with the Development fake’s arbitrary non-missing Success names understood as deterministic test behavior; production remains authoritative to real WoM. S10-03 passed after the Live competition-synchronization capability correction, S10-04 passed, and S10-05 passed with screenshots confirming stale cached activity, timestamp, totals/ranks/coverage after event end; its visual table layout is deferred to the UI overhaul. S10-06 passed and the Development fake prevented real WoM calls. The user explicitly approved the Admin manual “Refresh cached activity” feature. Manual Slice 10 acceptance is complete; the visual notes are deferred presentation work, not functional blockers.

Final acceptance and packaging handoff (2026-08-03): the complete final automated gate passed Domain `162/162`, Application `83/83`, Browser `67/67`, and Integration `263/263`, combined `575/575`, with zero failures/skips. Release build passed with 0 warnings/errors; formatting, EF pending-model, `git diff --check`, migration/reset, artifact/secret, and staged-state gates passed. Durable TRX evidence is retained in `/private/tmp/slice10-final-domain-remediation-q0X7Mm/domain-remediation.trx` and `/private/tmp/slice10-final-automated-gates-pHOcQX/`. Final restricted review and consolidated manual acceptance are complete; the accepted delta is ready to package from base `85a17bae`.
