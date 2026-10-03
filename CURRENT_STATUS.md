# Current project status

## Active handoff — 4 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- Direct implementer: `/root/cleanup_astra_remediator`, `gpt-6-astra` / high,
  explicit user override for this named remediation round only.
- No orchestrator/additional workers/chats; Claude owns independent named recheck.
- Starting candidate `f6b5bd9`; last remediation-source checkpoint `6b8331d`.
- Durable assignment/decisions: `docs/references/admin-ui/reviews/2026-10-04/cleanup-remediation/review.md`
  and `decisions.md`. Detailed 15a/15b originals remain in the private review-notes folder.
- Exact finding → file:line/check/commit map:
  `docs/references/admin-ui/reviews/2026-10-04/cleanup-remediation/remediation-handoff.md`.

## Committed remediation and verification state

- Activation `a75bbe8`: separate documentation commit explicitly approved by user
  after initial automatic approval rejection. No rejected action was bypassed.
- H1 `b8156b3`: named evidence pointers/provenance/logs/collision files/demo values;
  recovered files byte-equal to backup; raw Dashboard 9/9 TRX omission explicit.
- H2 `ed19466`: stale baseline/status/evidence pointers corrected.
- H3 `f2bdb5d`: ranking, user-attributed tile visual acceptance, cohort, draft
  fields, capacity observation and pending C33 states. Exact Start/Scramble wording
  corrected by **`6b8331d`**, an additional H3-only commit explicitly approved by
  the user after automatic rejection. No amendments.
- H5 `c3a3474`: actual AU20 WA-2 rules and pending owning docs; DB-2/DB-3 integration
  defects distinct from DB-4/DB-5; EI-3 no-DB uncertain rendering; problem-only X-6;
  AU17a separate approved full-pool ticket; LKP-1/proof/RC routing; decided Catalogue
  AU23/CAT-1/WA-5 scopes. Tickets/docs only, no AU/RC implementation.
- H6 `610c842`: only Danish N-1 resource string. XML/key/caller checks passed.
- H4 `fe395f2`: corrected publication query, all-row correction count, G4 backfill/
  Down gate, banner verification only; separate **unapproved H4-2 proposal**.
- Scoped diff checks passed. H4 SQL matched source predicates/schema and gate/
  runbook text; no SQL, migration, Down/Up or rehearsal executed.
- Required stale-phrase checks passed; older out-of-scope temporary evidence
  citations remain and are counted honestly in the handoff.
- Production-file scope is only SharedResource.da.resx. No other src/tests edits,
  AGENTS/CLAUDE/frozen-reference changes, new tests or build reruns in this round.
- All remediation awaits Claude's independent review. No new independent PASS or
  functional/manual acceptance is claimed. UI_PAGE_MATRIX owns visual status.

## Pending approvals and retained evidence

- **H4-2 remains open.** Proposal:
  `docs/references/admin-ui/reviews/2026-10-04/cleanup-remediation/r3-isolation-proposal.md`.
  Reported to `/root` before runbook safety edits. It proposes an offline isolated
  VM/harness with local HTTPS S3/WOM fixtures and enabled worker health checks;
  production-wrapper/provider/public-TLS coverage is explicitly limited.
- Unsafe host `bingo-deploy` rehearsal instruction is withdrawn. The proposal
  has not been adopted as approved procedure. Harness implementation, backup
  transfer, R3 execution and production deployment are not authorized here.
- The user separately approved final reconciliation of this file and the handoff
  above in one additional documentation-only commit after automatic rejection.
  This approval does not approve H4-2, R3 or Claude's independent recheck outcome.
- Prior F1–F9/R-2 and G1–G6 Claude source-review results remain retained; accepted
  G checkpoint `576c661`. Original H candidate `f6b5bd9` failed the named cleanup
  review; these follow-up commits await recheck, not an inferred PASS.
- Original H evidence/handoff remains at
  `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1-h7-final-handoff.md`.
  Historical status versions remain in Git; no evidence was deleted or rewritten.
- R-1 conversion failure blocks release; R-3 remains unexecuted and blocking.
  D8 remains proposed; Claude plans subsequent batching only after this review passes.

## Next permitted action / stop boundary

Planner obtains the user's H4-2 procedure/coverage decision; only then may the
approved procedure be adopted under its stated scope. Claude independently
rechecks the named remediation commits; record H4-2 pending separately. No next
AU/RC/UI ticket, additional audit, provider call, user-owned DB mutation, R3 run,
push, merge or deploy. Send the exact checkpoint/blockers/next action to `/root`
before the final response, then stop. No wait_threads or routine planner polling.
