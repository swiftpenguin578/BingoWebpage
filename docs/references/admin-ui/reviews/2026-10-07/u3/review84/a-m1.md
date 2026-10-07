# A-M1 — Danish Schedule completeness

Added all missing Schedule field-label and reachable fixed refusal resources, including F4/AU20 coverage, generated per-boundary refusals and lifecycle errors. Dynamic overlap/capacity refusals localize a template while preserving their supplied values. Existing hints, reasons, picker/aria and JS payloads reuse those resources. Shared layout/topbar preserve already-localized titles instead of looking up Danish text as a key.

The executable F4 test exposed a directly related existing display defect: server errors targeted the picker's typing-error node and were cleared by picker rendering. Server validation now targets its existing named error element, preserving picker errors independently. No server rules changed.

Checks: Release parity fixture build 0 warnings/errors; source coverage 46 label/refusal keys; Schedule five-width conformance including ResourceNotFound audit passes; focused Chromium browser confirms four labels, aria, actual Danish F4 field refusal, confirmation row and session-lost draft labels. Both-engine affected groups run at remediation completion. F4 register corrected. Prior A-M2 red evidence retained.
