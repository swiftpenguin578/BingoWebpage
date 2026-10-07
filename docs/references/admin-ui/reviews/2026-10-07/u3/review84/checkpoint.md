# U3 review84 remediation and T2 integration checkpoint — 7 October 2026

U3 findings are implemented and scoped-checked. T2 is merged. The final combined gate is pending the concrete Catalogue conformance boundary below. This is implementer evidence, not an independent recheck or user acceptance.

## Commit identities

| Finding | Commit | Result |
|---|---|---|
| A-M2 source-localizer Danish audit | `fd1233df` | Required Schedule red baseline BEFORE A-M1; actual Razor/model/partial/JS-payload ResourceNotFound recorded |
| A-M1 Danish Schedule | `a067c726` |46 keys, dynamic refusals, F4 field error, title double-localization and F4 register corrected |
| A-L1 unknown-timezone locks | `5d1bb164` | All five fields explain timezone restriction EN/DA; no fictitious passed-time/bare Locked reason |
| A-L2 registered body blocks | `da2b617a` | Missing loading OR loaded blocks fail, never filtered away |
| A-L3 displayTimezone | `5637236d` | Immutable baseline/current readback retains stored ID and explicit display timezone |
| B-M1 definite refusals | `1d683ffa` | Server reason, current read/readonly, entries retained, no refused-add replay; message-span test followup `4606a43b` |
| B-M2 Settings parity | `c24763c3` | Reference consequences/wording/icons/states, Danish, extended Settings checklist; resource followups `20e2a6e9`, `9b975d27` |
| B-L1 absent imported form | `1af81e56` | Absence guard survives reopening/finalizing; no invented history; Q11 register updated |
| B-L2 changed counts | `de34106a` | Both old/new answer and release counts, including releases-only changes |
| B-L3 lost-save notice | `883c45fa` | Only that save's submitted display values; frozen attempts retain notice values |
| Accepted Events assertion fix | `b9c9d487` | Already localized phase labels are rendered once; brief80 item0 authority |
| T2 no-ff merge | `c30e39ea` | Exact reviewed `73df5e511e303733e4256302656aac14d9c9a0da`; only expected register conflict |

The merge retains T2's Catalogue bound rows and U3's localized F4 row, all Schedule/Signup/Accounts/Audit/Catalogue partials and shell routes, and both scenario sets. A post-resolution verification initially expected literal Schedule/Signup return statements; inspection confirms the unchanged generic `path.split('/').at(-2)` routes those GUID pages. No route was lost.

## Scoped execution

- Real PostgreSQL Release filter `FullyQualifiedName~ImportedArchivedWithoutFormIsReadOnlyAndExplicitWithoutCreatingHistory`: **3 passed /0 failed /0 skipped**, Archived/AwaitingFinalReview/Finalized. GET and Current200, explicit absence/readonly, refused add and no form/question/audit rows.
- [U3 conformance results](u3-conformance-results.json): **45 cases per engine**, all seven prior registrations at390/494/860/1280/1440, with Dashboard's three text presentations. Initial Events double-localization failure corrected separately; all five Events widths rerun in both engines, other40 passes reused. Source ResourceNotFound and missing-block assertions remain enabled.
- [Affected browser results](scoped-browser-results.json): **9 files passed in each engine**: Schedule10 groups; Signup15 groups; Schedule Danish real F4/confirmation/session list; EN/DA displayed counts/date formats; unknown-timezone EN/DA navigation; shared fetch classifier; Signup11 refused save paths; Signup11 session-loss paths; Settings18 EN/DA reference cases. B-M1's old whole-banner locator initially timed out after B-M2 restored SVG markup; exact message-span locator passed both reruns.
- Source/transport: missing registered blocks **38 rejection cases** after Catalogue registration; Schedule Danish **46 keys**; real GET precision/version/unknown-timezone readback **PASS**.
- Explicit Release parity-fixture rebuild after U3 corrections and again after T2 merge: **0 warnings/errors**. The case-colliding Confirmed key was corrected to SignupSetup.Confirmed; no warning suppression.
- Shared reference/app components CSS `cmp`: **PASS**. `git diff --check`: **PASS**.
- Full JS runner / Release solution final gate: **NOT RUN** yet on this unresolved merged candidate. Whole.NET: **NOT RUN**, planner owns it.

[Complete form/drawer and extended Settings parity checklist](../second-look/signup-parity-checklist.md). Current register changes in this round: F4 Danish binding and Q11 absence after reopening; all prior Q6/Q7/Q9/Q10/Q12/Q13/Q14 and protected outcome rows retained. Catalogue bound statuses came from the reviewed lane.

## Concrete Catalogue boundary

[Both-engine bounded diagnostic](catalogue-integration-diagnostic.json). No Catalogue production files were changed after merge.

1. **Harness source failure, both engines:** `checkSources` at scripts/lib/admin-page-conformance-checks.cjs:153 scans the entire module for250ms timers. Catalogue's directory URL debounce is at admin-catalogue.js:104; its actual POST helper at:175 uses `ui.busy`. This is the same save-versus-search distinction already ruled for Accounts. The assertion has not been bypassed or narrowed silently.
2. **Q7 loading placeholder failure, both engines:** `_AdminCatalogueLoadStates.cshtml:9` uses two `<b class="tnum ct-sk-count">` wrappers. The unchanged assertion requires the existing shared `.tab-count[data-pending-count] > .sk` pattern and finds **0 versus2**. The registration declares two numeric items and one fixed-text item; the generic optional `numberItems` declaration enforces each numeric position, not a page exception.
3. **Q6 phone reservation mismatch:** at390, loading summary is **43.125px**, line height19.575px, versus two reserved lines **39.15px**. The existing4px flex row gap accounts for the excess. Loaded geometry must remain natural. This measurement is diagnostic; five-width Catalogue geometry has not passed because the earlier checks stop the runner.
4. Danish actual render/localizer audit: **no missing resources** in both engines. The first diagnostic attempt tried the hidden desktop language control atphone width; the bounded continuation switches to1280 for that separate read and passes.

**Recommended next action:** authorize/route one bounded Catalogue integration correction: apply save-only timing verification to its single POST helper while retaining the search debounce; replace its two numeric loading wrappers with the shared count pattern; make the loading summary reserve exactly Q6's two phone/one wide lines (e.g. no row gap in loading only), preserving loaded geometry and all fixed text. Rerun Catalogue at all five widths in both engines. Do not weaken assertions or add a page exception. The user may route the T-owned partial/CSS correction to lane T or explicitly authorize this implementer.

Authorized unaffected followups are complete: one-entry Catalogue registration, mixed fixed/numeric declaration, and current Catalogue source/host references in UI_PAGE_MATRIX/UI_SYSTEM. No visual acceptance inferred. No new review environment links are claimed: refresh and final full gate remain pending this boundary, preserving the final-run-on-complete-head requirement and existing ownership markers.
