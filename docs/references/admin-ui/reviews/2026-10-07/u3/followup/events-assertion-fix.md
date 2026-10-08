# Accepted Events assertion correction — 7 October 2026

The single batch run at `5ef3ab3a` exposed two stale assertions requiring an empty loading summary. User decision U3-Q7(a), decisions08 “Count summaries load like the tab counts”, requires fixed words and number-sized placeholders.

Only `scripts/check-u2-events.cjs` changed: assert the two fixed word groups, two tab-style count placeholders and no attention button while loading. Scope the existing tab geometry probe to `.tabs .tab-count`, so the newly approved header count placeholders are not mistaken for tabs. Existing geometry, failure-state, interaction, localization and navigation assertions remain.

Focused command: `BINGO_PARITY_CONFIGURATION=Debug BINGO_PARITY_ENGINES=chromium,webkit node scripts/check-u2-events.cjs`. **28 passed / 0 failed**, both engines. Shared CSS cmp and git diff --check pass. No production changes or repeated full runner/build. Original batch result remains 97/5/102, not rewritten as a fresh full pass.
