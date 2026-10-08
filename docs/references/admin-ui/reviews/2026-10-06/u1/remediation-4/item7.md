# Round4 item7 — positional parity and final handoff

Authority: brief60 item7 /59 test-integrity note(a). The existing checker is extended, not replaced. Prior text/icon/computed-style/CSS-class/state/navigation assertions remain. The phone width/control exception is the confirmed user decision in decisions08; no new exemption or broad tolerance was added.

## Explicit geometric checks

All tolerances below are **1 CSS px per listed dimension or gap**, allowing only subpixel rounding. Boxes are measured after finite animations and fonts settle, on both reference and actual served Razor/Kestrel content.

| Pair | Dimensions/spacing checked |
| --- | --- |
| Page head | x, y |
| Card top | x, y; card top minus page-head bottom |
| First section heading | x, y; heading top minus card top (V19) |
| Sidebar event block | event-switch y |
| First event nav item | y; item top minus event-switch bottom (V6) |
| Save bar | x, y, width, height |
| Breadcrumb | x, y, height (approved control placement affects available width) |

Focused final-layout checks: **6 passed /0 failed** (desktop light/dark and390px, Chromium/WebKit). One negative run of the same extended checker against preserved untouched49ae49c production code specifically detected, in **both engines**:

- V6: event-to-first-nav gap **1px actual vs10px reference**, tolerance1px.
- V19: card-to-first-heading offset **41px actual vs21px reference**, tolerance1px.

`position-baseline/` preserves exact JSON and before/reference PNGs. The negative run reports0 passed/4 failed: two property-comparison failures plus the two pre-existing document-title assertions, not four separate V6/V19 findings. It is expected negative evidence, not a current failure. Archive provenance remains round3 item0/item7:49ae49c tar SHA25619584d1ea86a840700e9d0c717ae6640c36e8d62a2562b76488b12e80eccc5a1. The existing controlled fixture is reused; no reviewer credentials, user database or running visual app is used.

## Final gates

Clean nonincremental Release **0 warnings /0 errors** (11.17s). Whole Bingo.BrowserTests **150 passed /0 failed /0 skipped**. Full JS **51 passed / 0 failed / 51 executions**; real served/reference parity **66 passed / 0 failed**, both Chromium/WebKit, with **0 undefined CSS classes**. Exact final positional JSON/PNG pairs are in position-final/; baseline pairs in position-baseline/. Final result files, class summary and build/test logs are beside this evidence. Focused PostgreSQL/rendering and browser proof provenance is preserved in items1–6, including initial test-development failures separately from final passes. Frozen tokens/components byte comparisons and diff check pass. CI is unrun.

The **whole .NET suite remains pending Claude/user execution on the final commit**,0failed/0skipped required. Baseline4aca5a1 had2031passed/1failed/0skipped and did not meet the batch gate. Independent recheck59 was54PASS/2PARTIAL/0FAIL; the user's round3 inspection found no mistakes. Neither is final round4 acceptance. Claude rechecks these named changes, then user checks light/dark, EN/DA, phone and desktop Safari focused-field Save/Back.

```sh
cd /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"
```

Ordered commits: item1 c753326, item2 47e1fd4, item3 e108045, item4 8143029, item5 3d43134, item6 dc03454, item7 this commit. Stop here after its commit. No UR/U2+/laneT, migrations, review worker, push, merge, deployment or rehearsal.
