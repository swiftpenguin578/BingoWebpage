# AU05 implementation handoff — 2 October 2026

Role: implementer/remediator, /root/au05_implementer, Astra/high. Direct implementation
and focused verification only; no independent review or UI/manual acceptance claimed.
Source is stable. Return only to orchestrator /root in chat
01a0fce4-d253-72a2-a97d-c8b727e04813. Next action: fresh independent review.

## Exact checkout and identity

Checkout: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch: codex/participants-functionality
Packaged HEAD: 0ec8add9314a65ab66751bf44bbb4f5195ff854f
All changes remain uncommitted. No staging/commit/push/merge/deployment.
The attached Documents checkout is not the implementation authority.

Complete AU05-only patch: au05.patch
SHA-256: 7df6134957635f2f1f8e76997e1657ef6d5ce8f515e154b9f06c6d3ab588a183
Live 14-source manifest: source.sha256
SHA-256: 387426bb3709ae5d8de5d52132381c9ba59e8dad501706e2908bcaa2f3c26a45
Captured before identity manifest: before.json
SHA-256: 9d16eb6ffda411253f231d0b3e883f0af58f206ddb600b3df3cc41c49a129201

before/ contains exact inherited sources, including untracked AU01–AU04 files;
absent/deleted identities are retained in before.json. owned-paths.json and
changed-paths.json list the exact 14 paths. This is not a whole dirty-HEAD diff.
initial-status.txt, initial-head.txt, final-status.txt, checkout-identity.txt retain
checkout evidence. All 34 inherited non-owned paths retain their captured identities
(protected-source-check.json). Owned shared documents and Participants handler
retain inherited changes in their before snapshots and AU05-only patch.

## Delivered

- Questions.cshtml.cs requires the submitted expectedFormVersion for custom add,
  account-field add, custom edit, account rename and move. Missing/malformed tokens
  fail closed; stale tokens are checked against SignupForm.Version inside the
  existing Serializable event-lock transaction. All existing question forms forward
  their rendered token with hidden inputs. No layout or new UI state binding.
- EnableCoCaptainAsync now requires the same baseline (missing optional parameter
  fails closed) and returns current/saved form version. Existing delete/disable
  question-version and impact-count guards remain unchanged; no rebuilt counts.
- Local Questions handler filter handles only nested DbUpdateConcurrencyException
  and PostgreSQL serialization failure 40001. Handler transaction disposal rolls
  back before the filter handles the exception, clears tracking and redirects to
  the normal fresh page. Unrelated exceptions remain unhandled. Serializable
  isolation is preserved throughout production changes.
- SignupAdministrationResult retains SubmittedEventVersion separately from an
  authoritative SignupSettingsSnapshot: event version, capacity, waiting-list
  enabled, code-required and code-present only. No code text/hash is returned.
  Capacity service returns committed values; failures after rollback read fresh
  authorized state after core transaction disposal. Accepted unchanged-capacity
  saves explicitly advance event version, as do changed values.
- Existing Manage.Capacity and Participants.SignupAdministration alias expose the
  result for Accept: application/json; native redirects remain. Participants
  SignupCode exposes the same result shape, handles wrapped 40001 and reads fresh
  state after rollback/disposal. Capacity and code remain separate transactions.
  Returned baselines are immutable records scoped to that operation. Actual pending
  uncertain client state binding remains deferred; no retry identity guarantees.
- FirstResponseAt shape locks and required-to-optional normalization retained.
  Existing delete/disable assignment release, saved-account preservation and
  capacity promotion/atomic rollback verified by focused regression cases.
- Existing authorities promoted before production edits. Current status, AU05
  delivery/reference rows now say implementation/checks complete, review pending.
  Existing regression HTTP helpers forward rendered versions. No new tables,
  services, routes, policies, jobs, migrations or concurrency framework.

## Exact checks and outcomes

All relevant commands below used the exact assigned checkout as workdir and narrow
approved escalation for the out-of-default-writable-roots worktree. Tests use only
disposable Testcontainers PostgreSQL 17, synthetic accounts/code and deterministic
UTC timestamps aligned to microseconds. Baselines are integer versions; no timestamp
fingerprint/precision behavior was changed. No user database/provider was accessed.

1. Initial `dotnet build Bingo.slnx --configuration Release --no-restore` mistakenly
   omitted workdir and ran in attached Documents checkout. It passed, but is NOT
   AU05 evidence. build-initial.log retains resolved paths. No production edits
   were made there; later commands explicitly name the assigned workdir.
2. Assigned checkout: `dotnet build Bingo.slnx --configuration Release --no-restore`
   PASS, zero warnings/errors, build-assigned.log. Actual resolved worktree DLL and
   project paths appear in the log.
3. Initial focused test command below failed compilation only on new fixture
   analyzers (numeric ToString requires invariant culture; Assert.Single predicate
   syntax). Corrected test code. focused.log retained; no TRX for this attempt.
4. Same test filter after those corrections:

   dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SignupSetupVersionIntegrationTests|FullyQualifiedName~SignupCodeValidationIntegrationTests|FullyQualifiedName~ConfirmedQuestionMutationRechecksImpact|FullyQualifiedName~CoCaptainDisable|FullyQualifiedName~DeleteQuestionRemovesAnswers|FullyQualifiedName~SignupAdministrationOwnsCapacity|FullyQualifiedName~SignupAdministrationRollsBack' --logger 'trx;LogFileName=au05-focused-compiled.trx' --results-directory /private/tmp/au05-implementation-20261002

   17 PASS / 4 FAIL / 0 skipped, 21 total. focused-compiled.log and corresponding
   TRX. Two failures exposed malformed token validation responses preceding stale
   reporting (fixed early token validation). Two contention fixtures timed out on
   their query-text observation predicate; changed observation to actual blocked
   PostgreSQL sessions via pg_blocking_pids, not sleeps/timing assumptions.
5. Corrected baseline/contention plus four affected existing question HTTP cases:

   dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SignupSetupVersionIntegrationTests|FullyQualifiedName~QuestionsEditAfterFirstResponse|FullyQualifiedName~QuestionsEditInactive|FullyQualifiedName~AdminQuestionsRouteAdds|FullyQualifiedName~QuestionsReorder' --logger 'trx;LogFileName=au05-corrected.trx' --results-directory /private/tmp/au05-implementation-20261002

   13 PASS / 1 FAIL / 0 skipped, 14 total. corrected.log and TRX. Remaining actual
   concurrent-add loser returned 500: local filter missed provider-wrapped 40001.
   Changed only local conflict detection to traverse InnerException for supported
   conflicts. The same wrapped provider case is handled in code settings wrapper.
6. Directly affected conflict cases:

   dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ConcurrentHttpAddsSerialize|FullyQualifiedName~ConcurrentSettingsReturn' --logger 'trx;LogFileName=au05-conflict-corrected.trx' --results-directory /private/tmp/au05-implementation-20261002

   PASS 2/2, conflict-corrected.log/TRX. Both HTTP requests are verified blocked by
   PostgreSQL before releasing the controlled event lock. Adds produce exactly one
   question/audit/form increment and a normal loser redirect. Settings produce one
   committed winner, one failure, matching authoritative snapshots and one audit.
7. Final unchanged-save version correction and direct protected capacity cases:

   dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SeparateSettingsPostsReturn|FullyQualifiedName~ConcurrentSettingsReturn|FullyQualifiedName~SignupAdministrationOwnsCapacity|FullyQualifiedName~SignupAdministrationRollsBack' --logger 'trx;LogFileName=au05-settings-final.trx' --results-directory /private/tmp/au05-implementation-20261002

   PASS 4/4, settings-final.log/TRX. Existing settings scenario now asserts unchanged
   capacity save returns newer version and exactly matching other saved values.
   Promotion and audit-failure rollback pass after the explicit version advance.
8. Final `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS, zero
   warnings/errors; build-final.log. No source edits after this build except none.
9. Scoped `git diff --check -- <owned paths>` and AU05-added-line whitespace scan
   PASS (diff-check.log). Added-line private-key/provider-token/credential-URL scan
   PASS (leak-check.log). 34 non-owned before identities unchanged.

Combined direct evidence: 25 DISTINCT selected cases have passing outcomes across
corrected runs, not a new full 25-case run. No routine full suite/browser walkthrough.
The new baseline theory covers all six operations with stale, missing, malformed
and accepted current tokens through real login/antiforgery/route/handler boundaries.
It compares serialized event/form/questions/answers/participants/assignments/audits/
notifications before and after rejected operations. Settings HTTP cases assert
submitted versus saved/current versions, no secret leakage, separate transactions,
zero stale side effects, no-op versions, and controlled concurrent outcomes.

## Approval rejection and limits

Initial production edit command was rejected BEFORE execution for proposing lower
transaction isolation. Exact reason retained in approval-rejection.txt. The rejected
script was never executed or reconstructed. Safer approved implementation preserves
all existing Serializable transactions and uses explicit rollback/conflict handling.
No remaining task action requires the rejected isolation change.

Verified: implementation, direct focused checks above, stable patch/identity, retained
inherited files. No outstanding implementation defect or environment blocker known.
Unverified/deferred: independent review, page binding (including per-card uncertainty),
manual UI acceptance, AU06 add retry identities, AU07 explicit normalization outcome.
No UI approval, next ticket, provider action, user DB changes, app restart, staging,
commit, push, merge or deployment. Ready for the orchestrator's fresh independent
reviewer; same implementer remains available for named remediation/rechecks.
