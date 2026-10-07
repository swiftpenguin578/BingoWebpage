# A-L2 — require every registered body block

Geometry now requires every registration key in both loading and loaded measurements before computing shifts. Missing elements cannot be silently filtered out.

`admin-conformance-blocks.source.js`: PASS, 32 independently missing-block cases rejected across all current registrations, with complete positive controls. `git diff --check`: PASS. Full registered runtime conformance follows on the corrected implementation.
