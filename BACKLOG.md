# Backlog

Ideas and fixes to consider later. Nothing here is approved or scheduled; an item
becomes work only when the user picks it and it gets a brief. Bugs found in
production are added under "Bug fixes" as they are noticed.

Priority: **High** = most likely will be done · **Normal** · **Maybe** = decide later.

## Ideas and improvements

| Item | Priority | Notes |
| --- | --- | --- |
| Draft page scales up on large screens (clamp) | High | Draft only: keep today's size as the minimum and grow to a maximum width. |
| Admin pages scale up on large screens (clamp) | Maybe | Decide after using production on a large monitor. Cheapest route: one shared `zoom` rule on the admin shell between today's size and a max width (~1 day); rem/`clamp` across the frozen tokens is several days. |
| Public pages: fixes from the last bingo's feedback | Normal | Mainly text that is too small. |
| Switch your playing account | Normal | Extra functionality/UI to switch which account you play on; possibly a How To section explaining it. |
| Public page CSS cleanup | Normal | Leftover transitional CSS on the public pages (U10-E2, 8 October 2026). |
| Board reference preview | Maybe | Keep the reference modal but show the tile area as plain background with "Not supported yet" (U7-Q3, low priority). |
| Full R-3 rehearsal harness | Maybe | The VM-based rehearsal in `docs/PRODUCTION_RUNBOOK.md`; this release used the lighter local rehearsal instead. |

## Bug fixes

| Bug | Where | Noticed | Notes |
| --- | --- | --- | --- |
