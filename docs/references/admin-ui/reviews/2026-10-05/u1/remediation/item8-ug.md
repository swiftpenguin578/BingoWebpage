# Item8 — U-G Danish events

New shell/Identity uses event/events wording, including Events, Alle events,
field labels, schedule preview labels, descriptions and notices. Nineteen scoped
AdminDesign.* resource keys have neutral English values and Danish overrides.
Only new-shell/Identity callers use them; existing shared Danish keys and old-page
wording remain unchanged. This uses the existing .NET resource/localizer mechanism.

No existing test expectation changed. Added coverage verifies every scoped key's
exact English fallback and Danish terminology, plus unchanged legacy Events/Event
translations. Controlled HTTP proves Danish shell/Identity output, no exposed
resource keys, and the old Events page retaining its prior wording/layout. Existing
English bound-layout assertions also pass.

Executed localization **2 / 0 / 0**, controlled PG/HTTP **2 / 0 / 0**. Logs:
`/private/tmp/bingo-u1-remediation-item8-{localization,http}.log`.
`git diff --check` clean. Named recheck and visual acceptance remain pending.
