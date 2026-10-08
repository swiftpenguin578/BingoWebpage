# Backlog

Ideas and fixes to consider later. Nothing here is approved or scheduled; an item
becomes work only when the user picks it and it gets a brief. Bugs found in
production are added under "Bug fixes" as they are noticed.

Priority: **High** = most likely will be done · **Normal** · **Maybe** = decide later.

## Ideas and improvements

| Item | Priority | Notes |
| --- | --- | --- |
| Draft page uses large screens better | High | Readable at 1080p (user check, 8 October 2026) but much of the screen is empty. Draft only: larger player chips and team cards, or a scale-up between today's size and a max width. |
| Admin pages scale up on large screens (clamp) | Not needed for now | User check on a 1080p monitor (8 October 2026): all pages look fine; Schedule and Identity a bit small but acceptable. Revisit only if that changes. |
| Public pages: fixes from the last bingo's feedback | Normal | Mainly text that is too small. |
| Playing account in the header | Normal | Move the account switcher from its current place into the site header, shown only while the event is Live. It always names the account you are playing on; players with one account see it without a switch option (today they can see "Not active"). Possibly a How To section on switching. |
| Public page CSS cleanup | Normal | Leftover transitional CSS on the public pages (U10-E2, 8 October 2026). |
| Board reference preview | Maybe | Keep the reference modal but show the tile area as plain background with "Not supported yet" (U7-Q3, low priority). |
| Full R-3 rehearsal harness | Maybe | The VM-based rehearsal in `docs/PRODUCTION_RUNBOOK.md`; this release used the lighter local rehearsal instead. |
| CI: make JavaScript tests block the release | Normal | `build-and-test` (and so the production image) ignores the JS job today. Once the JS tests are reliable, add `javascript-tests` to its `needs`; also shard the JS job (54 browser scripts × 2 engines on one runner, ~25–40 min). |

## Bug fixes

| Bug | Where | Noticed | Notes |
| --- | --- | --- | --- |
| Team removal audit can still grow with team size | Teams/Draft — remove a team | 8 October 2026 (review 109 F1) | Ended membership ids are listed; ~4,000 chars only at ~90 members or max-length names. Cap or hash above a threshold. |
| Audit label missing a name | Audit page — finalized roster removal | 8 October 2026 (review 109 F2) | Removing someone not on the published roster shows "Membership ·" without a name; fall back to the character name. |
