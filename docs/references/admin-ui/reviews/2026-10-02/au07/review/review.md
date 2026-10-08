# AU07 independent review — PASS

Reviewer: `/root/au07_reviewer`, independent read-only Astra/high role, 2 October 2026. Recipient: orchestrator `/root` only. No required findings or remediation.

## Exact reviewed state

Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Branch: `codex/participants-functionality`; verified packaged HEAD: `0ec8add9314a65ab66751bf44bbb4f5195ff854f`.
Baseline: captured AU07 `before/`, including inherited uncommitted/untracked AU01–AU06/planner/AU15 changes. Reviewed the complete AU07-only before/current patch, not a broad HEAD comparison.

Evidence root: `/private/tmp/au07-implementation-20261002/`.
- `au07.patch` SHA-256: `cba06fb4096196d67b307253c353c1a7544b244e4510df3c337c649ef63e6e4d`.
- `source.sha256` SHA-256: `93c76d180af2d6044bd01f11a6c86179a85257a5c27372a02daaf3214c4285ba`.
- Independently verified all 14 live files and all 14 current snapshots match that manifest.
- Applied the patch with whitespace errors prohibited to a temporary copy of `before/`; all 14 reconstructed source hashes match the manifest.
- Independently compared all 1,044 protected inherited identities with the live checkout: no changes, including inherited missing/deleted paths.

## Approved scope comparison

Baseline is final DELIVERY_PLAN AU07 plus the planner-approved marker-only clarification and the exact authorized stale TECHNICAL_ARCHITECTURE reconciliation. Read active AGENTS, CURRENT_STATUS, DELIVERY_PLAN 4.2.1, focused product/functional/data/architecture signup rules and UI feedback rules.

Delivered required scope:
- Post-first-response required custom adds persist as optional and return successful `CompletedAsOptional`, a normalization flag, explanation and immutable original committed definition. Pre-response required creation retains ordinary `Completed` and required persistence.
- Replay first validates current Admin authority, visibility and actor/event/canonical-original-input fingerprint. It resolves the exact operation's field-linked creation audit, requires a unique usable snapshot and never substitutes current edited definitions or same-label records. It preserves the original definition and normalization; deleted fields retain Removed status without recreation. Failure results do not expose a prior definition.
- The response-only null-to-first-accepted marker transition preserves editable form version only when no other form property or tracked question mutation accompanies it. Explicit Version marks/advances and mixed settings/definition changes invalidate baselines. No stale-baseline acceptance was added. The existing Serializable event locks and actual-state required/type guards remain in place; RecordAcceptedResponse remains first-write-only.
- Existing JSON result and ordinary form feedback convey normalization, with informational severity and Danish localization. No new frontend recovery workflow was introduced.
- Existing audit/operation/question/version atomicity, Account/system guards, authorization and lifecycle behavior remain protected.
- Active authorities record the approved clarification; the identified architecture paragraph now agrees with the pre-draft editing contract.

Missing approved scope: none within AU07's technical boundary.
Unmapped material implementation: none.
Changed explicit non-goals: none. Frontend uncertainty binding, redesign and manual acceptance remain deferred.
Unbudgeted complexity: none. No new table, migration, service, route/page, policy, job or generalized framework. The result DTO, audit readback and bounded version predicate directly support approved behavior.

## Evidence assessed

Reused the implementer's executable evidence instead of rerunning passing checks. Independently parsed both TRX files: initial focused run 20 passed/1 failed; subsequent contention run 2 passed/0 failed, yielding 22 distinct passing cases. This is not a single final 22-case run. The initial failure was the simultaneous-signup fixture expecting a returned failure while existing Npgsql serialization contention surfaced as a wrapped SQLSTATE 40001. The corrected fixture requires that exact underlying failure and retries in a fresh context; signup production behavior was not changed.

Reviewed the discriminating test paths: real PostgreSQL 17 migrations and transactions; authenticated HTTP form/JSON/redirect normalization across a first accepted signup with observed lock contention; absence of participant backfill; pre-response editor rejection of post-response required/type changes; marker-only and mixed/explicit version behavior; concurrent definition change rejecting the stale add without extra operation/audit/version; original replay and lost commit response after edits and same-label creation; exact non-microsecond audit timestamp roundtrip; preserved Account/system/authority/lifecycle and atomic audit-failure guards; corrupt/missing snapshot fail-closed behavior. The earliest accepted marker survives simultaneous signup contention and fresh-context retry.

The supplied final exact-checkout Release solution build log records success with 0 warnings and 0 errors. Scoped checks record no added-line secret-pattern matches; the reviewer independently verified patch reconstruction/whitespace and inherited source preservation. No newly identified uncertainty justified more executable tests.

## Limits and next owner

This is a technical independent review pass, not UI/manual approval or a release-gate pass. Full frontend uncertainty binding, manual visual acceptance and later integration/release gates remain deferred. Existing wrapped 40001 signup retry behavior remains unchanged. No full suite, browser walkthrough, provider call, user database action, application restart, repository edit, stage, commit, push, merge or deployment was performed by this reviewer. Only the temporary review report and temporary reconstruction were written.

Next owner: orchestrator `/root` records this pass and completes its authorized planner handoff. No routine remediation remains.

## Reviewed source identities
91ce5fe1f07012711fc6eb6991b901b67942abbaa9fe2dc37c6ca218b25d0fb4  CURRENT_STATUS.md
be9332e8fa3b9cf7ee49a87badc6da856056f778527f1d4de67c3232bdae4dd8  DATA_MODEL.md
b20dc6930d5d0c8e4727947215413c9c7b7a1733f41a8cada30060d828ebc073  DELIVERY_PLAN.md
29632dab13e6a59590c741dfe780ae17f74897793ab9ee5d41fc75a69ffca604  FUNCTIONAL_CONTRACTS.md
b7926f62661f93004d5881985bebe06fb900086e32df26a30908f3c2b17b8b5c  PRODUCT_REQUIREMENTS.md
46a8156ca687fb68bbe0e7f34c807948bd00dcb933665d386b29e4cfa9ac26b6  TECHNICAL_ARCHITECTURE.md
10fdb0725146b25783b269de01044034b14e69b33640ecded2a44c7e96358946  docs/references/admin-ui/FUNCTIONALITY_CHANGES.md
22633270813d76ca157f8f51940bf9bf5d97ddeaf58e581d4eafc5d607127f61  src/Bingo.Application/Signups/SignupQuestionCreation.cs
8a1981d88bcd08ccca32e4f5409393e9dd15d64cf2198de415889894d5242d88  src/Bingo.Infrastructure/Persistence/ApplicationDbContext.cs
d8880329f0dc83ab4f787232d897ea9fc4f304f7031410a994da78a895516a9a  src/Bingo.Infrastructure/Signups/SignupService.QuestionCreation.cs
7132e433788ded3e84a61c57fe3254f41de36df5deba30c80eeedee433c4f0c8  src/Bingo.Web/Pages/Admin/Events/Questions.cshtml.cs
dab390b5dd44c6408555e66b071580167ad0681137b3015daf27132f91f38d54  src/Bingo.Web/Resources/SharedResource.da.resx
39527f486e6c1065e09b7f9a71cfee93ce6a83de39b264b52a7a6f886ae1ad85  tests/Bingo.IntegrationTests/SignupQuestionCreationRetryIntegrationTests.Normalization.cs
5c3affc400085d142ca5d50171813e3e1edb79f65813834f0b2ed5848f7fc5a1  tests/Bingo.IntegrationTests/SignupQuestionCreationRetryIntegrationTests.cs
