# U2 brief72 item7 — importer-shaped review seed and overlap check

Parent: 9bdf614498bbceda4bf8ddf05b22133bdc97ba27. Implementer evidence only.

Before/after (frozen import only; accepted platform board positions untouched):
- First reconstructed submission: start+1hour → event start (import sequence0);
  approval/contribution/Approve action: +1minute → submission+1second.
- Activity upstream timestamp: fabricated event end → null, matching this input;
  synchronization's aggregate end fallback remains the importer's own fallback.
- Board/template/frozen requirement position: 0 → 1.
- Frozen approval tile instructions: invented screenshot instruction → exact
  HistoricalEventImporter.Disclosure. Imported event/submission/action disclosure
  now uses the same existing constant, not a second synthetic wording.
- Failed scheduled opening: readiness only → readiness plus the very same
  EventSignupLifecycleService.CurrentEventBoundaryConflictAsync used by production.
  This existing read-only helper is exposed for reuse and returns the existing
  ReadinessItem; query/order/exclusion/description/production mutation unchanged.
  No new service, schema, lifecycle path or product scope.

Proof extends UiReviewScenarioIntegrationTests for both live/final-review profiles:
fresh PostgreSQL reads exact submission/approval/action timestamps, null upstream
times, all four board/template/approval positions and exact frozen instructions.
The seeded failed window has no overlap; an unpersisted probe with a known public
event's window proves the shared production check returns EVENT_WINDOW_OVERLAP
and that event's name. Seeded audit/inbox descriptions are asserted against the
combined production blockers; prior authorization/hidden/session/printed URLs
remain intact.

## Executed checks

- Final UR PostgreSQL class: **2 passed / 0 failed / 0 skipped**,21s.
  Command: dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj
  --configuration Release --no-restore --filter FullyQualifiedName~UiReviewScenarioIntegrationTests
  --logger 'trx;LogFileName=u2-rem2-item7-final.trx' -v:minimal.
  TRX: tests/Bingo.IntegrationTests/TestResults/u2-rem2-item7-final.trx.
- Parity fixture Release refresh:0 warnings/errors,1.66s.
- Bundled Node scripts/check-u2-ur.cjs: **4 passed / 0 failed**, live and
  final-review in Chromium/WebKit. Paging (>25), Confirmed cancellation, real
  EN/DA sorting, attention, no capacity/no dates, frozen import/shared first,
  Live/Final review, real audits/inbox and repeated A16/disposal/Back/Forward pass.
- git diff --check exit0; no baseline frozen tokens/components diff.

Initial UR class before
the additional ReviewAction assertions passed2/0/0 (21s). An extra assertion's
missing namespace was caught at compile time and corrected, not treated as a
runtime pass. Item8 and final gates remain pending. Both pages await Claude
review, then user visual acceptance. Next: item8 root-cause reproduction before
any infrastructure fix. No push/merge/deploy.
