# AU06 implementation handoff — 2 October 2026

Role: implementer/remediator, Astra/high. Recipient: orchestrator `/root` only.
Exact checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Branch: `codex/participants-functionality`; packaged HEAD:
`0ec8add9314a65ab66751bf44bbb4f5195ff854f`. All work is uncommitted.

## Result and scope

Existing signup service now owns both event custom-question and secondary Account
field additions. The Application request carries a nonempty request ID, event,
authenticated actor, immutable submitted form baseline and requested definition.
One concrete `signup_question_creation_operations` table records globally unique
request ID, actor/event, SHA-256 of canonical intended values including the original
baseline, and created question ID. Definition, operation, audit and form version
commit atomically under the existing Serializable event lock. No new service,
route, policy, job or generic receipt framework is introduced.

Exact same-input retries return the created ID without another field/audit/version
change. Persisted success replay occurs before new-write stale/lifecycle checks,
but after current enabled Admin and event-visibility checks. Changed input/baseline
or cross-actor/event key reuse returns conflict without revealing the prior field.
Separate requests from another Admin may create the same label, but cannot substitute
for the original request. A renamed field still reconciles by its immutable ID;
inactive/deleted field replay returns Removed and never recreates it. Hidden and
discarded event outcomes are unavailable, with no created ID disclosed.

Canonicalization trims label/help and normalizes choice lines; fingerprints retain
requested Required, not post-first-response effective optional. Existing optional
normalization remains, explicit feedback is deferred to AU07. Additional Account
fields remain optional and distinct from primary/captain system fields; invalid
roles/custom Account input fail closed. No global account/character records are
created. Key allocation retains existing slug-derived ordering/collision rules and
bounds the stem to fit the existing persistence column.

Three bounded fresh transactions handle serialization/deadlock or operation-key
contention. Exhaustion retains the same request ID/submitted baseline with an explicit
retryable outcome. Unexpected/commit-response errors propagate as uncertain; they
never invent success or a replacement request. There are no clock values in the
fingerprint, so no timestamp-roundtrip fingerprint boundary needs separate testing.
All new fixture instants are deterministic UTC and microsecond aligned.

Web handlers bind a hidden request ID and return the typed result for JSON callers;
ordinary form navigation/status messages are retained. Each account/custom form has
a distinct generated ID. Submitted custom ID/input remains on ordinary validation
rerender. Only exact Questions default/AddAccount POSTs delegate lifecycle decisions
to the signup service after the existing hidden/discarded route checks. Other
question mutations retain their lifecycle route gate. This narrow dependency was
reported to and accepted by the orchestrator during implementation.

The operation's restricted actor/event FKs retain history. Its scalar question ID
has no deletion-cascading FK; event discard can remove setup definitions while the
operation stays with the event tombstone. The actual discard path is tested. Real
Development reset explicitly truncates the new table and is exercised with a
committed field-add operation present. Complete generated migration, designer and
snapshot are included; migration runs against real disposable PostgreSQL 17.

## Stable artifacts and preservation

- `au06.patch`: complete AU06-only before-to-current patch, including untracked files.
  SHA-256 `c8a6b1d6f8711c3656fc17b324ce5ccb541b0ea9ff7a98698f5e176e4bc76c98`.
- `source.sha256`: all 25 owned final source identities.
  SHA-256 `54af691e959320a0b2d38a45d8c875ec86d47e452d5d94aea25809fd7bd50665`.
- `before/`: before snapshots, including inherited uncommitted source files.
- `before.sha256.json`: 1,049 baseline identities; `before-status.txt` captures the
  starting working tree. `changed-paths.json` lists the 25 owned files.
- `protected-source-check.json`: 1,031 inherited/non-owned baseline files unchanged.
  Existing changes inside shared owned files are preserved in the before-to-current
  patch. No inherited source was staged, reverted, overwritten or committed.
- `diff-check.log`: scoped `git diff --check` and AU06 added-line whitespace PASS.
- `leak-check.log`: added-line private-key/provider-token/credential-URL scan PASS.
- `test-results.json`: exact test names and outcomes extracted from both TRX files.
- `package.py`: artifact creation and identity/whitespace/leak verification command.

Scope mapping: six existing authority/status documents; existing Signup interface
and new request/result contract; one Domain operation and persistence configuration;
DbContext plus migration/designer/snapshot; SignupService partial declaration and
focused creation implementation; Questions Razor/handler; exact lifecycle route
filter; reset list and discard retention comment; one new focused integration class
and minimal existing helper/reset integration changes. No UI matrix approval change.

AU05 planner reconciliation was carried into CURRENT_STATUS: eleven non-metadata
production/test/product/functional/data identities verified, only three expected
completion-metadata differences, no code changes, prior orchestrator idle. AU05 tests
and review were not repeated. Two existing transport helpers now supply the newly
required identity; the existing custom-add validation route and directly affected
Development reset regression were run because AU06 changes their boundary.

## Commands and evidence

Every repository command used the exact assigned checkout above as explicit workdir.
Write/build/test escalations were narrowly scoped to that checkout or disposable
synthetic databases. No approval rejection/environment blocker occurred.

1. `dotnet ef migrations add AddSignupQuestionCreationOperations --project src/Bingo.Infrastructure --startup-project src/Bingo.Web`
   Initial generation failed compilation (`migration-generation.log`). A bounded
   diagnostic `dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore` established
   CS1061: Account has `LoginName`, not `Username` (`initial-build.log`). Corrected
   that new audit projection; generation then succeeded
   (`migration-generation-corrected.log`). Generated migration namespace converted
   to the repository's file-scoped convention before test compilation.

2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SignupQuestionCreationRetryIntegrationTests|FullyQualifiedName~AdminQuestionsRouteAddsOneCustomQuestionAndPreservesInvalidInput|FullyQualifiedName~DevelopmentResetClearsCreationOperationsAndRetainsSeededCreationJourney' --logger 'trx;LogFileName=au06-focused.trx' --results-directory /private/tmp/au06-implementation-20261002`
   First compile identified fixture CS0117, GlobalRole.Player should be GlobalRole.User
   (`focused-initial.log`). Corrected that typo; actual execution then passed 10/12
   (`focused-corrected.log`, `au06-focused.trx`). The two HTTP cases failed before
   reaching the route after login: Testing secure cookies require HTTPS, while the
   new fixture used the default HTTP BaseAddress. Production behavior was not the
   cause of those failures.

3. Corrected new HTTP BaseAddress to HTTPS; included the required narrow lifecycle
   filter integration and additional later-state replay/new-write/Move assertions.
   `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SignupQuestionCreationRetryIntegrationTests.ActualHttpFormsBindIdentityBaselineAndEnforceAuthenticationAntiforgery' --logger 'trx;LogFileName=au06-http.trx' --results-directory /private/tmp/au06-implementation-20261002`
   PASS 2/2 (`http-corrected.log`, `au06-http.trx`). No unchanged failing command rerun.

4. Restored the existing account-add success wording in the shared response helper;
   no JSON/service behavior changed. Final
   `dotnet build Bingo.slnx --configuration Release --no-restore`
   PASS, zero warnings/errors (`build-final.log`). No production edits after it.

5. `python3 /private/tmp/au06-implementation-20261002/package.py`
   PASS: stable complete patch/manifest, 1,031 inherited identities unchanged,
   scoped whitespace and leak checks. No full suite/browser walkthrough.

Twelve DISTINCT selected cases have passing outcomes across the corrected runs;
this is not a final single 12-case passing run. The ten new cases cover:
- custom and Account duplicate/concurrent adds with two observed PostgreSQL lock
  waiters; exactly one receipt/definition/audit/form increment; exact shared ID;
- a thrown response after an actual PostgreSQL commit, recovered in a new context;
- same-label success by another Admin, later definition rename, original ID replay,
  changed label/type/help/Required/baseline, cross-actor and cross-event conflicts;
- canonical choice normalization and requested-required versus effective-optional
  replay after the first response, with no definition/backfill rewrite;
- current demoted/disabled role, hidden SuperAdmin result, draft lock, and fresh
  write versus replay distinctions;
- actual deletion and discard with retained operation/no resurrection/FK failure;
- database audit-constraint failure rolling back operation/definition/version,
  followed by successful use of the same original request;
- authenticated HTTP (custom/Account), rendered IDs, antiforgery, invalid identities,
  immutable baseline, stale-new rejection, later Archived replay/new-write rejection
  and unchanged other mutation route protection;
- invalid types/roles/required-account shapes, pre-response required question support,
  protected system fields and absence of global character/account creation.
The remaining two passing cases are existing custom-add invalid-input/rendering and
real Development reset integration.

## Boundary and next owner

Verified: implementation, direct checks above, final Release build, stable AU06-only
artifacts and inherited-source protection. No known required finding or environment
blocker. Unverified: fresh independent review; orchestrator owns dispatch next.
Frontend draft/uncertainty recovery, AU07 normalization feedback, manual UI acceptance
and integration/release gates remain deferred. No self-review claim or page approval.
No user database/provider operation, running-app change, full suite, browser
walkthrough, stage/commit/push/merge/deploy. The same implementer is available for
named remediation; routine report goes only to `/root`, not the originating planner.
