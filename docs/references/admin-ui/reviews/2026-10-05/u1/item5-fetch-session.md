# U1 item5 — C-CMP-2 request boundary

`AdminFetch.request` sends same-origin credentials, X-Requested-With, JSON Accept when requested and the shell antiforgery header on writes. Outcomes are handler/session-lost/refused/unknown. Login, AccessDenied and ChangePassword redirects or204 navigation are session loss; HTML instead of expected JSON is also session loss. Manage redirects are refusals, never success. Unrecognized redirects, failed HTTP/network and malformed payloads are unknown and never retried. An explicitly allowed own-page PRG destination is returned as handler data for the page to validate, not as a save-success assertion.

Session notice is a blocking shared confirmation: localizes not-saved wording, displays the page-supplied entered values as text, keeps underlying inputs intact and offers Keep editing or an explicit sign-in link. It preserves accessChanged and ReturnUrl. There is no automatic navigation, retry or resend. New layout loads this helper and does not load site.js. Identity uses it when item7 binds the page.

## Proof

- Four real PostgreSQL/HTTP cases: Identity POST and Current GET, each with absent session and with a signed-in account subsequently disabled: **4 passed /0 failed /0 skipped**. Each returns302 to Login, disabled case carries accessChanged, response has no data, event name remains unchanged and no identity audit entry is written. `/private/tmp/bingo-u1-item5/item5-session.trx`.
- New browser test proves all response classes, headers/cookie/token, explicit own-page PRG classification, text-safe retained values, inert background notice, and no automatic retry/navigation. Redirect-follow metadata uses controlled Response doubles because Playwright route interception does not cover the redirect's subsequent network hop; the actual302 server boundary is tested above.204 navigation and ordinary requests use routed browser fetch. Initial fixture failure was a fetch failure at that unrouted hop; no production assertion was weakened.
- Full JS gate **40 files passed /0 failed /40 total**, exit0; [results](item5-js-results.json). Controlled stale fixtures reused.
- Release Web/integration build succeeded in the HTTP run with no warnings/errors; syntax/diff checks clean. Existing assertions unchanged; item3 test fixture class made partial to share its controlled database fixture.

Item6 Danish coverage next. No production page bound yet. Full .NET final gate remains user/Claude execution; independent review, visual acceptance and CI remain unexecuted here.
