# U1 item6 — Danish coverage

Added missing Danish strings for the new shell, navigation, theme, loading, draft guard and session-loss notice. Language abbreviations and brand initials also pass through the localizer. Existing lowercase `board` resource is reused with display capitalization (its established Danish value is Board); no duplicate Board resource or old-page translation change was kept.

`AdminDesignLocalizationTests` checks all new-layout/partial literals, the new shell event projection and shared lifecycle presentation. It automatically discovers page models marked AdminDesignAttribute and includes their markup and code-behind, so Identity must add Danish entries as part of item7. It checks localizer/Localize/title literal keys for nonempty Danish values; runtime entity/user data is not a translation key. New JS visible text comes from these localized data attributes.

Proof: localization test plus unchanged shell/toast tests **5 passed /0 failed /0 skipped**, `/private/tmp/bingo-u1-item6/item6-localization.trx`. Release compilation clean after correcting a new resource-case collision and test analyzer syntax. Full JS gate **40 files passed /0 failed /40 total**, exit0; [results](item6-js-results.json). The final translation-key markup correction did not change JS. Diff clean. Existing tests unchanged.

User still spot-checks Danish at visual acceptance. No production page is bound; item7 adds Identity's strings and is subject to this test. Independent review and whole .NET final gate (user/Claude) remain pending. CI has not run; optional setup-node pin note remains carried.
