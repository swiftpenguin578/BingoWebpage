# B-L2 — compare both confirmation counts

Changed-impact confirmation now names old and new answer counts and event-registration release counts, in English and Danish. The server's changed impact/version and explicit reconfirmation remain required.

Release parity fixture build PASS,0 warnings/errors. Chromium `admin-design-signup-setup.browser.js` PASS15 interaction groups; expanded confirmation case first changes both counts, then changes only releases (answers stay3, releases2→4), and asserts both old/new pairs before cancellation. `git diff --check` PASS.
