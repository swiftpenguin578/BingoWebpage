# Round2 item2 — U-F completion / 52b N2–N4

Save uses aria-disabled and remains natively focusable, with No changes to save
only in its tooltip. The submit guard still rejects pending/unchanged attempts.
Status renders Unsaved changes only for changed/uncertain state and returns to
its clean note after revert; Saved/Up to date notes remain available as appropriate.
The timezone dismiss button is Cancel. UI_SYSTEM records the complete U-F rule.

Before → after → authority (brief53 item2 / U-F):
- Exact timezone modal text Keep editing → Cancel; all other exact text preserved.
- Pending/finished wait predicates previously read native disabled; now read exact
  aria-busy true/false. They still wait for the actual pending lifecycle and do not
  mistake aria-disabled on a clean form for an unfinished request.
- Existing Playwright isDisabled assertions remain (they include aria-disabled).
  Added explicit native-disabled false, aria-disabled true, focus/tooltip, empty
  clean status, type/revert state and no POST on keyboard/click activation proofs.
- Save pending/reason/stale/version/one-POST/read-count/tuple assertions unchanged.

Identity binding, timezone and readback JS all PASS. Logs
`/private/tmp/bingo-u1-r2-item2-{binding,timezone,readback}.log`. Diff check clean.
