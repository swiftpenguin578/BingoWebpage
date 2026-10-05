# U1 item 1 — explicit route classification

Replaced filename/substring policy with `AdminEventPagePolicies`, keyed by page
model type and exact GET/POST handler name. Explicit terminal-view switches remain
on only for Manage, Finalize, Participants, Participant and WiseOldMan. Identity's
switch remains off until item7. Hidden/Discarded ordering, limited SuperAdmin
Overview inspection, exact Hide query, Questions committed-add replay, WOM matrix,
Board correction and retained artwork, and service-owned mutations are preserved.
No routes, existing tests or production mutation rules were changed.

Inventory correction: Razor Pages uses `NonHandlerAttribute`; MVC `NonAction`
does not exclude the five Manage Prepare* methods or retired Finalize methods.
They are included in the manifest with their baseline gates. The unchanged
PrepareDestructiveConfirmation HTTP proof passes, as does test #14.

Executed in the assigned checkout (Release, isolated PostgreSQL/Testcontainers,
controlled provider doubles; no user database or live WOM calls):

1. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdminEventHandlerClassificationTests --logger 'trx;LogFileName=item1-classification.trx' --results-directory /private/tmp/bingo-u1-item1`
   — **18 passed, 0 failed, 0 skipped**; compilation completed without warnings.
   Reflection enumerates every public OnGet*/OnPost* handler and pins the exact
   table and terminal flags; separate assertions pin all lifecycle state sets.
2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Slice3DestructiveLifecycleIntegrationTests|FullyQualifiedName~TerminalQuestionsAllowOnlyExactCommittedAddReplay|FullyQualifiedName~FinalizedManageHideUsesExactHandlerBoundaryAndValidatesBeforeMutation|FullyQualifiedName~WiseOldManTerminalGetRendersInspectionOnly|FullyQualifiedName~WiseOldManUnknownPostHandlerRedirectsWithoutMutation' --logger 'trx;LogFileName=item1-route-boundaries.trx' --results-directory /private/tmp/bingo-u1-item1`
   — **24 passed, 0 failed, 0 skipped**. Includes unchanged test #14, terminal
   replay cases, exact hidden-event boundary, live Schedule, lifecycle handlers,
   terminal WOM inspection and unknown WOM handler refusal.
3. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ParticipantListPaymentRemainsEditableThroughFinalReviewAndFinalizedButNotDiscarded|FullyQualifiedName~ManualRefreshRunsInFinalReviewButSkipsAnUnmatchedEndAndFinalizedEvent' --logger 'trx;LogFileName=item1-payment-wom.trx' --results-directory /private/tmp/bingo-u1-item1`
   — **2 passed, 0 failed, 0 skipped** (S4/S6 HTTP persistence proofs).

`git diff --check` passed. No existing assertion changes. This is an implemented
and checked checkpoint awaiting Claude's independent batch review, not a full
suite or manual acceptance claim. Next: item2 JS runner and unchanged baseline.
