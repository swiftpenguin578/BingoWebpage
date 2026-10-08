# U3 Signup setup early look and T1 final look

7 October2026. Review environment refreshed after the approved T1 merge. All61 listed URLs below returned200 at their exact URL using the indicated synthetic account. These supersede URLs from earlier U3 checkpoints. All review accounts use the local-only password `ReviewOnly!1234`.

## Commits and scope

- Signup setup first working binding: `3ebd92ce0ae2eeb8736c95b044ba592596a0933e`.
- Approved T1 merge: `4b94de92082910b1adff3a2de4094fb4a4492f11`, second parent verified `d03fc7a24fe1fd24953ec5202f42d14cf2e1a30d`.
- This report accompanies the separate Accounts/Audit conformance registration commit.
- Only conflict was `_AdminDesignTemplates.cshtml`; retained all Schedule, SignupSetup, Accounts and Audit partial lines. Automatic merges retained both sets of register rows, shell routes and scenarios. No local changes to the A10 lines in `check-u2-ur.cjs`; accepted T1 version retained. Seeder includes `UiReviewAccount.Id`.
- No laneT page-code remediation; Accounts/Audit manual-acceptance rows unchanged. No item4, fullJS, whole.NET or independent review by this worker. User supplied acceptance of prior T1 independent review/planner2119-pass evidence; not rerun here.

## Checks and remaining conformance failures

- Q11 focused PostgreSQL HTTP test1/1 PASS. Exact absent-form EN/DA message, read-only stored Settings, explicit `hasForm:false`/null version/empty questions, no invented form or history; terminal POST refused. Both live engines passed rendering and normal-form drawer smoke.
- Reused applicable item3 evidence:107 scoped PG cases,30 source cases,11 browser groups and5 conformance widths per engine. No redundant broad reruns. New Q11 case is additional.
- `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS0 warnings/errors. Separate Release parity fixture prerequisite also PASS0/0. Review refresh performed its normal incremental Release build, not another test gate.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~AccountsUiTests|FullyQualifiedName~AuditUiTests"`: PASS17/17.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~AccountsHttpIntegrationTests|FullyQualifiedName~AuditHistoryIntegrationTests"`: PASS13/13.
- For each of Accounts/Audit and Chromium/WebKit: `BINGO_CONFORMANCE_PAGES=<accounts|audit> PLAYWRIGHT_BROWSER=<chromium|webkit> NODE scripts/check-admin-page-conformance.cjs`: FAIL at source shared-visual-token assertion. Accounts `admin-design-accounts.css:61` has `border-radius:50%`; Audit `admin-design-audit.css:40` has `border-radius:9px`. These geometries also appear in frozen Accounts.dc.html:187 / Audit.dc.html:181. The existing generic assertion rejects literal radii. No assertion bypass or T1 page change made. Browser geometry, count/fixed summary, updates and Danish conformance stages were not reached; no conformance pass claimed.
- Recommendation for planner/T1 owner: reconcile these exact literal-radius failures with the shared-token rule, preferably using shape-equivalent shared tokens, then rerun only these two pages in both engines. If retaining literal reference shapes is intended, the generic source-rule decision belongs to the planner; this worker has not weakened it.
- Unreached source observation for that continuation: Accounts loading counts use `.ac-sk-count > .sk` inside `<b>`, while the existing generic count declaration requires Events `.tab-count[data-pending-count] > .sk` and no `<b>`. This is not an executed second failure; count-summary stage remains gated by the source assertion.
- Registration declares Accounts count words in EN/DA; Audit fixed text/offset in EN/DA (T1 reviewL2). Shared helper now accepts an explicit fixed-summary declaration without replacing the Q6 height assertion. Selected-family scope now limits source checks as well as browser checks. Audit update probe uses actor typing, since opening More filters intentionally moves focus.
- Syntax and `git diff --check`: PASS. No fixture changes needed during registration. No failed conformance command repeated without change; the four runs are the four requested page/engine cases.

## Decisions and evidence

- U3-Q9/Q10: exact shortened linked summary, Danish, no reservation override; differences from SignupSetup.dc.html:127/:1551 registered.
- U3-Q11(a): imported absent form state registered against reference:519–520; no D17 exception, no historical writes.
- C-CMP-2/AU05/AU06 transport row retained; A-SignupSetup-1 cap boundary and all T1 rows retained.
- `signup-t1-early-look-checks.json`: compact output excerpts, SHA256s of logs, exact live links/results and both-engine smoke outcomes. Previous detailed item3 evidence remains in `signup-item3-checks.json`.
- Screenshots: `signup-imported-1280.png`, `accounts-1280.png`, `audit-1280.png`; prior clean Signup setup Settings/Form/Drawer screenshots remain beside this file. Synthetic data only.
- Environment remains owned and running. Both `artifacts/ui-review/owner.json` and preserved `owner.json.stale-20261007` exist. Do not hand it to laneT without instruction.

## Stop boundary

**Stopped for user look.** Signup setup early look and Accounts/Audit final T1 look are available. Conformance remains unresolved as above. No item4 or further work until the user gives the next instruction. No push/merge beyond the one explicitly authorized local T1 merge, or deployment.

## Verified live links

### Schedule / Signup setup

- [Private setup — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/63384ba4-0541-4b49-a23b-485a901efac2) — ReviewAdmin
- [Private setup — signup settings](http://127.0.0.1:5310/Admin/Events/SignupSetup/63384ba4-0541-4b49-a23b-485a901efac2) — ReviewAdmin
- [Signups open — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/db92c2dc-833d-449f-8705-d6ea97b78771) — ReviewAdmin
- [Signups open — signup settings](http://127.0.0.1:5310/Admin/Events/SignupSetup/db92c2dc-833d-449f-8705-d6ea97b78771) — ReviewAdmin
- [Signups closed — finalized affiliated rosters — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/a72e92ed-98c3-40d3-bc0b-239adfe33fb8) — ReviewAdmin
- [Signups closed — finalized affiliated rosters — signup settings](http://127.0.0.1:5310/Admin/Events/SignupSetup/a72e92ed-98c3-40d3-bc0b-239adfe33fb8) — ReviewAdmin
### Schedule

- [Archived — affiliated roster history [Archived] — end-only or read-only schedule](http://127.0.0.1:5310/Admin/Events/Schedule/5dcf69e0-a1d3-498f-b322-f8013833f121) — ReviewAdmin
- [Archived — WOM end could not update [Archived] — end-only or read-only schedule](http://127.0.0.1:5310/Admin/Events/Schedule/8d51438c-23c0-4321-9e6e-050588bd9566) — ReviewAdmin
- [Imported — frozen synthetic history [Archived] — end-only or read-only schedule](http://127.0.0.1:5310/Admin/Events/Schedule/5c1bedb7-9b0c-4d73-b8ce-80249b7e5087) — ReviewAdmin
- [Cancelled with signup history [Cancelled] — end-only or read-only schedule](http://127.0.0.1:5310/Admin/Events/Schedule/15930077-91ac-401f-adb3-c9c26eadb989) — ReviewAdmin
- [Live — published board correction [Live] — end-only or read-only schedule](http://127.0.0.1:5310/Admin/Events/Schedule/41f17ec7-895a-4f9d-9a5f-d96c118c6d1f) — ReviewAdmin
### Signup setup

- [Private setup [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/63384ba4-0541-4b49-a23b-485a901efac2?tab=form) — ReviewAdmin
- [Signups open [SignupOpen] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/db92c2dc-833d-449f-8705-d6ea97b78771?tab=form) — ReviewAdmin
- [Signups closed — finalized affiliated rosters [SignupClosed] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/a72e92ed-98c3-40d3-bc0b-239adfe33fb8?tab=form) — ReviewAdmin
- [Unknown timezone [SignupClosed] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/0c6e7e6f-eb15-4050-b7d5-6edc67e63e7f?tab=form) — ReviewAdmin
- [Upcoming setup 01 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/38eb26e5-2dd2-43db-a9ab-d2a40d2ecece?tab=form) — ReviewAdmin
- [Upcoming setup 02 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/3f504ad0-b65c-4a61-ac5f-f9cc37f38dfe?tab=form) — ReviewAdmin
- [Upcoming setup 03 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/7d67abae-c2f1-48ea-abb6-ae4245bb148d?tab=form) — ReviewAdmin
- [Upcoming setup 04 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/a7cd0448-5895-4212-a246-adb7f122f2fe?tab=form) — ReviewAdmin
- [Upcoming setup 05 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/25fe082d-9e25-480d-aa4a-771d13475f75?tab=form) — ReviewAdmin
- [Upcoming setup 06 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/2d218a58-935f-4131-bb9f-2e4667939e2a?tab=form) — ReviewAdmin
- [Upcoming setup 07 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/af40c1a1-ab8c-48e4-bf2a-219e96c82fc0?tab=form) — ReviewAdmin
- [Upcoming setup 08 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/00a0c8b2-2640-4fd1-b130-dd2da820ea9a?tab=form) — ReviewAdmin
- [Upcoming setup 09 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/134fb72b-4405-4ff4-a865-4c62f338ed43?tab=form) — ReviewAdmin
- [alpha [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/8d08a773-4add-42a1-82d2-f1e3142208f8?tab=form) — ReviewAdmin
- [Alpha [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/4f9b009a-5e23-406f-9bd5-15a8a688ca06?tab=form) — ReviewAdmin
- [Ægir [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/93a67270-257a-4f6a-9e62-b18007561a04?tab=form) — ReviewAdmin
- [Ørn [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/6833f907-632b-4d85-bc6d-42e2ab7e899b?tab=form) — ReviewAdmin
- [År [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/73d137b0-89c4-40e5-a944-51cccd3c3bfe?tab=form) — ReviewAdmin
- [No capacity limit [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/dcd6c33e-b7a1-4725-bef4-cb06df2a025d?tab=form) — ReviewAdmin
- [No dates configured [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/06f5d6e6-20b2-4be7-8fab-9563885a37e9?tab=form) — ReviewAdmin
- [Upcoming setup 17 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/767dbc14-14f6-4755-ba7c-b33827493e86?tab=form) — ReviewAdmin
- [Upcoming setup 18 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/484510b8-921d-474d-abde-cc33ce26796c?tab=form) — ReviewAdmin
- [Upcoming setup 19 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/a9074666-5a06-470c-b948-637b988f3ba8?tab=form) — ReviewAdmin
- [Upcoming setup 20 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/524b271a-7b3d-48d2-9465-eda0e7be2ee3?tab=form) — ReviewAdmin
- [Upcoming setup 21 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/5479b95f-d245-41ef-bfd9-9be11e321f00?tab=form) — ReviewAdmin
- [Upcoming setup 22 [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/8c130d8b-cccf-4f27-99bc-6a0f962516f0?tab=form) — ReviewAdmin
- [Signup opening failed [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/8ffac9f2-0e32-40dd-9c3a-bba295869904?tab=form) — ReviewAdmin
- [Archived — affiliated roster history [Archived] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/5dcf69e0-a1d3-498f-b322-f8013833f121?tab=form) — ReviewAdmin
- [Archived — WOM end could not update [Archived] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/8d51438c-23c0-4321-9e6e-050588bd9566?tab=form) — ReviewAdmin
- [Imported — frozen synthetic history [Archived] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/5c1bedb7-9b0c-4d73-b8ce-80249b7e5087?tab=form) — ReviewAdmin
- [Cancelled with signup history [Cancelled] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/15930077-91ac-401f-adb3-c9c26eadb989?tab=form) — ReviewAdmin
- [Live — published board correction [Live] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/41f17ec7-895a-4f9d-9a5f-d96c118c6d1f?tab=form) — ReviewAdmin
### Accounts

- [Plain Admin, Admin role filter — Admin accounts explained, not offered](http://127.0.0.1:5310/Admin/Accounts?role=admin) — ReviewAdmin
- [Plain Admin — the Super Admin account is protected](http://127.0.0.1:5310/Admin/Accounts?account=2434dc4d-ebcf-4ffd-948a-58cfbfcfed3d) — ReviewAdmin
- [Disabled account — notice, reason and Restore](http://127.0.0.1:5310/Admin/Accounts?account=b85a259f-b6c9-4ea1-8990-47e04e28e117) — ReviewAdmin
- [Captain — events, team roles, reset link and Disable](http://127.0.0.1:5310/Admin/Accounts?account=bf4dc330-d55d-4aa7-96fc-71d0cc03be84) — ReviewAdmin
- [Former member — ended team role](http://127.0.0.1:5310/Admin/Accounts?account=263c3ec4-dbd9-490f-9140-60aeff4752f5) — ReviewAdmin
- [Search ignores case](http://127.0.0.1:5310/Admin/Accounts?q=REVIEWSECOND) — ReviewAdmin
- [No accounts match / Clear search and filter](http://127.0.0.1:5310/Admin/Accounts?q=nobody-here&role=superadmin) — ReviewAdmin
- [Link to an account that isn’t available](http://127.0.0.1:5310/Admin/Accounts?account=00000000-0000-0000-0000-000000000001) — ReviewAdmin
### Audit

- [History, newest first — filters, chips and the entry drawer](http://127.0.0.1:5310/Admin/Audit) — ReviewAdmin
- [Participants area (S11) — moved team membership keys](http://127.0.0.1:5310/Admin/Audit?action=participant.) — ReviewAdmin
- [Signups area (S11) — automatic signup opening failures, Automated](http://127.0.0.1:5310/Admin/Audit?action=signup.) — ReviewAdmin
- [Actor search ignores case and a leading @](http://127.0.0.1:5310/Admin/Audit?actor=%40reviewowner) — ReviewAdmin
- [Link with unrecognised filters — notice, the rest applies](http://127.0.0.1:5310/Admin/Audit?type=spaceship&from=2027-13-40&actor=ReviewOwner) — ReviewAdmin
- [Entry link that isn’t available](http://127.0.0.1:5310/Admin/Audit?entry=00000000-0000-0000-0000-000000000001) — ReviewAdmin
### Accounts

- [Directory as the Super Admin — Transfer ownership in the header](http://127.0.0.1:5310/Admin/Accounts) — ReviewOwner
- [Your own drawer as the Super Admin — Ownership row](http://127.0.0.1:5310/Admin/Accounts?account=2434dc4d-ebcf-4ffd-948a-58cfbfcfed3d) — ReviewOwner
- [Admin account drawer as the Super Admin — Revoke Admin](http://127.0.0.1:5310/Admin/Accounts?account=f6186cb0-e1f5-4b7d-b57c-a78d5a06cdfe) — ReviewOwner
### Audit

- [Hidden event history, marked Hidden](http://127.0.0.1:5310/Admin/Audit?event=70cd2889-9509-4e1d-bd2e-daf2d5a595d4) — ReviewOwner

References: [Signup setup](http://127.0.0.1:5320/SignupSetup.dc.html), [Accounts](http://127.0.0.1:5320/Accounts.dc.html), [Audit](http://127.0.0.1:5320/Audit.dc.html).
