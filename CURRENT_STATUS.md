# Current project status

## Active assignment — B4 / Catalogue AU21 → AU23 → CAT-1, 4 October 2026

- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`; clean B4 baseline was `42e12cf2f0bb6ad5f1eddd28e8ef30aa3f965577`.
- Planner `/root`, replacement chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`; sole implementer `/root/au_b4_implementer`, `gpt-5.6-luna` / max. No extra workers or visible chats.
- B3/AU20 is accepted as the prerequisite per the supplied B4 brief. Its R1 conversion, R3 rehearsal and operator migration-count gates remain future obligations; no production or user-owned database access was used here.
- B4 item 1 AU21 is implemented in `01c5dc2`; item 2 AU23 is implemented in `00dfd40`; item 3 CAT-1 is implemented in `fd3ce10`; item 4 documentation/register updates are this checkpoint. Evidence is under `docs/references/admin-ui/reviews/2026-10-04/au-b4/`.
- Worker-reported focused checks: AU21 PostgreSQL 2/2; AU23 PostgreSQL 2/2; Cat01 mechanics 2/2; retired metadata refusal 1/1; CAT-1 handler 2/2; CAT-1 migration rehearsal 1/1; Release builds completed with 0 warnings/errors; scoped diff checks passed. These are implementer results pending external review.
- The CAT-1 migration fails closed for any non-default per-drop context and the runbook now requires read-only production counts for `conditional_on_parent` and non-default `assumed_participants`/`probability_scope`; the user's 4 October zero counts remain user evidence, not agent verification.
- Current-page UI and WA-5/RC10 binding are deferred. No new current-page display, B5 work, rehearsal execution, main merge, push or deployment is authorized.

## Retained B1/B2 evidence and limits

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. Prior checks are reused, not rerun or independently reviewed here.
- B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Execution evidence remains workers'.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/` and `au-b2/remediation/`; merge evidence `au-b1/remediation/merge-back.md`.
- Approval attribution: `docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md`, supplied `assignment.md` and `decisions.md` beside it.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Operator pre/post migration counts remain due. No production access assigned.

## Prior outcomes / corrected approvals

- F1–F9/R2 and G1–G6 passed Claude source review; historical evidence retained.
- H cleanup accepted by Claude source recheck (`cleanup-recheck.md` beside the
  Step 0 assignment), subject to attribution corrections now included here.
- H4-2 procedure/limits approved by user after Claude review on 4 October, as
  directed by the quoted Step 0 assignment and supplied planner decisions.
  Earlier at-writing attribution is corrected. Tooling unbuilt/untested, R3 unrun.
- H3-4 `6b8331d`: not user-approved, per the current assignment’s attribution
  correction. Accurate code-description correction stands; Git history unchanged.
- R1 conversion failure remains blocking; R3 is required on final candidate.
- AU23 precheck and CAT-1 migration gate are approved in the B4 assignment; production counts remain unrun by this worker.
- AU17 Contribution/readback only, AU18 WOM outcome/version history only, AU19
  approved. No additional Review context/current-event readiness UI is authorized.

## Next permitted action

Stop after the four B4 commits for external Claude review of the stable B4 range. No independent review or manual UI acceptance is claimed. Preserve the B3/AU20 R1/R3 and operator migration-count gates; do not start B5, WA-5/RC10 binding, rehearsal execution, main merge, push or deployment.
