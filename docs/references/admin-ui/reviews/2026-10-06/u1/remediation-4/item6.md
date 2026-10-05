# Round4 item6 — language animation

Authority: brief60 item6 /59 N5. Before importing a translated page fragment, remove its page-local fade-in entrance classes. Ordinary navigation/full-load entrance animation is unchanged. The existing language cross-fade still targets sidebar/topbar/page region and uses the theme duration; reduced motion adds none.

Extended the existing actual Kestrel served-language scenario (no replacement checker): observe animationstart names and computed durations while retaining dirty guards, exact POST counts, document/sidebar retention and language assertions. Both Chromium/WebKit emit exactly3 `adminLanguageFade` animations, each0.2s equal to `--dk-dur-theme:.2s`; no fadeIn. Switching back under reduced motion emits an empty animation list. **4 checks passed /0 failed** across both engines. The first new test compared seconds to milliseconds incorrectly; corrected unit conversion retains exact equality, no tolerance. The final raw results are preserved with the evidence. No previous assertion changed or removed.

Latest whole BrowserTests150/0/0; final batch will rerun it with full JS, parity and Release. Diff clean; manual acceptance pending.
