# Identity old routes and handlers

| Owner | Disposition |
| --- | --- |
| /Admin/Events/Identity/{id:guid} GET | Kept; opts into reference shell. Cancelled/Finalized/Archived now read-only200 (D17). Hidden/discarded404 retained. |
| ?handler=Current GET | Kept authorized no-store read; JSON extends values with persisted version (A14), same visibility boundary. |
| Identity POST | Kept baseline/merge/timezone/transaction/audit owner. Success/no-change PRG target changes Manage→Identity. D16 refusal to Manage stays refusal, never success; hidden404 stays. |
| Old Cancel links→Manage | Retired from form; shared navigation guard owns departure. |
| Status/timeline/public-pages rail | Retired from Identity. Overview retains existing links. Permanent absolute /Events/{slug}/Signups copy remains here. |
| event-manage.js on Identity | Retired inclusion. |
| Legacy event-identity.js global/IIFE protocol | Retired. Same asset filename is now an ES module with init/dispose using AdminUI/AdminFetch. |
| New routes/handlers/tables/migrations | None. |
