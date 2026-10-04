# B4 item 1 — AU21 shared-item scope

Status: implemented by the B4 worker in `01c5dc2` (`fix(catalogue): require explicit shared item scope`); external Claude review and new-UI/manual acceptance are pending.

Remediation status: the initial read-only review identified A1/A2 gaps. The
authorized remediation is recorded in
[`remediation/item1-au21-remediation.md`](remediation/item1-au21-remediation.md)
and committed in `c4c52a4`; the initial checkpoint above remains historical.

The source decision is D7 option (a): a shared item's name or image belongs to the item and therefore changes everywhere, but a save that affects other activities requires explicit confirmation naming those activities. Without that confirmation the save has no mutation; an item used by one activity keeps the existing single-activity behavior. The user's authorized B4 brief also requires explicit “use the shared item” intent on adoption, no per-activity split, and preservation of independent item/version, stale refusal, dependency-safe deletion, price invalidation and draft refresh. See [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md), “B4 (Catalogue) brief decisions” and “Catalogue layout”, and the [authorized B4 brief](/Users/christopher/Documents/BingoWebpage/review-notes/27-codex-brief-b4-catalogue.md) lines 33–49.

The Catalogue handlers now refuse adoption of an existing item unless the add contract supplies `UseExistingItem`. Shared name/image changes detect the other activities using the item and refuse without the named confirmation; the accepted path updates the shared item and does not split identities. The current page has no new confirmation control, so its unable-to-supply case remains a clear refusal for shared items. Rate/mechanics remain source-drop-owned.

Worker-executed PostgreSQL checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Au21
Passed 2, Failed 0, Skipped 0, Total 2
```

The two cases cover refusal/no mutation followed by explicit adoption, and refusal/no mutation with the affected activity named before a shared rename/image update. The required Release build and `git diff --check` are recorded in [item4-docs-register.md](item4-docs-register.md). No production or user-owned database was accessed. The user's production pre-checks are evidence for the later AU23/CAT-1 gates, not test output from this item.
