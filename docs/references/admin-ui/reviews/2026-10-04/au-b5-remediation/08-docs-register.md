# Item 8 — owning documentation and binding register

AU14/AU17a/AU17/AU19: **backend implemented, remediation done, Claude recheck
pending, binding pending**. No independent review or manual acceptance is claimed.
Authorities: brief35 item8; reports31/31e; supplied
[08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md)
B5 review D12/D13/D15/D16/D17, Reference sweep decisions, and item7 planner wording
correction. The latter was delivered by the user through planner `/root`, chat
`01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`; it preserves middleware and distinguishes
handler Forbid from actual HTTP302. D15 confirms the earlier B5 assertion/extra
commit approval; that implementer received the quoted “I approve” through `/root`.
No older approval is invented or extended.

## Changes and boundaries

- Owning plan, requirements, contracts, architecture and functionality status now
  distinguish completed backend remediation, pending Claude recheck and binding.
  Corrected plan EVD-02/scope preface, requirement queued preface and Review R6
  per-account context ownership. The accepted test-health whole-suite result is
  historical evidence, not this remediation's gate.
- AU17a original evidence explicitly names the Cannot→Can test expectation change
  under D11. D12 subsequently narrows former membership to upload coverage, with
  current members retained and latest Playing role required. Item2 records the
  brief's “after” vs decision's “not ended before” boundary and proof.
- Register rows cover D17; S1/S2/S3/S9/S10/S13; C-ACC-1/C-ACC-2; cap-limited B2;
  A-Overview-3/6/7, B-Final-1, C-WOM-2; plus the one authorized session-loss row
  across picker/Review/Board/Teams requiring C-CMP-2 before sign-out.
  Existing AU14 row carries roster PublishedAt and WOM CreatedAt/UpdatedAt.
  S13 unresolved queue-reorder text is closed as not approved.
- **AU19 C1 needs no new gap row:** the multi-issue list already exists in the
  Board reference (label/text/go); codes/IDs are transport. No artificial row added.
- Sweep rows are future binding obligations, not implementation claims. D17 view
  redirect removal happens in each page's binding. S4/S6/S8/F3/F4 server fixes
  remain stopped; F1 stays Teams binding. No frozen reference, middleware,
  migration, new current UI or release tooling changes in this item.

## Focused evidence retained

| Item | Evidence | Passing execution |
| --- | --- | --- |
| 1 | [Public identities](01-public-identities.md) | Implementer Integration1 + Browser4 |
| 2 | [D12 eligibility](02-correction-eligibility.md) | Implementer Integration12 |
| 3 | [Review metadata/caps](03-review-metadata.md) | Implementer Integration13 |
| 4 | [Board issues](04-board-issues.md) | User Integration15; implementer additional11 |
| 5 | [Publish comparison](05-publication-comparison.md) | Implementer corrected comparison9; unaffected5 reused |
| 6 | [Roster times](06-roster-operation-times.md) | Implementer Integration3 |
| 7 | [Endpoint robustness](07-endpoint-robustness.md) | Implementer Integration20 + HTTP4 |

Each listed passing run has0failed/0skipped. Per-item files retain earlier compile,
fixture and assertion failures and their corrections. Item4 retains sandbox
PostgreSQL28P01 initialization failure, precise cause unproven; user-run15/15 is
separate evidence after planner-authorized continuation, not a sandbox pass.

## Item8 checks

`git diff --check`: PASS. Scoped consistency command (assigned worktree):

```sh
python3 - <<'PY'
from pathlib import Path
p=Path('DELIVERY_PLAN.md').read_text(); section=p.split('### Bindings not shown in the design references')[1].split('### Proposed reference corrections')[0]
for token in ['D17 / D16','S1 in','S2 in','S3 in','S9;','S10 in','S13 in','C-ACC-1','C-ACC-2','A-Overview-3/6/7','B-Final-1','C-WOM-2','Cap-limited Contribution','C-CMP-2','PublishedAt','CreatedAt']:
 assert token in section, token
for n in ['Review','Board','TeamsDraft','Participants','Overview','Catalogue','Accounts','FinalReview','Wom','Identity','Schedule','SignupSetup']:
 assert Path('docs/references/admin-ui/'+n+'.dc.html').exists(), n
assert 'AU17/RC07 pending' not in p
assert 'other ticket statuses are unchanged' not in p
assert 'implementation remains queued, not complete' not in Path('PRODUCT_REQUIREMENTS.md').read_text()
assert 'Credited account correction refreshes its own context | RC07 + AU17' not in Path('docs/references/admin-ui/FUNCTIONALITY_CHANGES.md').read_text()
print('PASS: required register obligations, all 12 referenced design files, and stale-owner text checks')
PY
```

PASS. Initial token check incorrectly expected `S1 |`, while the source column
reads `S1 in [08-decisions.md]`; check token corrected to `S1 in`, no product/doc
requirement relaxed. Documentation changes only in item8; no test expectation edits.

## Final-commit gate and handoff

At this commit's creation, final gates are **pending**, not passed. Execute on the
final commit, then report exact SHA and counts to `/root` without a ninth commit
or amendment merely to package results:

```sh
dotnet clean Bingo.slnx -c Release -v minimal
dotnet build Bingo.slnx -c Release -v minimal
dotnet test Bingo.slnx -c Release --logger 'trx;LogFileName=b5-remediation-final.trx' --results-directory /tmp/au-b5-remediation-final -v minimal
git diff --check
git status --short --branch
```

Required: clean Release0warnings/errors; whole suite across every project0failed/
0skipped. Focused checks never substitute. Any new sandbox environment failure
stops dependent execution for user-terminal routing; never retry around it or
claim a pass. Stop for external Claude recheck; all UI binding and publication
remain unassigned. Retain B4 accepted29PASS, test-health accepted34 and release
R1/R3/manual gates in CURRENT_STATUS.
