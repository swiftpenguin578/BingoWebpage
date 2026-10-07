# Current parity addendum — numbered follow-up items1–5

The second-look element checklist remains the baseline. Current changes below supersede only its named rows.

| Reference | Current app | Result |
|---|---|---|
| Schedule117;Signup127 | respective HeaderSummary links | D decision: different registered-page links use shell navigation; same-page query/drawer handlers retain own behavior |
| components695–697/873;Signup373–375/394 | shared .ro-value::after and _AdminDesignFieldLock/_AdminSignupSetupDrawer | REGISTERED U3-Q14: same shared lock glyph in right calendar slot;42px text padding; reason text retained without duplicate icon; stored-code list lock retained |
| Signup29/46/174 | .ss-num max180px wide/no maximum phone, measured390/494/860/1280/1440 | MATCH; input unchanged. Only left column flexes under Q12b |
| Signup179;vmCap | Signup data-confirmed and localized text labels | MATCH: Confirmed / N of M, Danish Bekræftet / N af M |
| Schedule readonly dates/reason notes;Signup1388 | Schedule LocalDate/EventDate/Display and client formatter;Signup Current firstResponseDay | User item5 REGISTERED: non-padded day, localized month, comma and fixed HH:mm; Signup first-response now full timestamp |

No timestamp transport/precision or save behavior changed. Both engines:10 Schedule and15 Signup groups PASS; text probes cover EN/DA five lifecycle states and reference input width atallfive widths. Signup first-response formatting source checked; existing controlled response-note browser probe includes timestamp. Five-width Identity/Schedule/Signup conformance15cases/engine PASS. Build0warnings/errors;cmp/diffcheck PASS.

Item3 remaining T failures both engines: Accounts requires2 tab-style numeric placeholders but has0; Audit390px loading summary43.125px versus39.15px Q6. Old busy-timing/fade failures resolved. No T production edits beyond approved merge; no assertion weakening. Later widths remain gated, not claimed passed.
