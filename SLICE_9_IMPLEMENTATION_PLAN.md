# Slice 9 Implementation Plan

## Approved outcome

Slice 9 completes authoritative event ending and recovery, live participant withdrawal/replacement, final review, official result history, archive access, and operational notifications. It extends the existing lifecycle, participant, team, evidence, finalization, and notification boundaries rather than creating a parallel workflow system.

The user approved this product behavior on 2026-08-02. Implementation must not begin until the single independent read-only implementation-readiness review is complete and its accepted corrections are recorded here.

## Approved product decisions

- An event that entered `AWAITING_FINAL_REVIEW` prematurely through either manual or automatic end may resume `LIVE` only before official finalization.
- Resume requires an enabled Admin, strong confirmation, a written reason, and a replacement future event-end time.
- The prior end transition, effective time, actor/reason when applicable, evidence, reviews, contributions, swaps, focus, and other intervening history remain immutable.
- The ordinary submission cutoff is re-derived from the replacement end. A separately reopened submission window is revalidated and is not silently carried into resumed play.
- Eligibility resumes prospectively at the successful transition and is never backdated across the final-review interval.
- Resume rechecks singleton-current, lifecycle, stale-update, and retry/idempotency protections. It is unavailable directly from `FINALIZED` or `ARCHIVED`.
- Emergency credentials are not silently re-enabled. An Admin may explicitly enable a valid credential again through the existing audited action.
- Live withdrawal preserves competitive history and creates a real vacancy. Replacement is optional, Admin-selected, prospective, and grants no inherited credit.
- Captain/co-captain assignment remains a separate explicit Admin action.
- Normal finalization is confirmed without a reason; exceptional blocker overrides and unfinalization require reasons. Archive is confirmed without rewriting official history.

## Core invariants

- Scheduled end uses the configured instant as the authoritative effective end even when processing runs late; early end uses the confirmed actual instant and preserves the schedule.
- No newly obtained drop is eligible after the authoritative effective end or during a final-review interval later followed by resume.
- Submission access and drop eligibility remain separate. The active upload cutoff never changes silently.
- A participant withdrawal immediately revokes website mutation authority while the approved next-whole-UTC-minute boundary governs final competitive eligibility.
- Withdrawal never deletes draft picks, memberships, assignments, evidence, contributions, roles, or audit history.
- A replacement has one prospective membership/activation boundary, intact event-character reservations, and no predecessor contribution.
- Finalization uses the authoritative reviewed state, creates immutable versioned result snapshots, and cannot coexist with unresolved required blockers.
- Unfinalization supersedes rather than deletes official snapshots and never silently reopens uploads.
- Archive preserves public URLs and historical participant access while removing operational mutation authority.
- Operational notifications and action items are idempotent and resolve from authoritative underlying state, not merely read/unread status.

## Pass 9.1 — authoritative end and premature-end recovery

- Reconcile scheduled and manual early end through the shared lifecycle service and worker.
- Preserve scheduled/effective/actual end distinctions and the existing 30-minute ordinary submission cutoff behavior.
- Add the confirmed, reasoned `AWAITING_FINAL_REVIEW → LIVE` recovery action with a replacement future end.
- Preserve prior lifecycle and intervening competitive history; restore only prospective eligibility.
- Re-derive the ordinary cutoff, recheck singleton-current/lifecycle/concurrency rules, and prevent duplicate transitions, audits, or notifications.
- Keep Finalized/Archived recovery outside this action and retain explicit emergency-credential enablement.
- Add the smallest Development scenario and manual journey needed to exercise scheduled end, early end, resume, stale/conflicting resume, and the second authoritative end.

Pass 9.1 should be independently deployable without changing replacement or finalization behavior.

## Pass 9.2 — live withdrawal and replacement

- Complete Admin live withdrawal through the existing participant/team lifecycle boundary.
- Revoke mutation authority on commit, preserve all history, record the approved eligibility end, end active membership/role, and create a durable vacancy.
- Notify enabled Admins and remaining linked Captain/Co-captains once with private-safe context.
- Allow an Admin to leave the vacancy open, select an available waiting-list participant, or create a validated internal participant when necessary.
- Revalidate capacity, waiting status, frozen event assignments, uniqueness, team vacancy, and concurrency inside one transaction.
- Promote/create the replacement prospectively with `Replacement` membership linkage and active primary-character history; inherit no evidence, contribution, role, or draft pick.
- Notify the linked replacement and current linked team leadership once. Keep Discord availability contact manual.
- Keep Captain/co-captain assignment separate and preserve public/private projection boundaries.

Pass 9.2 should be independently deployable after Pass 9.1 and must not rewrite the draft ledger.

## Pass 9.3 — final review, official history, archive, and operational handoff

- Reconcile the final-review checklist with authoritative pending evidence, completion checks, operational blockers, and direct resolution destinations.
- Preserve reasoned exceptional blocker resolution without changing underlying records.
- Keep normal finalization an explicit confirmed Admin transaction that locks/rechecks state, recalculates progress/rankings, snapshots official placements/statistics, and records actor/time atomically.
- Preserve versioned official history through reasoned unfinalization and re-finalization; never delete or silently reopen uploads.
- Keep confirmed `FINALIZED → ARCHIVED` history read-only on existing public routes and participant-owned historical projections.
- Recheck singleton-current boundaries when unfinalizing Finalized/Archived results.
- Reconcile personal notifications and the Admin action inbox so read state never resolves authoritative operational work.
- Finalize Development fixtures, source-of-truth wording, and one compact consolidated Slice 9 manual checklist.

Pass 9.3 is independently deployable after Passes 9.1 and 9.2 and completes Slice 9.

## Manual-test reachability

Development reset must provide the minimum accounts and event states for: scheduled/early end, premature-end recovery, a vacancy with waiting-list replacement, optional internal replacement, missing-Captain warning and explicit reassignment, pending-evidence finalization blocker, reasoned override, official snapshot, unfinalization/re-finalization, and archive history. Reuse existing accounts/events where practical; do not add broad demonstration fixtures.

## Complexity budget

- Persistence: reuse event transition/audit/finalization, membership, activation, vacancy/action, and notification data. Add persistence only if the readiness review proves existing history cannot represent resume or replacement without ambiguity.
- Services: extend existing lifecycle, participant/team, finalization, and notification services. No generalized workflow, replacement, lifecycle, or notification framework.
- Routes/UI: reuse Manage, Participant/roster management, Finalize, public history, and notification surfaces. No parallel lifecycle console or UI redesign.
- Jobs: extend the existing lifecycle worker only where scheduled end/cutoff handling requires it. No new background-job family.

## Minimal proportional verification

- Focused lifecycle scenarios for scheduled/late/early end, resume, second end, singleton conflict, stale request, retry, and no residue.
- Focused PostgreSQL withdrawal/replacement scenarios for authority revocation, eligibility boundaries, waiting/internal replacement, uniqueness/conflict rollback, history, and idempotent recipients.
- Focused finalization/unfinalization/archive scenarios for blockers, override history, snapshot versions, public/private projections, and concurrency.
- One worker catch-up scenario covering authoritative scheduled instants and duplicate suppression.
- One authenticated route/markup scenario for each materially changed Admin journey and its direct resolution destination.
- Clean/retained migration rehearsal only if persistence changes.
- Focused per-pass build/format/diff/EF checks, then the complete suite and consolidated manual acceptance only at the final Slice 9 gate.

## Explicit non-goals

- Direct resume from `FINALIZED` or `ARCHIVED`.
- Erasing or rewriting prior end/finalization history.
- Retroactive eligibility across a final-review interval.
- Automatic replacement selection or automatic Captain assignment.
- Inherited replacement evidence, contribution, membership history, or draft pick.
- Discord automation.
- New workflow/action/notification frameworks.
- UI-overhaul work, the post-Slice-10 Application Atlas, or Slice 10 Wise Old Man integration.

## Change control and readiness

This plan is the authoritative Slice 9 review baseline. Any material user-approved change during implementation or manual remediation must be recorded here before implementation. The post-implementation independent review compares the exact base-to-current diff with the final updated plan and blocks missing approved scope, changed non-goals, unapproved material behavior, or unbudgeted complexity.

Current readiness: product behavior is approved; independent read-only implementation-readiness review is pending. No Slice 9 production implementation has started.
