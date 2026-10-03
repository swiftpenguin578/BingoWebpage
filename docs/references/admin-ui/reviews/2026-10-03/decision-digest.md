# Fix-batch decision digest — 3 October 2026

This is a sanitized digest of `08-decisions.md` and `09-codex-handoff-fix-batch.md`.
Those source files are identified by hash in the sibling README.

## Authorized route

The user authorized one direct implementer, F1 proposal first, then F2 and F3 as
separate checkpoints, followed by F4 through F9. Each item receives focused tests,
durable evidence and its own local commit. The batch stops after stable handoff to
Claude for independent review. No other AU/RC work, UI integration, deployment,
merge, push, provider call or production database mutation is in scope.

## Decisions that constrain implementation

- Banner retirement must have a supported full-deploy path that honors cleanup and
  retained-data rules. Any path that deletes or changes retained data requires an
  explicit decision before implementation.
- Historical Luck v1 conversion is explicit, bounded and idempotent. It must not
  run on every startup or use newer evidence. Failed conversion is visible and
  leaves the source row untouched.
- Reset-link consumption rechecks both people: the recipient remains active,
  non-SuperAdmin and resettable, and the issuer remains active and authorized to
  issue the token now. Recipient role/disable/ownership changes and issuer role or
  active changes supersede unused tokens transactionally.
- A late event end catches up to the configured end; an early end remains based on
  the click time and keeps the grace window.
- AU13 keeps the manual planning team-size estimate editable after finalization;
  actual roster sizes never replace that estimate.
- Finalization messaging names the blocking event and says results must be
  published; a Live event must first be ended, then published. Reopen reasons are
  capped at 2000 characters at both page and service boundaries.
