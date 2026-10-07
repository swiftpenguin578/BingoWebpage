# SR2 — remove redundant identity English resource entries

Planner ruling after the7269a198 boundary: do not prefix keys; remove exactly the80 unprefixed identity-value entries introduced by dcc1a40f from SharedResource.resx only. [Provenance/count/key inventory](sr2-provenance.json) verifies all80 were introduced by that commit and every value equals its key. All other English entries preserved. SharedResource.da.resx is byte-identical; no key/lookups, JS payloads, markup or displayed text changed. English uses the existing literal-key fallback; current Danish remains “Tider vises i {0}; skift den på”.

Checks:

- Full Release AdminDesignLocalizationTests: **3 passed /0 failed /0 skipped**, exit0; [exact TRX summary](sr2.json).
- Rebuilt Release AdminDesignParityFixture before browser checks:0 warnings/errors,2.64s. No stale embedded resources.
- Schedule conformance at390/494/860/1280/1440: **5/5 Chromium** (35.746s), **5/5 WebKit** (33.930s). Existing English rendering, frame/style/geometry/update/document and Danish-resource checks passed; no missing-text failure or assertion changes.
- Existing Schedule browser file: **10 interaction groups per engine**, Chromium26.557s, WebKit27.726s, both exit0. Includes picker/DST, exact save/readback/stale/session outcomes and English/Danish linked summary at all five widths.
- `git diff --check`: exit0. [Browser commands/timings/compact geometry](sr2-browser-results.json).

Only affected Release test binaries and the parity fixture rebuilt. No fullJS/whole.NET repetition, flake repair, environment refresh or acceptance change. Prior SR1/3/4/5 checks remain applicable. This resolves the earlier sr2-boundary.json failure; it remains historical evidence.
