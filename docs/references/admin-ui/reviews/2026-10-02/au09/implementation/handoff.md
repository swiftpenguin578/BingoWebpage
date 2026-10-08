# AU09 — stable implementation handoff

Exact checkout /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage; branch codex/participants-functionality; packaged HEAD 0ec8add9314a65ab66751bf44bbb4f5195ff854f. Implementer /root/au09_implementer, Astra/high. All work remains uncommitted; initial independent review identified one P2 multiline serialization defect, now remediated and awaiting the same reviewer’s named recheck.

## Implemented

Approved dispatch clarification promoted narrowly into DELIVERY_PLAN AU09 and FUNCTIONAL_CONTRACTS 4.3 before production edits. Full immutable canonical Name/Description/BuyInDescription/Timezone expected tuple remains separate from original edit intent. Explicit Use current takes reviewed value; untouched fields use latest actually observed values. Post-timeout reads never recompute expectations.

Existing Identity page now exposes Current GET handler under the same authorization, lifecycle and visibility filters as its rendered read. It returns only event ID and all four full canonical current values with no-store. No receipt, request attribution, version rebasing, audit mutation or new persistence/service/route infrastructure.

Existing event-identity.js exposes the small frozen readback session; only GET is used by Check again. Full equality yields upToDate, different fields yield different, failed/redirected/malformed/wrong-event reads yield unknown. Existing enhanced timezone confirmation captures the session before dispatch and retains it on a lost response, non-success status, unexpected destination/body or returned uncertain server outcome. Further confirmation/form submits check current state rather than reposting the mutation. The draft, baseline and later unsaved edits stay intact for all read results; matching is current-state evidence only. Unexpected redirects cannot navigate away with the draft. Generic caught save exceptions now report uncertainty and expose a narrow transport marker. English/Danish current-state/recovery wording added.

Protected AU08 three-way write comparison, explicit resolution guards, timezone review and serializable save/audit logic remain intact. No changes to slug, validation limits, UTC instants, auth policy/filter, schema, migrations or other mutation owners.

## Executed checks

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~IdentityReadback' --logger 'trx;LogFileName=au09-focused.trx' --results-directory /private/tmp/au09-implementation-20261002`: 6/7 PASS, first run. Applied/lost-response, unapplied, other-admin matching/different, successful unseen-disjoint merge remaining Different, explicit Use current, complete maximum-length optional values, repeated read/no-write assertions all passed. Hidden-event fixture alone used a nonexistent hiding actor and hit FK_events_accounts_hidden_by_account_id.
- Corrected that fixture to the seeded event creator only; focused `FullyQualifiedName~IdentityReadbackUsesExistingAuthorizationAndHiddenEventBoundary` rerun: 1/1 PASS (`correction.log`, `au09-correction.trx`). Seven distinct real PostgreSQL/authenticated HTTP cases pass across runs. No broad suite rerun.
- `tests/Bingo.BrowserTests/identity-readback.transport.js`: PASS (`transport.log`). Shipped scripts in isolated headless Chromium; lost mutation response, matching/different/503/network/redirected/malformed/wrong-event reads; explicit Use current and observed untouched values; immutable snapshot despite later edits; exactly one mutation POST and GET-only recovery. This controlled fixture is not a production browser walkthrough or visual/manual acceptance.
- Directly affected existing `identity-timezone-confirmation.browser.js`: PASS (`timezone-transport.log`). Success fixture URL aligned with real Manage redirect. Returned validation/stale preview/draft transport remains protected. Both fixtures used bundled Node/Playwright runtime, no user database/app mutation.
- Final `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS, zero warnings/errors (`build.log`).
- Scoped diff/whitespace and added-content leak check: PASS (`scoped-checks.json`). No introduced credentials or private keys.

## Stable review evidence

Complete AU09-only patch: au09.patch SHA-256 `2678c7969a12bdf56a7664c019ffb06452347fd7bd55ae731365aba94903d868`.
Ten-source source.sha256 manifest SHA-256 `a93e95844bc5d4ba30174cf0325875a5eef52379b7075f1cbd61244d9de94688`.
Before/current owned snapshots, baseline-sha256.json, baseline-status.txt, source-identity.json, logs/TRX and test-results.json retained here. DELIVERY_PLAN before review snapshot is normalized only to exclude orchestrator-owned metadata outside AU09; full initial baseline hashes remain preserved. Inherited non-owned source identities unchanged except authorized CURRENT_STATUS, DELIVERY_PLAN queue/status metadata, and reference-register AU09 tracking row (bounded nonowned-reference-register.diff). None of those tracking changes are included in AU09 patch. The planner-authorized UI_PAGE_MATRIX top acceptance addition is also preserved and excluded.

## Deferred and next owner

Full new-reference/ordinary-save UI binding, AU08 conflict-choice UI, visual/manual acceptance and broader release gates remain deferred as assigned. Session persistence across reload/navigation is not introduced; current enhanced confirmation retains its in-page draft/session only. No claim of request-specific save proof or exact server-merged tuple reconstruction. Read failure execution is the controlled client transport fixture; PostgreSQL cases exercise actual successful reads, authorization, visibility and mutation outcomes.

No stage/commit/push/merge/deploy, next ticket, app restart/reset, provider or user-database action. No new worker created. Orchestrator should dispatch the fresh independent reviewer against this exact frozen checkout/patch and approved AU09 contract. Implementer remains available for named remediation only.

## Named remediation — P2 multipart newline mismatch

Initial review: /private/tmp/au09-review-20261002/review.md (ef3b4f54e831a646514cd394d5321ee7b5f0406a422618a0491d6706d7ea630c). Only event-identity.js and identity-readback.transport.js changed for remediation. Frozen intended/new-write values and original-value comparison now follow native multipart CRLF normalization before existing trim; untouched or Use current values retain exact observed line endings. No domain/server mutation semantics changed.

Expanded the focused shipped-script Chromium fixture to send actual multiline Description and BuyInDescription textareas; readback uses values extracted from the real serialized multipart POST. Matching sent text yields Up to date, a genuinely changed second line yields Different, and original-versus-draft line endings compare at the submitted boundary while retained/reviewed values remain exact. Existing draft retention, Unknown, no attribution and one-POST/GET-only checks also pass. Executed once after correction: newline-remediation-transport.log PASS. Scoped diff check PASS. Prior seven PostgreSQL cases, affected timezone fixture and Release build remain applicable; no C#/Razor/contract changes in remediation and no redundant reruns.

Pre-remediation frozen evidence is retained under pre-remediation/. Updated complete patch, ten-source manifest and current snapshots reflect the named correction; independent review PASS is not yet claimed. Next owner: same reviewer’s named recheck through orchestrator.

Orchestrator additionally confirmed authorized unrelated planner PRODUCT_REQUIREMENTS section 9 and reference-register Catalogue/ownership decision edits; preserved, excluded from AU09 patch, and not implemented or claimed as AU09. No unexplained non-owned source drift remains.
