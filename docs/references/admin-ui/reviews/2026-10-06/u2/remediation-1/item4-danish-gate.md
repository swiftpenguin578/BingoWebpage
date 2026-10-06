# U2 brief70 remediation item4 — Danish coverage

Changed: `tests/Bingo.BrowserTests/AdminDesignLocalizationTests.cs`,
`src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml`,
`src/Bingo.Web/Resources/AdminCommunityResource.da.resx`, this evidence.

The existing SharedResource/legacy assertions remain. Added community resource
nonempty validation, all opted-in pages/models and community-resource partials,
including Dashboard/Events load templates, pending table and Create template.
The scanner includes D[...] and L(...), conditional key branches and current
dynamic labels/views/columns/empty-state/phase sources. A bounded lexical
first-key scan excludes condition constants, nested format arguments and dynamic
ViewData indexing; it does not mistake CSS/URL/date-format strings for keys.
Detected one real uncovered loading aria key: “Loading statistics” was using
SharedResource with no Danish entry. Bind it to the community resource and add
“Indlæser statistik”. No product wording changed.

Executed:

- Release focused `AdminDesignLocalizationTests`: 2 passed,0 failed,0 skipped;
  baseline TRX `tests/Bingo.BrowserTests/TestResults/u2-remediation-item4-baseline.trx`.
- Temporarily removed only the Danish entry “Participation by event appears once
  a second event has been held.”, using apply_patch, then `--no-build` gate (the
  test reads source XML): **0 passed,1 failed,0 skipped**,48ms, exact missing key
  reported from Index.cshtml. TRX
  `tests/Bingo.BrowserTests/TestResults/u2-remediation-item4-mutant.trx`.
- Restored the original entry at its original position before staging. Final
  full focused gate **2 passed,0 failed,0 skipped**; TRX
  `tests/Bingo.BrowserTests/TestResults/u2-remediation-item4-final.trx`.
- Initial test-authoring checkpoints exposed regex escaping/false keys and a
  malformed quote expression; corrected to the bounded lexical scanner before
  mutation proof. No production defect was inferred from those test defects.
- `git diff --check`: exit0. Mutant was never staged or committed.

Implementer checks only. Dashboard/Events remain awaiting Claude review, then
user visual acceptance. No independent/self-review or publication.
