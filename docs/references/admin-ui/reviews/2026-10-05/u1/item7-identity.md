# U1 item7 — Identity binding (5 October 2026)

Implemented by `/root/u1_implementer`, Astra/high, from clean
`3e8c64675ac00fbe1ebada71d45cf59f37031442` on the assigned worktree/branch.
Authority: brief43 item7, inventory42b Page3 I-1–I-16, A10/A14/I-1, brief48
and its exact readback-property-count addendum. Planner decisions remain labelled
planner decisions within the earlier user scope; no new product approval is claimed.

## Implemented behavior

- Identity alone opts into the new shell. Reference form-card sections replace the
  old rail and Cancel navigation; permanent absolute Signups link remains copyable.
  Frozen reference files and copied tokens/components remain byte-identical.
- Success and no-change PRG stay on Identity, with toast and quiet Saved, no extra
  client history entry. All writes/readbacks use AdminFetch; Manage refusal is not
  success, and lost-session input remains visible before sign-in.
- AU08 comparison/transactions/audit stay intact. Untouched fields take reviewed
  current values; conflicts keep the draft, display theirs and submit explicit
  KeepMine unless Use theirs supplies UseCurrent. Newer conflicts still re-block.
- Readback JSON has exactly eventId, values and persisted version. The frozen
  request tuple retains native multipart newline semantics and exact observed
  values. A14: matching values+moved version => Up to date without attribution;
  moved version+differing values => conflict flow; unchanged version => not applied,
  draft kept and Save available. Failed reads stay uncertain; no automatic retry.
  Uncertainty locks fields and changes Save to Check again. Departure offers the
  default Check again or Leave anyway warning that changes may already be saved.
- EI-3 captures shell context before attempting the write, catches rollback failure
  and renders the posted draft from the pre-write snapshot. The injected real-PG
  commit-loss test proves zero DB reads after the loss, one persisted mutation/audit,
  and an uncertain HTML response despite a failed rollback.
- Timezone confirmation presents all five scheduled rows, old/new offsets across
  winter/summer, and unscheduled values. It names other changed fields. The existing
  13-moment microsecond fingerprint remains unchanged. Stale basis closes the layer;
  the next Save opens a fresh review. Before public exposure, direct save has a note.
  Copenhagen/UTC and retained unsupported current values use the existing supported
  timezone rules; changing away disables re-selection. Ever-Live timezone lock stays.
- Existing changed-name-only 50-code-point rule stays; client counters use code
  points for Name and UTF-16 for Description/Buy-in. Inline errors, multi-field
  summary and first-invalid focus retain input. Terminal values are full-colour
  read-only with a one-sentence reason. D17 Identity GET/Current now succeeds;
  D16 mutation refusal and hidden/discarded 404 protections stay.
- ES module init/dispose owns listeners, request cancellation and layer cleanup.
  Disposal awaits layer closure; shell draft guard supports the uncertain departure
  callback. Repeated event switches and Back/Forward show the selected event and
  disposed forms cannot submit. No legacy adapter is retained for old fixtures.
- New visible literals have Danish entries. Existing owner docs and the matrix
  now describe the bound behavior and pending independent/visual acceptance.

## Executed checks

Machine-readable results: `item7-checks.json`.
- Focused Integration: **47 passed / 0 failed / 0 skipped**. This includes full
  IdentityFieldConflictIntegrationTests, Slice3CreationIdentityPersistenceIntegrationTests,
  AdminDesignShellIntegrationTests (new item7 cases and prior shell/session cases),
  and unchanged TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect.
- Focused Browser .NET: **33 passed / 0 failed / 0 skipped** (EventCreationUiTests,
  ManagedCompetitionUiTests, AdminDesignLocalizationTests and matching policy tests).
- Full JS runner: **41 files passed / 0 failed**, including the rewritten timezone
  and readback tests and new identity-binding test, including final ordinary-form
  Back and server-validation focus assertions. Final-commit full runner execution
  is reported to dispatcher without a separate evidence-only commit.
- Solution Release build: **0 warnings / 0 errors**. `git diff --check` and frozen
  reference CSS comparisons clean. No CI run is claimed.
- Initial new-test analyzer errors (array allocation in a repeated assertion,
  Regex.Matches.Count) were corrected without changing expectations. Initial new
  fixture failures were missing production defer/reviewed-values attributes and
  a predicate reading through the temporary navigation skeleton. No timeout was
  increased, existing safety assertion weakened, or environment retry performed.

Commands (all from the assigned worktree):

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AdminDesignShellIntegrationTests|FullyQualifiedName~IdentityFieldConflictIntegrationTests|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests|FullyQualifiedName~TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect' --results-directory /private/tmp/bingo-u1-item7-trx --logger trx
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --no-build --filter 'FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~ManagedCompetitionUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~AdminEventPagePolicy' --results-directory /private/tmp/bingo-u1-item7-ui-final-trx --logger trx
dotnet build Bingo.slnx --configuration Release --no-restore
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/run-browser-tests.cjs
```

## Mechanical disposition

No Identity RedirectToPage(Manage), client Manage-success recognition, display
ManageModel dependency, First public preview row, old rail, old Cancel control or
legacy page-script adapter remains. The generic uncertain catch contains no DB
read; the separate ordinary concurrency-conflict path still deliberately reads
current state. Identity's explicit terminal-view policy is true and mutation gate
is unchanged. The view's only public URL helper is /Events/Signups. Overview still
has Signup form, Public signup table and conditional Public board at its existing
lines218–222; Overview was not changed.

## Acceptance and next action

Implemented and focused-executed, **not independently reviewed or visually accepted**.
The user/Claude runs the unfiltered whole .NET suite on the reported final SHA:

```sh
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"
```

That gate remains pending and requires zero failed/zero skipped per project.
Then Claude independent review and user light/dark, Danish/English, phone-width
acceptance (including confirmations, errors and toasts). CI itself is unrun.
Optional carried note: setup-node@v4 is still a mutable tag; no unrelated workflow
pinning was added. No migrations, live WOM/user database, extra worker, self-review,
U2+, rehearsal, push, merge or deployment.
