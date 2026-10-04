# B4 item 4 — documentation and register

Status: implemented as the fourth scoped B4 commit; external Claude review is pending for the stable four-commit range.

The documentation now records AU21, AU23 and CAT-1 as B4 backend-delivered outcomes rather than pending implementation, while retaining the external-review and WA-5/RC10 binding boundaries. The Catalogue decisions section records the delivered backend state. The “Bindings not shown in the design references” register now has rows for the decided rate panel, activity Team size in Settings/Add activity, and the D7 confirmation naming every other activity affected by a shared-item rename/image change. The D7 row links [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md) and keeps the current-page refusal until the new UI supplies the intent. The runbook adds the two read-only pre-deploy checks:

```sql
SELECT COUNT(*) FROM source_drops WHERE conditional_on_parent;
SELECT COUNT(*) FROM source_drops
WHERE assumed_participants <> 1
   OR probability_scope <> 'Participant';
```

Both must be zero. A nonzero result stops deployment for a product/data decision; the operator must not infer team size or rewrite retained context. The migration independently fails closed on the second condition. The user's 4 October report of zero and zero is linked as supplied evidence in the runbook and [authorized B4 brief](/Users/christopher/Documents/BingoWebpage/review-notes/27-codex-brief-b4-catalogue.md) lines 1–5, 50–73; it was not run by this worker.

Files updated in this item include [CURRENT_STATUS.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/CURRENT_STATUS.md), [DELIVERY_PLAN.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/DELIVERY_PLAN.md), [FUNCTIONALITY_CHANGES.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/docs/references/admin-ui/FUNCTIONALITY_CHANGES.md), [FUNCTIONAL_CONTRACTS.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/FUNCTIONAL_CONTRACTS.md), [PRODUCT_REQUIREMENTS.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/PRODUCT_REQUIREMENTS.md), [DATA_MODEL.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/DATA_MODEL.md), and [PRODUCTION_RUNBOOK.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/docs/PRODUCTION_RUNBOOK.md).

The four scoped commits, in order, are:

1. `01c5dc2` — AU21 explicit shared-item scope.
2. `00dfd40` — AU23 ordinary rate text and roll mechanics.
3. `fd3ce10` — CAT-1 activity team size and fail-closed migration.
4. The documentation/register commit containing this evidence and the authority updates below.

Worker checks for this final checkpoint:

```text
dotnet build Bingo.slnx --configuration Release --no-restore --disable-build-servers
Build succeeded; 0 Warning(s), 0 Error(s).

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat1MigrationFailsClosed
Passed 1, Failed 0, Skipped 0, Total 1.

git diff --check
PASS (after the evidence/register files and documentation edits are added).
```

These are worker-reported results pending Claude's independent source review. No manual UI acceptance, production pre-deploy query, migration against a user-owned database, merge, push, deployment, B5 work, WA-5 binding or rehearsal execution is claimed. The next permitted action is external Claude review of the stable four-commit range.
