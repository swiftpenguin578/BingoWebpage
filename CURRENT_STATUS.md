# Current project status

## Active assignment — B4 / Catalogue AU21 → AU23 → CAT-1, 4 October 2026

- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`; initial B4 baseline was `42e12cf2f0bb6ad5f1eddd28e8ef30aa3f965577`, and the remediation started from the separately verified clean `90df58a2f16842706c0df4f16be3c9bd47c3c693`.
- Planner `/root`, replacement chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`; sole implementer `/root/au_b4_implementer`, `gpt-5.6-luna` / max. No extra workers or visible chats.
- B3/AU20 is accepted as the prerequisite per the supplied B4 brief. Its R1 conversion, R3 rehearsal and operator migration-count gates remain future obligations; no production or user-owned database access was used here.
- B4 remediation item 1 AU21 is committed in `c4c52a4`; item 2 CAT-1 binding is committed in `c2d2220`; item 3 D8 snapshot/deployment handling is committed in `c2b00b2`; item 4 documentation/register corrections and remediation evidence are this checkpoint. The earlier AU23 backend remains `00dfd40`. Evidence is under `docs/references/admin-ui/reviews/2026-10-04/au-b4/`.
- Worker-reported focused checks: AU21 PostgreSQL 7/7; AU23 existing PostgreSQL 2/2 plus Cat01 mechanics 2/2 and retired metadata refusal 1/1; CAT-1 ordinary-Admin handler 2/2 plus real HTTP binding 1/1; CAT-1 migration rehearsal 1/1; D8 snapshot round-trip/fail-closed zero-write 1/1; full Slice6 Catalogue class 167/167; Catalogue price HTTP class 28/28; Release build 0 warnings/errors; deploy script syntax and scoped diff checks passed. The broader `FullyQualifiedName~Snapshot` filter had 47 passes and one retained-fixture `placement_rule` failure; the separate retained-catalogue migration class has five predecessor-schema fixture failures (`team_size`/`placement_rule` missing before its later migrations), all before product assertions. These are worker results pending external review.
- The CAT-1 migration fails closed for any non-default per-drop context and the runbook now requires read-only production counts for `conditional_on_parent` and non-default `assumed_participants`/`probability_scope`. The user's 4 October evidence, recorded in [08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions), reported **0** for each query; this worker did not run those production queries. The decision and remediation provenance are also recorded in the [authorized B4 brief](/Users/christopher/Documents/BingoWebpage/review-notes/27-codex-brief-b4-catalogue.md) and [review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-review-decisions).
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
