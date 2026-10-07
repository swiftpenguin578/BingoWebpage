# U9 checkpoint after 1b

Checkout `/Users/christopher/.codex/worktrees/u9-final-wom/BingoWebpage`, branch `codex/u9-final-wom`, base `a91b29ad627dac8b5bf859b1948ea4bbea014877`.

Commits: 0a `7a9d59c1` (Final Review server), 0b `740967ac` (WOM server), 1a `b0882c06` (Final Review workspace), 1b `4030828d` (Final Review actions/drawers). One local commit per completed item; no push/merge/deploy.

Done: mandatory Reopen version/no-write refusal; blocking-current-event read; structured Final Review and WOM outcomes/current reads; detailed WOM eligibility/reasons and stored end-update retry time; hidden WOM inspection removed; Final Review shared layout/readiness/standings/history/read-only states; guarded in-place Publish/Reopen; A15/AU18 notes and version drawers. No lane A/B production files, migrations, shared CSS or shared lifecycle script touched.

Executed evidence and exact commands are in `docs/evidence/u9-item-{0a,0b,1a,1b}.md`. Counts: 0a 73 PostgreSQL + 4 localization; 0b 264 PostgreSQL + 8 source/localization + 2 application; 1a 22 passing HTTP cases plus 5 named/direct-consequence rechecks after two old-markup failures, 10 source, 5 widths per Chromium/WebKit; 1b 8 source, final 4 localization, 7 browser scenario groups per engine, 5 conformance widths per engine. No whole .NET suite. Initial implementation/test expectation failures were corrected; no known base failures observed. One automatic permission-review timeout resolved with its expressly permitted single retry.

Shared integration edits: shell family registration + load template registration, conformance registration, Current-handler policy/classification, append-only DELIVERY_PLAN rows, one contiguous DA block at the resource end (obsolete next-time entry removed). Runtime fixtures use their own PostgreSQL containers/Kestrel ports. Shared 5310/5320/54339 were untouched.

Wording proposed for the early look: A15 “Publishing now makes the last fetch before the end the official WOM data.”; U9-Q1 Live/Awaiting/legacy Finalized reasons exactly as ruled; Cancelled “This event was cancelled. Results are read-only.”; legacy Finalized “Official results are read-only. Complete the legacy publication to archive the event, or reopen for corrections.”; Archived “Official results are read-only. Reopen only when a correction is needed.” Unknown outcome says “We couldn’t confirm whether the results were published/reopened” then the cached current state and latest retained version, without attributing the change. Final refresh success is explicit; failures/skips retain the AU18 reason.

No unruled product question, migration need or A/B-file need identified. Stored end-update retry only, make-due handler/test only, state-specific Reopen wording and reference shared-rank marker rulings applied. Other wording remains proposed. No independent review or manual acceptance claimed; UI_PAGE_MATRIX untouched.

Review environment: planner owns serving. Build this branch after item 2b. Final Review owned scenario runner uses the existing `final-review` profile with opt-in `BINGO_PARITY_U9_READY=1`; open Finalize for current ready/blocked, archived blocked by current Awaiting, publication with skipped refresh, retained `?version=1`, and read-only states. Detailed combined scenarios/parity checklist follow in 2b. No shared environment start requested or attempted.

Exact next permitted step: continue item 2a WOM workspace immediately, then 2b actions/parity/scenarios; commit each. Stop at the early look after 2b, before items 3/4. Planner arranges serving, review and acceptance.
