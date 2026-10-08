# U8 Review parity checklist (rule 22)

Reference: `docs/references/admin-ui/Review.dc.html` (template `:80-458`, behaviour script `:459-1320`). App: `Pages/Admin/Review/{Index,Details,_ReviewWorkspace}.cshtml`, `Pages/Shared/_AdminReviewLoadStates.cshtml`, `wwwroot/js/admin-review.js`, `wwwroot/css/admin-design-review.css`.

Method: (1) every user-facing literal in the reference template and script was extracted mechanically and searched in the app sources (section A); (2) every control/region was mapped by hand (section B); (3) literals that are not in the app source are classified (section C). `match` = same text/control; `registered` = a row in `DELIVERY_PLAN.md` “Bindings not shown in the design references” (U8 rows at the end of the table). Executed: the extraction and search (Python, read-only). Conformance geometry at 390/494/860/1280/1440 passes in Chromium and WebKit (`BINGO_CONFORMANCE_PAGES=review`). Manual visual acceptance is the user’s.

## B. Controls and regions (reference → app → result)

| Reference | App | Result |
| --- | --- | --- |
| Queue header `:166-172`: h1 “Review”, summary (pending count, phase text, times line) | `Index.cshtml` header + `_AdminReviewLoadStates` skeleton | match; times line is “Times in UTC, with {city} time below” — registered (U8-Q1) |
| Search `:177-183` (`#rv-search`, placeholder, clear button, Escape clears) | `Index.cshtml:47-50` | match |
| Status segmented control + narrow select `:184-190` with counts | `Index.cshtml:52`, `_AdminReviewLoadStates.cshtml:20` | match |
| Table head/rows `:195-222`: Uploaded, Team, Account, Tile, Status, Check; row opens the submission; tags; “No warnings” | `Index.cshtml:79+` | match; Uploaded leads with UTC (registered, U8-Q1); per-row warnings from the bounded projection (registered) |
| Table foot “N of M submissions” `:200` | `Index.cshtml:102` | match |
| Queue states: loading skeleton, error + Try again, empty (no submissions yet / event not Live), no match + Clear, hidden/unknown event | `Index.cshtml`, `_AdminReviewLoadStates.cshtml` | match; hidden/unknown/no-event states and terminal empty text registered |
| Workspace subhead `:208-224`: Back, title (tile), status badge, Previous/position/Next | `Details.cshtml:19-40` | match; neighbours follow the list as opened (registered) |
| Workspace loading / error / missing `:226-232` | `Details.cshtml`, `_AdminReviewLoadStates.cshtml` | match |
| Viewer bar `:238-250`: zoom out/percent/in, Fit, 100%, hint, Original | `_ReviewWorkspace.cshtml:73-81` | match; ported inline viewer — registered (U8-E1); Original opens `/Evidence/{assetId}` |
| Viewer stage states: image, loading, error + retry, missing | `admin-review.js:431+`, `_ReviewWorkspace.cshtml` | match |
| Notice banner `:259-265` (uncertain with Check status; others with Dismiss) | `admin-review.js` banner() | match (Dismiss added in this item); wording registered |
| Warnings list `:267-273`: missing screenshot, after end, paused, left team, same image (+links) | `_ReviewWorkspace.cshtml:105-113` | match; wording and S9 text registered |
| Correct details card `:275-292`: Tile, Drop, Credited account, weight line, reason with count, hint, inline error | `admin-review.js:155+`, `_ReviewWorkspace.cshtml` correction section | registered: field label “Objective” and per-objective options (U8-E2); accounts picker with Released/Left team/Current markers (AU17a) |
| Compare with the screenshot card `:294-305`: Account, Drop, Objective, Code (if enabled), Uploaded, Event window, Contribution; submitter’s note; reviewer feedback | `_ReviewWorkspace.cshtml:151-165` | match; Account sub-line “active account since / switched from” dropped, Uploaded sub-line uses the event timezone, Contribution wording incl. cap-limited and blocked — all registered |
| History / Earlier approvals / Files disclosures `:308-322` | `_ReviewWorkspace.cshtml:169-195` | match; History wording from the stored presenter — registered (U8-E4) |
| Decision card `:325-358`: error banner, closed text, result + Next/Back, Approve, Reject…, Correct details… | `_ReviewWorkspace.cshtml:205-240` | match; closed-state texts registered; no card shadow — registered (U8-E3) |
| Reject panel `:332-340`: Reason for rejecting, count, hint, Reject submission | `_ReviewWorkspace.cshtml:224-232` | match; submit is the BR-4 confirmation — registered |
| Correct panel footer `:342-346`: note, form error, Save correction | `_ReviewWorkspace.cshtml:234` | match; no-op refusal text bound server-side — registered |
| Approved panel `:347-350`: approved-by text, Reverse approval… | `_ReviewWorkspace.cshtml:240` | match |
| Reverse dialog `:384-408`: title, body, points, Reason for reversing, Cancel/Reverse | `_ReviewWorkspace.cshtml:245-260` | match; “Required. Kept in the history.” kept; real reallocation text |
| Discard dialog `:415-428` (Keep editing / Discard) and uncertain-leave dialog (Leave anyway / Check status) | shared `admin-design-shell.js` discard dialog; leave dialog in `_ReviewWorkspace.cshtml:64` | match (shared dialog); backdrop/Escape never leaves (decision B) |
| Toasts `:442-452` | shared toast (`ui.toast`) | match; “Details corrected. The submission is still pending.” |
| Sidebar, topbar, crumbs, theme switch, event menu `:82-157` | shell (`_AdminLayout`, `_AdminDesignSidebar`) | shell-owned; Review nav current on Details too |
| Prototype bar, scenario toggles, sample data `:436-441`, `:482-1105` | none | not applicable (prototype only) |

## A. Reference literals found in the app source (164 of 409 distinct literals)

| Reference line | Literal | App location |
| --- | --- | --- |
| `:87` text | Administrator | `Pages/Admin/Review/Details.cshtml.cs:80` |
| `:95` text | Events | `Pages/Admin/Review/Details.cshtml.cs:4` |
| `:97` text | Accounts | `Pages/Admin/Review/Details.cshtml.cs:181` |
| `:98` text | Audit | `Pages/Admin/Review/Details.cshtml.cs:3` |
| `:103` aria-label | Event | `Pages/Admin/Review/Details.cshtml.cs:4` |
| `:120` text | Board | `Pages/Admin/Review/Details.cshtml.cs:6` |
| `:122` text | Review | `Pages/Admin/Review/Details.cshtml.cs:17` |
| `:158` placeholder | Search team, account or tile | `Pages/Admin/Review/Index.cshtml:49` |
| `:159` aria-label | Clear search | `Pages/Admin/Review/Index.cshtml:50` |
| `:161` aria-label | Status | `Pages/Admin/Review/Details.cshtml.cs:49` |
| `:169` text | Couldn’t load the submissions | `Pages/Shared/_AdminReviewLoadStates.cshtml:25` |
| `:169` text | Your search and status are kept. Check your connection and try again. | `Pages/Shared/_AdminReviewLoadStates.cshtml:25` |
| `:169` text | Try again | `Pages/Admin/Review/_ReviewWorkspace.cshtml:67` |
| `:175` text | No submissions match | `Pages/Admin/Review/Index.cshtml:73` |
| `:175` text | Try another search or status. | `Pages/Admin/Review/Index.cshtml:73` |
| `:175` text | Clear search and status | `Pages/Admin/Review/Index.cshtml:73` |
| `:178` aria-label | Loading submissions | `Pages/Shared/_AdminReviewLoadStates.cshtml:29` |
| `:179` text | Uploaded | `Pages/Admin/Review/Details.cshtml.cs:217` |
| `:179` text | Team | `Pages/Admin/Review/Details.cshtml.cs:194` |
| `:179` text | Account | `Pages/Admin/Review/Details.cshtml.cs:66` |
| `:179` text | Tile | `Pages/Admin/Review/Details.cshtml.cs:59` |
| `:179` text | Check | `Pages/Admin/Review/Details.cshtml.cs:37` |
| `:185` aria-label | Submissions, pending first | `Pages/Admin/Review/Index.cshtml:78` |
| `:195` aria-label | No warnings | `Pages/Admin/Review/Index.cshtml:95` |
| `:217` text | Previous | `Pages/Admin/Review/Details.cshtml.cs:278` |
| `:217` aria-label | Previous submission | `Pages/Admin/Review/Details.cshtml:35` |
| `:219` text | Next | `Pages/Admin/Review/Details.cshtml.cs:278` |
| `:219` aria-label | Next submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:56` |
| `:229` text | Couldn’t load this submission | `Pages/Shared/_AdminReviewLoadStates.cshtml:43` |
| `:229` text | Check your connection and try again. Nothing was changed. | `Pages/Shared/_AdminReviewLoadStates.cshtml:43` |
| `:232` text | This submission isn’t available | `Pages/Admin/Review/Details.cshtml:45` |
| `:232` text | The link may be incomplete, or the event is hidden. | `Pages/Admin/Review/Index.cshtml:37` |
| `:232` text | Go to the queue | `Pages/Admin/Review/Details.cshtml:45` |
| `:237` aria-label | Screenshot | `Pages/Admin/Review/ReviewList.cs:17` |
| `:238` aria-label | Screenshot controls | `Pages/Admin/Review/_ReviewWorkspace.cshtml:71` |
| `:240` aria-label | Zoom out | `Pages/Admin/Review/_ReviewWorkspace.cshtml:73` |
| `:240` title | Zoom out (−) | `Pages/Admin/Review/_ReviewWorkspace.cshtml:73` |
| `:242` aria-label | Zoom in | `Pages/Admin/Review/_ReviewWorkspace.cshtml:75` |
| `:242` title | Zoom in (+) | `Pages/Admin/Review/_ReviewWorkspace.cshtml:75` |
| `:244` text | Fit | `Pages/Admin/Review/_ReviewWorkspace.cshtml:77` |
| `:244` title | Fit (0) | `Pages/Admin/Review/_ReviewWorkspace.cshtml:77` |
| `:245` title | Actual size (1) | `Pages/Admin/Review/_ReviewWorkspace.cshtml:78` |
| `:247` text | Drag to move · scroll to zoom | `Pages/Admin/Review/_ReviewWorkspace.cshtml:80` |
| `:248` text | Original | `Pages/Admin/Review/Details.cshtml.cs:217` |
| `:250` aria-label | Screenshot viewer. Plus and minus zoom, 0 fits, 1 shows actual size, arrow keys move. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:83` |
| `:252` text | Loading the screenshot | `Pages/Admin/Review/_ReviewWorkspace.cshtml:87` |
| `:253` text | Couldn’t load the screenshot | `Pages/Admin/Review/_ReviewWorkspace.cshtml:88` |
| `:253` text | The file is kept; this was a loading problem. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:88` |
| `:253` text | Open original | `Pages/Admin/Review/_ReviewWorkspace.cshtml:88` |
| `:254` text | No screenshot available | `Pages/Admin/Review/_ReviewWorkspace.cshtml:92` |
| `:254` text | No image is attached to this submission. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:92` |
| `:267` aria-label | Needs a closer look | `Pages/Admin/Review/_ReviewWorkspace.cshtml:105` |
| `:276` text | Correct details | `Pages/Admin/Review/_ReviewWorkspace.cshtml:121` |
| `:277` text | Change only what the screenshot shows differently. The screenshot, upload time and contribution can’ | `Pages/Admin/Review/_ReviewWorkspace.cshtml:122` |
| `:279` text | Drop | `Pages/Admin/Review/Details.cshtml.cs:39` |
| `:280` text | Credited account | `Pages/Admin/Review/_ReviewWorkspace.cshtml:137` |
| `:283` text | Reason for the correction | `Pages/Admin/Review/_ReviewWorkspace.cshtml:142` |
| `:284` placeholder | e.g. The chat shows a fish barrel, not a tackle box. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:143` |
| `:291` text | Compare with the screenshot | `Pages/Admin/Review/_ReviewWorkspace.cshtml:150` |
| `:297` text | Submitter’s note | `Pages/Admin/Review/_ReviewWorkspace.cshtml:163` |
| `:302` aria-label | History and files | `Pages/Admin/Review/_ReviewWorkspace.cshtml:167` |
| `:304` text | History | `Pages/Admin/Review/Details.cshtml.cs:35` |
| `:308` text | Earlier approvals for this objective | `Pages/Admin/Review/_ReviewWorkspace.cshtml:179` |
| `:309` text | None for this team yet. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:181` |
| `:312` text | Files | `Pages/Admin/Review/_ReviewWorkspace.cshtml:186` |
| `:313` text | No files are attached. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:188` |
| `:313` text | Open | `Pages/Admin/Review/Details.cshtml.cs:32` |
| `:313` text | Checksum | `Pages/Admin/Review/Details.cshtml.cs:37` |
| `:318` text | Decision | `Pages/Admin/Review/Details.cshtml.cs:42` |
| `:323` text | Back to the queue | `Pages/Admin/Review/_ReviewWorkspace.cshtml:203` |
| `:328` text | Reject… | `Pages/Admin/Review/_ReviewWorkspace.cshtml:216` |
| `:330` text | Correct details… | `Pages/Admin/Review/_ReviewWorkspace.cshtml:218` |
| `:334` text | Reason for rejecting | `Pages/Admin/Review/_ReviewWorkspace.cshtml:224` |
| `:335` placeholder | e.g. The drop message isn’t visible in the screenshot. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:225` |
| `:339` text | Cancel | `Pages/Admin/Review/Details.cshtml.cs:55` |
| `:342` text | Correcting details for this submission. It stays pending. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:232` |
| `:348` text | Reverse approval… | `Pages/Admin/Review/_ReviewWorkspace.cshtml:240` |
| `:366` text | Back | `Pages/Admin/Review/_ReviewWorkspace.cshtml:203` |
| `:404` text | Reason for reversing | `Pages/Admin/Review/_ReviewWorkspace.cshtml:252` |
| `:405` placeholder | e.g. The drop was on a different account than the one credited. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:253` |
| `:406` text | Required. Kept in the history. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:144` |
| `:439` aria-label | Reload | `Pages/Admin/Review/Details.cshtml.cs:26` |
| `:509` js | minute | `Pages/Admin/Review/Details.cshtml.cs:203` |
| `:509` js | minutes | `Pages/Admin/Review/Details.cshtml.cs:203` |
| `:630` js | Approval reversed | `Pages/Admin/Review/_ReviewWorkspace.cshtml:55` |
| `:706` js | , status: | `wwwroot/js/admin-review.js:315` |
| `:745` js | No screenshot | `Pages/Admin/Review/_ReviewWorkspace.cshtml:92` |
| `:794` js | Wait for the current decision to finish. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:65` |
| `:942` js | Enter a reason for the correction. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:65` |
| `:942` js | Enter a reason. The team sees it. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:65` |
| `:942` js | Use 4,000 characters or fewer. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:65` |
| `:947` js | Choose the drop shown in the screenshot. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:66` |
| `:948` js | Change at least one detail, or cancel. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:66` |
| `:955` js | approve this submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:57` |
| `:961` js | added to | `Pages/Admin/Review/_ReviewWorkspace.cshtml:53` |
| `:968` js | reject this submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:57` |
| `:971` js | The team’s captains and | `Pages/Admin/Review/_ReviewWorkspace.cshtml:54` |
| `:971` js | are notified with your reason. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:54` |
| `:982` js | save the correction | `Pages/Admin/Review/_ReviewWorkspace.cshtml:57` |
| `:1002` js | reverse the approval | `Pages/Admin/Review/_ReviewWorkspace.cshtml:57` |
| `:1005` js | removed from | `Pages/Admin/Review/_ReviewWorkspace.cshtml:55` |
| `:1035` js | This submission changed while you were reviewing it. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:59` |
| `:1040` js | Couldn’t | `Pages/Admin/Review/_ReviewWorkspace.cshtml:58` |
| `:1052` js | It wasn’t saved. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:62` |
| `:1136` js | Ended | `Pages/Admin/Review/ReviewList.cs:11` |
| `:1136` js | · uploads closed, review continues | `Pages/Admin/Review/Index.cshtml:18` |
| `:1137` js | Review opens when the event goes Live | `Pages/Admin/Review/Index.cshtml:22` |
| `:1141` js | pending | `Pages/Admin/Review/Details.cshtml.cs:107` |
| `:1141` js | Nothing pending | `Pages/Admin/Review/Index.cshtml:28` |
| `:1148` js | No submissions yet | `Pages/Admin/Review/Index.cshtml:69` |
| `:1149` js | Evidence appears here as teams submit it. | `Pages/Admin/Review/Index.cshtml:69` |
| `:1149` js | Teams can submit evidence once | `Pages/Admin/Review/Index.cshtml:69` |
| `:1149` js | is Live. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:24` |
| `:1151` js | submissions | `Pages/Admin/Review/ReviewList.cs:9` |
| `:1161` js | Submission not found | `Pages/Admin/Review/Details.cshtml:9` |
| `:1161` js | Couldn’t load the submission | `Pages/Shared/_AdminReviewLoadStates.cshtml:25` |
| `:1166` js | Submission | `Pages/Admin/Review/Details.cshtml.cs:20` |
| `:1169` js | No screenshot is available. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:106` |
| `:1169` js | There is nothing to compare. Reject it with a reason unless the evidence can be confirmed another wa | `Pages/Admin/Review/_ReviewWorkspace.cshtml:106` |
| `:1170` js | after the event ended. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:107` |
| `:1170` js | Uploads are allowed until the cutoff, but the drop must happen before | `Pages/Admin/Review/_ReviewWorkspace.cshtml:107` |
| `:1172` js | left | `Pages/Admin/Review/_ReviewWorkspace.cshtml:43` |
| `:1173` js | The same image is on | `Pages/Admin/Review/_ReviewWorkspace.cshtml:112` |
| `:1173` js | another submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:112` |
| `:1173` js | other submissions | `Pages/Admin/Review/_ReviewWorkspace.cshtml:112` |
| `:1178` js | Approving adds | `Pages/Admin/Review/_ReviewWorkspace.cshtml:38` |
| `:1178` js | Nothing left to add | `Pages/Admin/Review/_ReviewWorkspace.cshtml:43` |
| `:1179` js | Worth | `Pages/Admin/Review/_ReviewWorkspace.cshtml:39` |
| `:1179` js | , but only | `Pages/Admin/Review/_ReviewWorkspace.cshtml:39` |
| `:1179` js | remains | `Pages/Admin/Review/_ReviewWorkspace.cshtml:39` |
| `:1179` js | remain | `Pages/Admin/Review/_ReviewWorkspace.cshtml:39` |
| `:1179` js | Objective: | `Pages/Admin/Review/_ReviewWorkspace.cshtml:40` |
| `:1179` js | The objective is already complete for | `Pages/Admin/Review/_ReviewWorkspace.cshtml:43` |
| `:1180` js | Added | `Pages/Admin/Review/_ReviewWorkspace.cshtml:46` |
| `:1180` js | Objective progress: | `Pages/Admin/Review/_ReviewWorkspace.cshtml:46` |
| `:1182` js | Not counted | `Pages/Admin/Review/_ReviewWorkspace.cshtml:47` |
| `:1186` js | Manual objective | `Pages/Admin/Review/_ReviewWorkspace.cshtml:154` |
| `:1189` js | Event window | `Pages/Admin/Review/_ReviewWorkspace.cshtml:157` |
| `:1195` js | Current · replaced the original | `Pages/Admin/Review/_ReviewWorkspace.cshtml:191` |
| `:1195` js | Earlier version · kept for history | `Pages/Admin/Review/_ReviewWorkspace.cshtml:191` |
| `:1203` js | It may already have been saved. Check before doing anything else. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:60` |
| `:1203` js | Check status | `Pages/Admin/Review/_ReviewWorkspace.cshtml:61` |
| `:1217` js | Screenshot submitted for | `Pages/Admin/Review/_ReviewWorkspace.cshtml:86` |
| `:1217` js | , credited to | `Pages/Admin/Review/_ReviewWorkspace.cshtml:86` |
| `:1229` js | Review starts when the event is Live. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:24` |
| `:1229` js | Review is available while the event is Live or in final review. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:28` |
| `:1238` js | Reject submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:229` |
| `:1239` js | Required. Sent to the team’s captains and | `Pages/Admin/Review/_ReviewWorkspace.cshtml:226` |
| `:1239` js | , and kept in the history. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:226` |
| `:1249` js | Choose a drop to see its weight. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:66` |
| `:1249` js | , the same as now. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:66` |
| `:1249` js | The weight comes from the board. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:66` |
| `:1252` js | Save correction | `Pages/Admin/Review/_ReviewWorkspace.cshtml:234` |
| `:1257` js | Approved by | `Pages/Admin/Review/_ReviewWorkspace.cshtml:239` |
| `:1260` js | Withdrawn by the team before review. It stays in the history. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:263` |
| `:1260` js | This attempt stays in the history and can’t be approved again. A new upload is a separate submission | `Pages/Admin/Review/_ReviewWorkspace.cshtml:263` |
| `:1265` js | Enter a reason. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:65` |
| `:1269` js | Its contribution of | `Pages/Admin/Review/_ReviewWorkspace.cshtml:246` |
| `:1269` js | is removed, and tile, line, leaderboard and placement progress are recalculated. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:246` |
| `:1269` js | Later approved evidence for this objective may take up the freed amount. | `Pages/Admin/Review/_ReviewWorkspace.cshtml:246` |
| `:1272` js | Reverse approval | `Pages/Admin/Review/_ReviewWorkspace.cshtml:240` |
| `:1279` js | Leave before checking? | `Pages/Admin/Review/_ReviewWorkspace.cshtml:64` |
| `:1280` js | We couldn’t confirm your last decision, and it may already have been saved. Check now, or leave and  | `Pages/Admin/Review/_ReviewWorkspace.cshtml:64` |
| `:1281` js | Leave anyway | `Pages/Admin/Review/_ReviewWorkspace.cshtml:64` |

## C. Reference literals not in the app source (classified)

- **Shell chrome** (sidebar labels, “Iron Pact · Administrator”, theme labels, “Open navigation”, collapse/expand, “Administration”): rendered by the shared shell, not by Review (`:83-140`, `:1294-1297`). Match by the shell.
- **Prototype only** (proto bar, “Prototype URL”, scenario switches “Fail next load”, “Show browser bar”, sample events, teams, accounts, drops, chat lines, file names, ids, `, key:`-style fragments, JS concatenation pieces): not product content. Not applicable.
- **Copenhagen strings** (`:1141` “Times in Copenhagen time”, `:1188` “Copenhagen time · recorded by the server”): replaced by the event-timezone wording. Registered (U8-Q1).
- **“active account since / switched from”** (`:1184`): dropped. Registered (S9 row).
- **“Playing accounts on this team. The player is set from the account.”** (`:280`): app says “Playing accounts on this team at upload time. The player is set from the account.” Registered (AU17a row).
- **Outcome and history sample strings** (`:958-961`, `:970`, `:978-986`, `:1003-1004`, `:1052`, `:1058-1063`): app wording is built from the server outcome. Registered (outcome/recovery row; History row).
- **Discard dialog** (`:1279-1281`): shared discard dialog from the shell; its own text is shell-owned.
- **“Opens the original image in a new tab, at full size.”** (`:930`) and **“Opens {file} …”** toast (`:1196`): the app opens the real original (`/Evidence/{assetId}`) so no toast is needed. Covered by the viewer row (U8-E1).
- **“Fails, nothing changed” / “Times out …”** (`:489`): prototype scenario labels. Not applicable.
- **“Nothing is left to add: this objective is already complete for …” (`:958`)**: the server refusal for Approve with nothing left is the existing service text (unchanged, `Resources` “This objective has no remaining eligible contribution…”); reference wording differs. Registered (AU17 Contribution rows, `:1218`).

No mismatch without a decision remains. Behaviour questions are in the U8 reports (open questions), not here.
