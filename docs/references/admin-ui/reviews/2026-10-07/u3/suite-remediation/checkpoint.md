# Whole-suite remediation checkpoint — 7 October2026

Clean baseline bb391bc. Four independently authorized findings are fixed, checked and committed separately:

- SR1 `56d7a45d`: exactly nine missing Signup setup Danish entries; focused regression1/0/0.
- SR3 `c7998752`: all three stale legacy-page tests now use Participants, with unchanged assertion strength; shell PostgreSQL class26/0/0.
- SR4 `c942c1c9`: exact confirmed2/waiting2 checks at Signup setup, current displayed `2 of 1` and `2`; ParticipantFlow PostgreSQL class10/0/0.
- SR5: owning checkpoint commit restores retired handler plus original Setup classification; C11 class60/0/0 and classification21/0/0. C11 test unchanged.

Each srN.md/json records scope and exact results/times. No whole.NET, fullJS rerun, unrelated timeout-flake work, review-environment refresh, acceptance edit or publication.

## SR2 — consequential scope boundary, no edits made

The named localization test loops **every** SharedResource.resx entry and requires `AdminDesign.` plus a matching English value and Danish entry. There are **80** unprefixed English keys, all with existing Danish translations. The first assertion reports only the first key (`Times are in {0}; change timezone on`); changing that key alone merely exposes the next failure. [Exact inventory and executed class result](sr2-boundary.json): **2 passed /1 failed /0 skipped**, the sole failure is SR2.

The named old summary key has no current use. The actual Schedule summary uses `Times are in {0}; change it on`; its Danish is already the required `Tider vises i {0}; skift den på`. Neither has been changed here.

Decision requested from dispatcher/planner: authorize normalization of the80 new-layout fallback keys and directly affected lookups while preserving displayed text, JS payload keys and legacy translations, or specify a narrower resource strategy. Recommended: consistently scope the new-layout resource lookups; do not weaken the test. Full AdminDesignLocalizationTests pass remains pending this decision. Stop after completing unaffected work; not ready for a green-suite claim.
