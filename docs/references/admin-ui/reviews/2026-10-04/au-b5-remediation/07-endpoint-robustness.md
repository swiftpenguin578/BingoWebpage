# Item 7 — endpoint robustness

Backend remediation done; Claude recheck and page bindings pending.

Implemented: picker no-store and service-refusal mapping; Review/Board/Teams admin
lookups inside failure handling; failed reads return Known=false; current handler
Forbid behavior for disabled admins; malformed WOM audit isolated to its field;
ambiguous Board objectives remain readable with every identity retained; JSON
ApproveState/PublishState suppress old page status messages. New proofs cover
actual other-team/other-event assignments, former member now on another team,
separate PostgreSQL sessions for competing-admin approve/publish, and HTTP picker
200/no-store/404. No writes from failed readbacks; no request-success attribution,
replay, permission or lifecycle changes.

Existing test changes (no expectations removed or weakened):
- `B5DraftReadbackDoesNotInferRedrawFromChangedOrder`: direct SetDraftPosition +
  synthetic RenewControl setup replaced by real OnPostScrambleAsync calls on two
  teams, bounded at 40, stopping on the same-order random outcome. All original
  identity/order/version/persisted-time assertions remain; added two-team and
  observed-outcome assertions. Each command uses deterministic whole-second UTC
  time, so its lease renewal is a real change. This is the required production
  command proof in brief35 item7 / report31d L1, not retrying a failed test.
- Confirmed-captain case adds the required usable-captain precondition before
  changing its signup status. All existing assertions remain.

```
dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore -c Release -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationReviewReadbackHandles|FullyQualifiedName~B5RemediationReviewDisabled|FullyQualifiedName~B5RemediationCorrectionPickerHttp|FullyQualifiedName~B5RemediationCorrectionChecksActual|FullyQualifiedName~B5RemediationDraftReadFailures|FullyQualifiedName~B5RemediationMalformedWom|FullyQualifiedName~B5DraftReadbackDoesNotInfer|FullyQualifiedName~B5DraftReadbackIncludesInclusion|FullyQualifiedName~B5RemediationBoardReadFailures|FullyQualifiedName~B5RemediationDuplicateObjectives|FullyQualifiedName~B5RemediationSeparateAdminSessions|FullyQualifiedName~B5BoardReadback' --logger 'trx;LogFileName=item7.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Results: Web build 0 warnings/errors; Integration **20 passed, 0 failed, 0 skipped**;
diff check passed. Initial two separately launched filters both stopped compilation
at CS1729 in the new other-event fixture (missing required PlacementRule argument).
The fixture constructor was corrected; assertions unchanged; combined run passed.
No new environment failure occurred during this continuation.

## Resolved authentication boundary

The planner wording correction, delivered by the user through `/root` in planner
chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`, says: “a disabled or no-longer-authorized
admin is refused and receives no data.” Recorded source:
[08-decisions.md, B5 remediation item7, lines252–253](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-remediation-item-7-planner-decision-5-october-2026).
This is the planner's brief correction, not a newly invented user product decision.
Handler tests retain ForbidResult. At HTTP, existing ValidatePrincipal signs out
changed/disabled accounts before handlers execute: **302 to /Account/Login with
accessChanged=true**, not HTTP403. No authentication middleware changed.

Four new real HTTP cases cover Review readback, correction picker, Board readback
and Teams readback. Each first proves authorized data is available, disables that
same admin in PostgreSQL, then asserts exact302, login path, accessChanged=true,
exact decoded ReturnUrl and two query fields, and an empty body. Redirect following
is disabled. Thus no readback/picker data is returned after refusal.

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationDisabledSession' --logger 'trx;LogFileName=item7-auth.trx' --results-directory /tmp/au-b5-remediation -v minimal
```

Implementer-executed result: **4 passed, 0 failed, 0 skipped**, 7seconds. The earlier
20/20 checks remain applicable; only new tests and this evidence were added after
that run. Future binding must detect login redirects and apply C-CMP-2 before
sign-out; item8 records the obligation. No current UI was added.
