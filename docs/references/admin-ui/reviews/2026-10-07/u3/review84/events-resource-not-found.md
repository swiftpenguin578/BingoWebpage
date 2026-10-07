# Accepted Events page — ResourceNotFound assertion correction

Authority: brief80 item0, “If an accepted page fails: fix it in its own commit, named after the failing assertion, and report it. Do not weaken the assertion.”

The new A-M2 audit failed in both engines because Events rendered already-localized `StateOptions.Label` through the AdminCommunity localizer a second time (eight Danish phrases such as `Tilmelding åben` became missing resource keys). Both menu and selected phase now render the model's localized label directly. Displayed wording and filtering behavior are unchanged; no missing-key exception was added.

Rebuilt Release parity fixture PASS,0 warnings/errors. Events-only conformance PASS5 widths in Chromium and WebKit, including actual Danish localizer audit, update/focus/scroll and loading geometry. Earlier conformance evidence already passed all40 cases for the other six pages per engine; together all45 current registered cases pass. `git diff --check` PASS.
