# A-L1 — unknown-timezone lock reasons

Unknown-timezone Schedule fields now explain that Identity must select a supported timezone, before evaluating lifecycle lock reasons. Unset draft fields no longer claim a time has passed; no field falls through to a bare Locked label.

Validation: Release parity fixture build passed (0 warnings/errors). `admin-design-unknown-timezone.browser.js` passed in Chromium for English and Danish, including all five lock reasons, explicit UTC display, unchanged stored timezone/version/instants, no toast, and subsequent navigation. Both-engine combined scoped validation follows with the remaining review findings.
