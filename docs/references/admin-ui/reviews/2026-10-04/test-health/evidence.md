# Test-health batch evidence

Implemented test-health batch, brief 32; baseline `0179b9b3f4d4dff00a401f642e6a80374b5dc807` verified clean on `codex/participants-functionality`. Sole implementer `/root/au_b5_implementer`, Astra/high; no independent review claimed. B5 remediation remains stopped.

Authority: [brief 32](/Users/christopher/Documents/BingoWebpage/review-notes/32-codex-brief-test-health.md), [08-decisions.md B5 review decisions D14 and approved test-change rule, lines 176–177](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-review-decisions-user-4-october). Whole-suite final-commit gate must have zero failures/skips; no failure is waived.

## Reproduction

At the clean baseline, a named-test filter selecting all 16 tests and F ran across `Bingo.slnx --no-build --no-restore -c Release`, logger trx, results `/tmp/th-reproduce`. Integration: 16 failed / 1 passed / 0 skipped (includes the failing flaky test and passing withdrawal theory case); Browser: 1 failed / 0 passed / 0 skipped. Domain/Application have no tests matching this diagnostic filter; final gate will run all projects unfiltered. All sixteen listed failures and F reproduced. Exact command is retained below.

## Per-test disposition

| # | Test | Verified cause/classification | Origin commit | Governing decision | Old → new / correction | Correction commit |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | C11FinalizedRosterIntegrationTests.StaleRolePostAfterWithdrawalOrActualStartCannotPublishOrChangeRoles(true) | Outdated post-start refusal; role command decides current lifecycle under lock and republishes role-only snapshot | `02d19dbf699d5d4781e68d09f0fd1ccfd031d8ea` | 08-decisions.md:21–25, TD-7 option 1; FUNCTIONAL_CONTRACTS.md:641 | Whole-state unchanged after start → exact expected role/version, transition, immutable old roster, replacement cycle/roster, two audits and one notification; every other original table/field remains exactly equal. Withdrawal theory case retains original whole-state hash equality. | `3f519429a2f645d1db93592088c24e6814841b22` |
| 2 | Slice10Pass102CompetitionSynchronizationTests.StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded | Cause 3: early EndNow changed configured WOM end, so unmatched-end protection correctly skipped the injected provider failure | `2d74bdec1d3dfc3dd11695f8948f6dad53161e9e` changes end; `66256173eadbdceb713d54cfbaeca7545d9d4a00` enforces publication fallback | 08-decisions.md:31–40, Step 3 WA-2 item 5; DATA_MODEL.md:340–355 | End at the fixture's configured provider end before EndNow; every feedback/audit assertion unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 3 | Slice4ParticipantLifecycleIntegrationTests.PreformedRosterMembersStayOutOfSignupCountsAndPromotionWithoutExcludingAdminSignups | Outdated capacity expectations: SignupParticipants now counts all event participants; confirmed count includes the manual member | `56020b861c795e7abaafe4ffb44580e8b4a0d8ac` removes the manual-team exclusion | 08-decisions.md:26, TD-2 option B (manual members count toward capacity); existing list-display binding is deferred separately at line 113 | Cap 2→3 for zero promotions; cap 3→4 for one promotion; exact confirmed projection 3→4. All promotion, participant-status, audit and current-page row assertions retained. Renamed to describe decided capacity behavior. | `3f519429a2f645d1db93592088c24e6814841b22` |
| 4 | BannerRetirementMigrationTests.PopulatedDatabaseKeepsExactKeysUntilFailedCleanupRecoversAndPreservesSharedObjects | Cause 3: current EF Event insert wrote later placement_rule into pre-retirement schema | `0f79ede26ae4cb497b667e167fb5a559fece136e` | DATA_MODEL.md:363 BNR-01; brief 32 §4 predecessor-schema proof | Explicit predecessor Account/Event SQL, realistic populated asset/shared-reference data retained; all assertions and migrations unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 5 | BannerRetirementMigrationTests.ManuallyCleanedSingleAssetRetiresSchemaWithoutPendingLedger | Cause 3: same later-column insert | `0f79ede26ae4cb497b667e167fb5a559fece136e` | DATA_MODEL.md:363 BNR-01; brief 32 §4 | Same predecessor-only setup helper; all assertions and migrations unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 6 | Slice5MigrationRejectionTests.RetainedPublicationBackfillUsesTheIdentityValidAtPublication | Cause 3: current EF draft-session insert wrote later requires_fresh_order into foundation schema | `1b5a139d6709ce499c87181363bd81b44ce1bdc7` | Brief 32 §4 explicitly requires realistic retained-publication predecessor-schema proof | Explicit foundation team/session/participant/membership/character/assignment SQL; deterministic UTC fixture time; publication-time identity assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 7 | Slice5MigrationRejectionTests.RetainedPublicationBackfillFailsClosedWhenIdentityIsMissing | Cause 3: same later-column insert prevented reaching intentional missing-identity failure | `1b5a139d6709ce499c87181363bd81b44ce1bdc7` | Brief 32 §4 | Same foundation helper with identity intentionally absent; fail-closed assertions and migrations unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 8 | AdminStaleChangeIntegrationTests.SharedItemAndDropRollBackWhenAuditPersistenceFails | Cause 3: missing shared-item activity confirmation stopped before intended audit failure | `c4c52a4b65ae041739e5a5f31c2055ca495c7676` | 08-decisions.md:154, B4 review D7 option a | Supply exact affected activity ID to existing command; all transaction/rollback assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 9 | AdminStaleChangeIntegrationTests.SharedItemEditRejectsStaleHttpFormAndAuditsCurrentDropAndItemTogether | Cause 3: missing confirmation prevented the competing edit needed by stale-form scenario | `c4c52a4b65ae041739e5a5f31c2055ca495c7676` | 08-decisions.md:154, B4 review D7 option a | Supply exact sharedItemConfirmationActivityIds on competing edit; all stale-form/audit assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 10 | EventCompetitionManagementIntegrationTests.ReceiptSaveRetryReloadsManagementAndCompletesTheOperation | Cause 3: fixture remote already matched desired state; legitimate reconciliation bypassed provider write and its receipt-failure hook | `19865e33bbf0860494fc671b16f8efd6326d5fea` | DATA_MODEL.md:792–828 managed applied/source fingerprints and durable reconciliation; FUNCTIONAL_CONTRACTS.md:1100–1109 | Seed a persisted acknowledged prior remote title/fingerprint, with current desired payload still pending; assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 11 | EventCompetitionManagementIntegrationTests.ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition | Cause 3: same already-matched remote meant write barrier was never reached; not slow execution | `19865e33bbf0860494fc671b16f8efd6326d5fea` | Same managed-operation contract | Same realistic previous applied state; no timeout increase/retry/sleep; all locking assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 12 | EventCompetitionManagementIntegrationTests.StaleRetryRequeuesCurrentLiveDatesAndRecordsCurrentFingerprint | Cause 3: same already-matched remote caused reconciliation instead of the intended provider update | `19865e33bbf0860494fc671b16f8efd6326d5fea` | Same managed-operation contract | Same previous applied state; current-live fingerprint and payload assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 13 | Slice1IdentityIntegrationTests.AccountSupportDisableReasonSurvivesRestoreAuditAndManageProjection | Cause 3: manually constructed Razor page lacked framework-provided TempData | `b44fa6e00e0c980a3d6b03377e88fe4fc7636ce5` | 08-decisions.md:128, B1/B2 D5 approved credential TempData | Initialize existing DictionaryTempDataProvider on this page fixture; all disable/restore/audit/projection assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 14 | Slice3DestructiveLifecycleIntegrationTests.TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect | Cause 2: terminal Questions route exempted all add attempts rather than only verified committed replays | `acf8ba91aee17b509a41341a71ad072b454bce0f` | **User decision 08-decisions.md:178, B5 review D16 option a**, resolves the previously stopped case | Test14 unchanged; terminal route now accepts only exact service-verified committed replay and otherwise gives exact Manage/read-only response. Six added HTTP/PostgreSQL cases cover both add types in all three terminal states, including Cancelled exact replay and mismatch/no-write checks | `f87e51865d62527dd1cc75155ef07cedd0a53252` |
| 15 | Slice3CreationIdentityPersistenceIntegrationTests.PublicSchedulePreviewPreservesLockedSignupOpeningWhenPostOmitsIt | Cause 3: claim-only actor absent from database failed enabled-Admin service authorization | `525d5d155eca3ea4956437f91599a1233a961087` | FUNCTIONAL_CONTRACTS.md:418, enabled Admin schedule authority | Persist controlled enabled Admin matching request actor; all preview/locked-opening assertions unchanged | `6a1f3af0479fd809f298282fe289fc06dcaa51a3` |
| 16 | Bingo.BrowserTests.EventCreationUiTests.CreationAndIdentityMarkupKeepTheMinimalCreationAndNativeRouteBoundaries | Cause 1: source owner and Identity comparison contract changed | `acf8ba91aee17b509a41341a71ad072b454bce0f` | FUNCTIONAL_CONTRACTS.md:340–357 and :375–389 | Exact old/new assertion mapping below; every original claim retained in actual owner, plus handler delegation/comparison guards | `3f519429a2f645d1db93592088c24e6814841b22` |
| F | Slice7Pass71IntegrationTests.CompletedTileFocusIsRejectedAndClearedThroughApprovalAndReversalRebalance | Flaky setup: equal SubmittedAt values left intended approval order to random Guid tie-break under BR-1 | `24c30fc66af9cb56087f0d60959d858e5420c788` | 08-decisions.md:41–46 BR-1 and refined scope; AGENTS timestamp precision rule | First submission exactly one microsecond earlier; exact PostgreSQL round-trip checks for both times added; all allocation/focus/reversal assertions unchanged | `ae48c69d9cd42d039ceb48ce8b9921c6f32c1bf9` |

Test 3 full class: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Slice4ParticipantLifecycleIntegrationTests' --logger 'trx;LogFileName=th-capacity.trx' --results-directory /tmp/th-capacity -v minimal`: **23/23 PASS**, 0 skipped, 53s, controlled PostgreSQL. Completed-code whole-suite PASS is recorded below; final-commit verification is reported in the terminal handoff.


Test 16 (outdated source-location/Identity-contract assertions), origin `acf8ba91aee17b509a41341a71ad072b454bce0f`: recorded decisions FUNCTIONAL_CONTRACTS.md:340–357 (atomic minimal creation/AU03 request identity), :375–389 (AU08 field comparison, baseline-free stale refusal, timezone confirmation freshness). Verified actual Create delegates the entire request to EventCreationService, which retains the same transaction/default-board/questions/audit/slug behavior; Identity applies the approved canonical compared tuple and schedule fingerprint. Assertions retained precisely:

| Old assertion | Replacement |
| --- | --- |
| ReadCommitted transaction / ConfigureSignup / exact 5×5 Board constructor / DefaultQuestions / IX_events_slug / event.created searched in Create handler | Same six exact strings searched in the actual EventCreationService owner; additional exact handler delegation of RequestId, Name, Timezone |
| Create redirects using local item.Id | Exact redirect uses result.EventId, the committed service outcome |
| Identity stale check Input.Version != snapshot.Version | Exact baseline-free check `!Input.HasBaseline && Input.Version != item.Version`, plus exact comparison call and all four original/intended/current fields |
| Timezone requiresPreview local-variable expression | Exact equivalent condition on authoritative item.FirstPublicAt, plus exact schedule fingerprint comparison |
| UpdateIdentity with old individual locals | Exact UpdateIdentity call with all four compared proposed fields and unchanged permanent slug |

No assertion deleted or skipped; every old claim remains checked against its current approved owner/contract. Test 16 full class: **28/28 PASS**, 0 skipped. Correction commit: `3f519429a2f645d1db93592088c24e6814841b22`.

Test 1 first class attempt failed at compilation on xUnit2031 (7 new Assert.Single/Where analyzer errors). Replaced with the semantically identical predicate overload; no assertion/field removed. Rerun: **59/59 PASS**, 0 skipped (2m45s).

## Execution checkpoint

All introducing commits above were traced through relevant `git log`/`git show`/`git blame` and the affected implementation; this is source provenance, not a claim that each commit was independently bisected. The clean-baseline diagnostic run reproduces every listed failure.

- Full C11 class: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~C11FinalizedRosterIntegrationTests' --logger 'trx;LogFileName=th-roles.trx' --results-directory /tmp/th-roles -v minimal` — 59/59 PASS, 0 skipped.
- Full creation Browser class: `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~EventCreationUiTests' --logger 'trx;LogFileName=th-creation.trx' --results-directory /tmp/th-creation -v minimal` — 28/28 PASS, 0 skipped.
- Eight setup cases (2, 8–13, 15): focused run `/tmp/th-setup-focused/th-setup-focused.trx` — 8/8 PASS, 0 skipped; full classes **361/361 PASS**, 0 skipped (8m41s).
- Migration cases 4–7: Banner full class **3/3 PASS** and Slice5 migration full class **5/5 PASS**, 0 skipped.
- F: full class **16/16 PASS in each of five consecutive runs**, zero skipped; durations 51s, 47s, 50s, 45s, 42s.
- D16 resolves #14; both affected classes pass. Completed-code whole suite passes below; no independent review claimed.

Full setup/migration class counts: AdminStaleChange 15; BannerRetirementMigration 3; EventCompetitionManagement 83; Slice10Pass102CompetitionSynchronization 168; Slice1Identity 71; Slice3CreationIdentityPersistence 16; Slice5MigrationRejection 5. All passed, zero skipped.

Exact commands for the baseline and setup runs:

```sh
dotnet test Bingo.slnx --no-build --no-restore -c Release --filter 'FullyQualifiedName~StaleRolePostAfterWithdrawalOrActualStartCannotPublishOrChangeRoles|FullyQualifiedName~StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded|FullyQualifiedName~PreformedRosterMembersStayOutOfSignupCountsAndPromotionWithoutExcludingAdminSignups|FullyQualifiedName~PopulatedDatabaseKeepsExactKeysUntilFailedCleanupRecoversAndPreservesSharedObjects|FullyQualifiedName~ManuallyCleanedSingleAssetRetiresSchemaWithoutPendingLedger|FullyQualifiedName~RetainedPublicationBackfill|FullyQualifiedName~SharedItemAndDropRollBackWhenAuditPersistenceFails|FullyQualifiedName~SharedItemEditRejectsStaleHttpFormAndAuditsCurrentDropAndItemTogether|FullyQualifiedName~ReceiptSaveRetryReloadsManagementAndCompletesTheOperation|FullyQualifiedName~ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition|FullyQualifiedName~StaleRetryRequeuesCurrentLiveDatesAndRecordsCurrentFingerprint|FullyQualifiedName~AccountSupportDisableReasonSurvivesRestoreAuditAndManageProjection|FullyQualifiedName~TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect|FullyQualifiedName~PublicSchedulePreviewPreservesLockedSignupOpeningWhenPostOmitsIt|FullyQualifiedName~CreationAndIdentityMarkupKeepTheMinimalCreationAndNativeRouteBoundaries|FullyQualifiedName~CompletedTileFocusIsRejectedAndClearedThroughApprovalAndReversalRebalance' --logger trx --results-directory /tmp/th-reproduce -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded|FullyQualifiedName~SharedItemAndDropRollBackWhenAuditPersistenceFails|FullyQualifiedName~SharedItemEditRejectsStaleHttpFormAndAuditsCurrentDropAndItemTogether|FullyQualifiedName~ReceiptSaveRetryReloadsManagementAndCompletesTheOperation|FullyQualifiedName~ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition|FullyQualifiedName~StaleRetryRequeuesCurrentLiveDatesAndRecordsCurrentFingerprint|FullyQualifiedName~AccountSupportDisableReasonSurvivesRestoreAuditAndManageProjection|FullyQualifiedName~PublicSchedulePreviewPreservesLockedSignupOpeningWhenPostOmitsIt' --logger 'trx;LogFileName=th-setup-focused.trx' --results-directory /tmp/th-setup-focused -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests|FullyQualifiedName~AdminStaleChangeIntegrationTests|FullyQualifiedName~EventCompetitionManagementIntegrationTests|FullyQualifiedName~Slice1IdentityIntegrationTests|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests|FullyQualifiedName~BannerRetirementMigrationTests|FullyQualifiedName~Slice5MigrationRejectionTests' --logger 'trx;LogFileName=th-setup-classes.trx' --results-directory /tmp/th-setup-classes -v minimal
```

Flaky-class commands ran sequentially with immediate stop on any nonzero exit; no failure retry was used. For each literal `N` = 1, 2, 3, 4, 5:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Slice7Pass71IntegrationTests' --logger 'trx;LogFileName=th-flaky-N.trx' --results-directory /tmp/th-flaky -v minimal
```

All five TRX files report 16 passed, 0 failed, 0 skipped. Across the corrected full classes there are 459 distinct Integration cases and 28 Browser cases passing; F's extra four runs repeat its 16 cases. This is focused implementation evidence, **not the final whole-suite gate**.

## Checkpoint history

The worker stopped #14 unchanged under brief32 §3 and preserved the passing independent corrections until the user recorded D16. No workaround or guessed decision was used. After D16, the code correction passed and groups 2–4 were committed in the required order. No migration, B5 remediation, UI binding, live provider, user database or deployment changes occurred.

The earlier working-checkpoint clean Release build passed (0 warnings/errors); the completed-code clean Release build also passed (0 warnings/errors, 26.67s). Whole-suite execution passed unfiltered on completed code `ae48c69d9cd42d039ceb48ce8b9921c6f32c1bf9` before this evidence/docs commit. The required verification of this final documentation commit uses the same unfiltered gate; its exact HEAD/results are in the terminal delivery report.

## D16 resolution and direct consequence

The human recorded D16 option a in [08-decisions.md, B5 review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-review-decisions-user-4-october). This resolves #14 as code wrong; it is not planner-inferred approval. Test14 remains unchanged. The filter relies on the existing locked service's replay identity/actor/event/intent verification, including immutable original definition checks; it adds no persistence or migration. Exact replay of an originally committed field that was later removed retains its existing AU06 result.

D16 also changes the directly affected existing `ActualHttpFormsBindIdentityBaselineAndEnforceAuthenticationAntiforgery` Archived-new-add assertion (both existing theory cases retained): old **exact JSON Outcome Locked** → new **exact HTTP 302 + exact `/Admin/Events/Manage/{id}` + exact extracted toast text `This event is read-only in its current lifecycle state.`**. All original replay, field-count, version and full persisted-snapshot assertions remain. The code's terminal routing and the full HTTP class check this recorded outcome; no assertion is loosened or removed.

First two-class run: **40 PASS / 3 FAIL / 0 skipped, 43 total** (`/tmp/th-terminal/th-terminal.trx`). The three new custom-add terminal cases exposed Razor selecting the default handler for an unknown handler name, allowing an unintended replay response. The code now also requires the submitted handler name to match the selected add handler. Test assertions remain unchanged; corrected full-class rerun **43/43 PASS**, 0 skipped (1m16s): destructive lifecycle 15; Questions retry 28. Exact command:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Slice3DestructiveLifecycleIntegrationTests|FullyQualifiedName~SignupQuestionCreationRetryIntegrationTests' --logger 'trx;LogFileName=th-terminal.trx' --results-directory /tmp/th-terminal -v minimal
```

The corrected run uses the same command with `LogFileName=th-terminal-fixed.trx`.

## Completed-code whole-suite gate — PASS

Code HEAD `ae48c69d9cd42d039ceb48ce8b9921c6f32c1bf9`; only the assigned documentation was uncommitted. Exact command:

```sh
dotnet clean Bingo.slnx -c Release -v minimal && dotnet build Bingo.slnx --no-restore -c Release -v minimal && dotnet test Bingo.slnx -c Release --logger trx --results-directory /tmp/th-whole-candidate -v minimal
```

Build **PASS: 0 warnings, 0 errors**. All four project TRX counters show executed=total, failed=0 and notExecuted=0.

| Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Bingo.Domain.Tests | 265 | 0 | 0 |
| Bingo.Application.Tests | 118 | 0 | 0 |
| Bingo.BrowserTests | 148 | 0 | 0 |
| Bingo.IntegrationTests | 1390 | 0 | 0 |
| **Total** | **1921** | **0** | **0** |

Integration duration: 30 minutes. No test project or class excluded. The original 16 failures and F all pass; #14 is unchanged. Six new D16 terminal cases account for the increase from the original 1915-test suite. Migration files are unchanged.

`git diff --check`: PASS. The source/test corrections are the four cause-group commits named in the table. This fifth commit contains only the evidence, corrected prior Stats explanation, current handoff, D16 owning contract and AGENTS whole-suite gate wording; role/model policy is unchanged. Owning-document line citations above refer to the starting `0179b9b` contents, before the D16 paragraph insertion. User decision attribution remains in supplied 08-decisions.md.

## Final-commit verification and stop

To satisfy both committed actual execution evidence and the explicit final-commit gate without amending, the final documentation commit is verified once more with:

```sh
git rev-parse HEAD
dotnet clean Bingo.slnx -c Release -v minimal && dotnet build Bingo.slnx --no-restore -c Release -v minimal && dotnet test Bingo.slnx -c Release --logger trx --results-directory /tmp/th-whole-final -v minimal
git diff --check
git diff --check 0179b9b3f4d4dff00a401f642e6a80374b5dc807..HEAD
git status --short --branch
```

The terminal delivery report records that exact final SHA, execution outcome/counts and clean status after the run. This committed report does not pre-claim an unrun final-commit result. The implemented batch stops for external Claude independent review; no self-review, B5 remediation, UI integration, rehearsal, live provider/user database, migration, push, merge or deployment occurred. No unresolved test remains after the completed-code gate.
