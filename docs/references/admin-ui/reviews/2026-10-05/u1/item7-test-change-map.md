# Item7 test changes — exact authority mapping

Correction after review49: the original preservation claim omitted the weakened
read-count and Slice3 substring checks and several removed assertions. The exact
before/after/authority disposition is now in
[remediation/item9-r5-r13.md](remediation/item9-r5-r13.md). The protected test14
and main IdentityFieldConflictIntegrationTests.cs remain unchanged.

| File | Before | After | Authority |
| --- | --- | --- | --- |
| identity-timezone-confirmation.browser.js | Legacy admin-confirmation/editor-guard scripts and global runtime; inline hidden/disabled confirmation; Manage success navigation; old fixture keeps timezone modal after stale response | Real shared shell + AdminFetch + module init/dispose, inert preview templates and one shared layer; Identity success; stale layer closes then fresh Save reopens review. Keeps focus return, draft retention, ordinary and preview Cancel/Back guard, preview-free error, exact Version basis and one POST. Adds five rows, other fields, pending Cancel/Escape/Back refusal, no reason/no auto retry proof. Production defer and HasReviewedValues attributes included in fixture | brief48 point1, A10 runtime, I-1 target, I-7 stale-basis behavior |
| identity-readback.transport.js | Legacy global session/confirmation runtime, values-only readback; matching Up to date, differing values remain uncertain | New module/AdminFetch, versioned body. Match+moved Up to date; differ+moved field conflicts; unchanged version not applied/draft/Save; unavailable remains uncertain/locked draft. Native multipart CRLF, observed LF, UseCurrent, immutable tuple, wrong-event/malformed/refused reads and no POST retry stay asserted; uncertainty departure tested | brief48 point2, A10/A14, AU09 invariant preserved |
| EventCreationUiTests.cs Identity assertions only | Old form Cancel, history sentinel, legacy guard/modal globals, old preview action, disabled timezone select, rail public links, Manage PRG | Same form/baseline/fields and server rules, new module/shared runtime, no extra history entry, template review with ConfirmTimezoneChange payload, read-only timezone branch, only Signups copy URL, Identity PRG. No other page assertions changed | A10, brief48 point3, brief43 I-1/I-6/rail retirement |
| ManagedCompetitionUiTests.cs | Literal Identity maxlength=50 (UTF-16 HTML limit) | Identity data-codepoint-limit=50; new executable browser test proves 50 emoji accepted and 51 rejected; unchanged legacy name remains valid | A10 markup adaptation; A-Identity-1 / I-12 |
| Slice3CreationIdentityPersistenceIntegrationTests.cs | HTTP HTML contains Signups opened/closed | HTML contains Signups open/close, reflecting scheduled review rows. All persistence/refusal/version/audit assertions unchanged | A10 old-markup labels; brief43 I-6 / A-Identity-2 |
| IdentityFieldConflictIntegrationTests.Readback.cs:63 | Object count exactly2 | Exact sorted property-name whitelist eventId,values,version and before.Version == JSON version (Int64). No-store, eventId, exact4 values, per-scenario equality, post-read version and audit assertions unchanged | A14 and brief48 readback-field-count addendum only |
| AdminDesignShellIntegrationTests.cs | Test-only opt-in Identity metadata; literal proof Identity not yet bound | Identity is now bound; proves its metadata true and unbound Schedule false. Same asset/token/toast/legacy-page assertions | brief43 item7 production opt-in; direct consequence of item3's temporary fixture |
| fixtures/identity.cjs (new helper) | Legacy partial embedded separately in two fixtures | Shared controlled fixture shell mirrors defer, module tags, token and page region; no production compatibility adapter | brief48 whole-file fixture/runtime adaptation |
| identity-binding.browser.js (new) | No bound-page proof | Counters/required+summary, legacy-name exception, untouched/KeepMine/UseCurrent, refused/session-lost input, event swaps and Back/Forward, disposed listeners and one current-event POST | brief43 item7 proof |
| AdminDesignIdentityIntegrationTests.cs (new partial) | No U1-specific server proofs | Terminal GET/Current and refused POST, hidden3-method404, Identity PRG/no-op audit, injected committed-write/lost-response/failed-rollback/no-read render, scheduled five-row DST/missing preview and absolute Signups URL | brief43 item7 proof |

Dropped obsolete fixture machinery: legacy modal partial/scripts/global initializer,
inline confirmation button selectors and Identity history sentinel. Kept protections
through the production shared runtime, never via a shipped legacy adapter. The
readback's former Different=>uncertain assertion changes solely under A14; failed
reads retain uncertainty and the immutable request tuple. The whitelist format uses
an exact sorted string equality to satisfy CA1861 without weakening the property set.
