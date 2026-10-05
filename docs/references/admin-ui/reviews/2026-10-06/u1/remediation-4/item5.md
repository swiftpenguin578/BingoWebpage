# Round4 item5 — scoped Danish corrections

Authority: brief60 item5 /59 N4 / planner Danish-cancelled decision, user spot-check per A5. Only AdminDesign resources/Identity preview-row bindings change.

| Key | Before | After |
| --- | --- | --- |
| AdminDesign.{0} is finished, so its identity can’t be changed. | {0} er afsluttet, så dets identitet kan ikke ændres. | {0} er afsluttet, så dets identitet ikke kan ændres. |
| AdminDesign.{0} is archived, so its identity can’t be changed. | {0} er arkiveret, så dets identitet kan ikke ændres. | {0} er arkiveret, så dets identitet ikke kan ændres. |
| AdminDesign.{0} was cancelled, so its identity can’t be changed. | {0} blev annulleret, så dets identitet kan ikke ændres. | {0} blev aflyst, så dets identitet ikke kan ændres. |
| AdminDesign.Signups open (new scoped row key; English Signups open) | Identity row reused legacy Signups open = Tilmelding åben | Tilmelding åbner |
| AdminDesign.Signups close (new scoped row key; English Signups close) | Identity row reused legacy Signups close = Tilmeldinger lukker | Tilmelding lukker |

Existing state/switcher Cancelled=Aflyst already matches and is unchanged. Existing Cancel=Annuller remains for dismiss buttons. No other subordinate word-order issue was present in the scoped resource matches; standalone “kan ikke” sentences remain grammatically correct. All legacy keys retain exact prior values.

Supplemental exact resource assertions cover all five changes and retained legacy/dismissal values. Actual PostgreSQL/HTTP Danish Identity render asserts both signup-row labels; cancellation render asserts the exact aflyst banner, while its switcher still uses the localized cancellation date. **3 passed /0 failed /0 skipped**. Whole Bingo.BrowserTests **150/0/0**. No previous assertion weakened; supplemental tests only. Diff clean. Final user language spot-check remains pending.
