# Round2 item3 — theme, refresh failure, Danish grammar

refreshContext reapplies the existing theme owner after replacing the topbar.
Identity catches/logs a context-refresh exception after a successful response;
the newly initialized saved editor and success toast remain usable. New Danish
AdminDesign keys use neuter event grammar (offentligt, det/dets), without changing
legacy keys.

Tests add only: actual shipped theme script/buttons in the controlled fixture;
dark selected class and both aria-pressed values after saved-context replacement;
an actual missing-topbar response causing refreshContext to throw, with one logged
warning, Saved toast, enabled editor/non-inert main and a subsequent editable draft.
Localized copy assertions require the exact corrected phrases. Existing safety
assertions remain unchanged. Authority brief53 item3 / 52a R16 / 52b N1,N5.

Identity binding JS PASS; localization 2 passed / 0 failed / 0 skipped.
Logs `/private/tmp/bingo-u1-r2-item3-{binding,localization}.log`. Diff check clean.
