# A-M2 — real Danish localization boundary

The parity host decorates IStringLocalizerFactory and observes ResourceNotFound under Danish for actual rendered resource lookups, including dynamic labels, model calls and JavaScript label payloads. A test-only response header supplies the missing resource names/keys to the generic check; every registered page must provide it and report zero misses. Existing source/runtime checks remain.

Before A-M1 translation changes, Release parity build passed and scoped Chromium Schedule conformance **failed as required**: Signup opening, Signup closing, Event start, Event end, and the already-translated title Tidsplan incorrectly re-localized by the layout. See a-m2-before-translations.log. This red baseline is intentional evidence; translation corrections and the final all-page pass follow separately. No production translations were changed in this commit.
