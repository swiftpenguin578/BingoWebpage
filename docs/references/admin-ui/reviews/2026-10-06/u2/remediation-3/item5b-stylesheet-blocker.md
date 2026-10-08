# Brief74 item5b — incomplete WebKit stylesheet checkpoint

Historical checkpoint, superseded on 7 October by the planner's active,
family-scoped approach. The successful continuation and exact checks are in
item5b-active-styles.md; failed attempts below remain evidence, not current status.

Parent0db74ff3af60c1040def025f263029da3f57d5d0. NOT complete, NOT committed.
Stopped under AGENTS repeated-blocker execution limit; no product/reference
decision has been made or requested. Preserve the candidate for continuation.

Candidate shared helper stages destination links inactive, awaits native load,
includes that wait in shared150/400 timing, preserves same-href links, activates
after old region replacement and then removes obsolete old styles. Abort/error
cleanup removes staged links; errors take the existing full-load fallback.
Language uses the same helper without a skeleton. Current candidate constructs
live-document links and sets media=all (or source media) at activation.

New admin-design-styles.browser.js exercises native stylesheet load/error using
actual page CSS plus a cascade sentinel, six Dashboard/Events/Identity directions
at149/151/650ms, an uncached first visit, same-href identity/no refetch, full-load
error fallback and language. Shared shell executes in browser; controlled shell
markup/module, not PostgreSQL/HTTP production-page evidence. Mutation observation
checks loaded sheet presence; saved native requestAnimationFrame checks actual
paint cascade, separately from the paused scheduling clock. No sleeps, enlarged
timeouts or retries.

Executed current-check checkpoint:
- Chromium20/0, exit0 (latest session94825); also earlier20/0.
- WebKit FAIL, exit1, first Dashboard→Events149ms case (latest counterpart):
  two observed native frames have destination region present with cascade
  sentinel empty, link media=all, sheet exists/disabled=false, sheet.media
  remains "not all"; parsed last rule is body { --fixture-page: events; }.
  Exact diagnostic appears in the failing assertion. This is not a pass.
- Earlier attempts/diagnostics: relative-href observer corrected to canonical
  href; imported inert-document links with removed/default media and explicit
  media=all exposed inactive rules; direct CSSOM media activation also failed
  the initial fake-frame check and was removed. Live-document construction
  still fails native-frame proof. Preserved native frames distinguish paint
  evidence from mutation/fake-clock scheduling; no no-flicker assertion removed.
- Fixture Release checkpoint0 warnings/errors,18.28s, before the final JS
  construction change; NOT a clean/final-source Release gate.
- Final checkpoint git diff --check0; frozen tokens/components cmp0 and no
  frozen CSS/reference diff from9b5d6e4.

Item5b remains an uncommitted production/test candidate. Items6 and7 NOT STARTED;
full final JS runner, final rendered/UR gates, final clean Release and complete
design checks1–6 NOT RUN for this incomplete round. Earlier committed item
checks remain evidence only for those items. Whole .NET suite NOT RUN per Q-S1.
No final-SHA whole-suite TRX exists or is claimed.

Next permitted action: same implementer resumes the scoped WebKit readiness/
activation issue from this preserved candidate, proves both engines, commits5b,
then performs6, focused final gates and docs-only7. Planner owns the eventual
whole .NET gate on the completed final SHA. No push/merge/deploy/new workers.
