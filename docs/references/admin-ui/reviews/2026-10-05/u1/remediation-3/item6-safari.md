# Round 3 item 6 — stable Save DOM under Safari pointer

Authority: brief55 item6 / V55 and user's focused-field trigger. Identity painting now changes text, title, attributes, disabled/hidden state and classes only when the value differs. Validation also retains unchanged text slots/summary markup. Text fields are painted on input; their subsequent blur/change does not repaint the Save bar. Timezone change still processes its option rules and paints.

New `identity-pointer-save.browser.js` runs a controlled local HTTP fixture with native multipart POST and 303 navigation. For both the first save and a save after success it edits the focused Name field, proves change produces zero Save-subtree mutations, clicks Save once, and asserts exactly one additional POST, the exact name/version, Identity route, and returned Save focus. **PASS Chromium and WebKit**, `/private/tmp/bingo-u1-r3-item6-{chromium,webkit}.log`.

Full existing JS runner **43 passed / 0 failed**, `/private/tmp/bingo-u1-r3-item6-full-js.log` and ignored `artifacts/js-tests/results.json`. No prior assertions changed. Diff and frozen CSS byte checks passed. This JS-only change did not rerun .NET; item4/5 persistence evidence remains applicable.

This is implementer behavior proof, not manual Safari acceptance. Item7 must still exercise actual served fingerprinted layout and commit the rendered reference comparison, baseline failure, screenshot pairs, CSS class proof and dual-browser runner/CI. Final clean Release and user/Claude whole .NET gate remain pending.
