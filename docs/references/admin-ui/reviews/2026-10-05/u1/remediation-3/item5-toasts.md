# Round 3 item 5 — reference toasts and phone clearance

Authority: brief55 item5 / V37–V39, user Q5. Item1 already supplied the shared reference icon/text/close partial and Dismiss notification label. Successful Identity save now uses exact “Identity saved.” with a scoped Danish translation; no-change/audit semantics are unchanged.

Toasts retain the reference maximum of three, 4500ms ordinary / 7000ms action lifetimes. Hover does not pause expiry. Close and action start the is-leaving animation before removal; actions dismiss once. On phone widths the host sits above the visible sticky form bar with 12px clearance; resize, scroll, bar resize and draft painting update its position. Desktop keeps the reference 20px bottom offset.

Executed: focused complete shell script **PASS Chromium and WebKit**, `/private/tmp/bingo-u1-r3-item5-{chromium,webkit}.log`; controlled PostgreSQL/HTTP save/no-change/uncertain checks **2/0/0**, `/private/tmp/bingo-u1-r3-item5-http.log` and `-http-trx/`; Danish coverage **2/0/0**, `/private/tmp/bingo-u1-r3-item5-localization.log`. Diff and frozen CSS byte comparisons passed.

Exact assertion change: `admin-design-shell.browser.js` old hover-at-5000ms leaves one toast + mouseleave expiry → reference exact 4499ms leaves three / 4500ms leaves zero despite hover (V38). Existing 6999/7000ms action assertions retained. Added explicit leaving class/exit removal and a 390×844 actual styled Save-bar fixture: elementFromPoint identifies Save, toast bottom is above bar top, click counter equals one (V39). The fake clock remains paused; all busy/disposal assertions unchanged. `AdminDesignIdentityIntegrationTests.cs` adds exact successful-save message and its absence after uncertain commit; existing PRG, persistence and audit assertions retained. No earlier assertion deleted.

These are implementer checks. The phone fixture is supplemental; actual served-app screenshot/parity coverage is item7. Full dual-browser runner and final Release/.NET/review/visual gates remain pending; whole .NET is user/Claude execution.
