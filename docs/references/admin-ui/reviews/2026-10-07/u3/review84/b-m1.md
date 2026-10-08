# B-M1 — definite Signup refusals

The shared classifier retains the reason from the followed Manage response's server-rendered notice. Signup capacity, code, form mutations and drawer saves handle `refused` before uncertain outcomes, read Current, retain entries and disable mutation controls. Refused adds never become replay candidates. A failed read still leaves the known refusal visible and disables editing.

Release parity fixture build: PASS, 0 warnings/errors. Chromium `signup-refusals.browser.js`: PASS all 11 save paths (capacity, code, both account adds, rename, move, co-captain off/on, delete, drawer add/edit). Each asserts the exact distinct server reason, one POST, one Current read, read-only state, retained entries and no Check-form replay. Both engines and affected shared classifier checks follow in scoped validation. No server lifecycle/version/receipt rules changed.

Final affected validation: all11 refusal paths PASS in both Chromium and WebKit. B-M2 restored the banner's separate icon/text elements; the exact server-reason assertion now targets `[data-card-banner-text]`, avoiding SVG formatting whitespace. The initial combined run timed out on the old whole-banner exact-text locator; only this failed test was corrected and rerun. Production refusal behavior remained passing.
