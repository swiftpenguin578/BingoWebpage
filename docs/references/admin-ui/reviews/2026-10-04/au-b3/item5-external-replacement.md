# B3 item 5 — external option 1 and WA-9

Authority: [supplied brief](supplied-brief.md) item 5, DELIVERY_PLAN AU20 point 6, WA-5 option 1 and WA-9; source attribution in item1-exact-window.md. Implemented and executed; external Claude review pending.

- External replacement before/during Live and disconnection before first actual Live work with a stored valid or rejected code. Exact configured-window checks remain. Website-created management and unresolved/conflicted operations remain protected.
- Active/unresolved management operations are checked both before provider validation and under the event lock. Sending/Unknown website Create cannot be overwritten by a newly linked external source; the reverse ordering prevents Create from posting.
- Successful replacement/disconnect locally retires the old management connection, clears its protected credential and current applied receipt, and retains operation history. No remote DELETE is sent. Explicit later code adoption reuses the unique management row with the new connection/code, not the retired credential. New link generation resets end-update state.
- Readback performs no provider fetch and selects the current connection operation; retired success cannot prove a new connection. Existing failed/cancelled/unresolved operation readback needed by Create handlers is preserved.
- Late fetch responses are fenced by source/generation/lease checks, preserving the replacement. Existing cross-event provider-reference locks remain intact.

Checks: final integration filter `FullyQualifiedName~ManagePageShowsCreate|FullyQualifiedName~ManualLinkWaitsForManagedProviderWrite|FullyQualifiedName~Au20` passed 44/44 (zero skipped). This re-executes all AU20 integration cases, including lifecycle, populated migration Up/Down/backfill, precision/timezones, retries, concurrent workers, fallback/freshness and replacement races. Earlier item-5 run passed 19/19 (17 new cases plus existing unknown/failed Create). Three additional affected existing cases passed: recreate after managed deletion, ID-only code adoption, update-all provider-reference locking. Release solution build: zero warnings/errors. `git diff --check`: PASS.

Corrections during execution: initial disconnect tests found old-success readback; fixed current-connection selection. Existing handler coverage then found no-link failed Update/Delete must remain visible to prevent showing Create; preserved that guard. The shared-reference-lock test deliberately returns a different provider window than requested: updated its former success expectation to the new Unknown mismatched-receipt contract, preserving its lock and rejected-link assertions, with a deterministic non-microsecond fixture. Final named/all-AU20 run passed.

Controlled doubles and isolated PostgreSQL only. No current-page display added, external deletion, live WOM request, user-owned database access, or independent review. Unrelated StatsPass4Boundary fixture limitation remains recorded in item 1.
