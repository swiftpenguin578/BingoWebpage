# DK Legacy Bingo

DK Legacy Bingo is the website for running OSRS (Old School RuneScape) bingo
events for one Discord community. Players sign up, are drafted into teams and
follow a shared board; captains submit drop evidence; admins review it. The site
keeps the standings, the evidence and the history of every event.

## What it does for players

- **Sign up** for an event with a website account, your OSRS character names, and
  the event's questions. You are confirmed or placed on a waiting list, and you
  can edit your signup while signups are open.
- **Join a team.** Captains are volunteers; an admin runs the team draft, and
  rosters are published when it is finalized.
- **Follow the board.** Every team has a 5x5 or 6x6 board of tiles, such as a drop
  from a boss or a set of items. Everyone can see each team's progress, the
  approved drops and the standings (EHB, drop EHB and players).
- **Submit evidence.** Players and captains upload a screenshot for a drop and can
  track its review. The team's submission page keeps the full history, including
  rejection feedback.
- **See results.** Live statistics show Luck percentile and kill-count difference,
  drop value, event milestones and boss leaderboards. After the event, official
  results stay available together with the board and the approved evidence.
- **Stay informed** through personal notifications and live announcements when a
  drop is approved. The site is available in English and Danish, in light and dark
  themes.

## What it does for admins

- **Events:** create an event, edit its identity, set the schedule, and see what
  needs attention on the Overview. An event's lifecycle is explicit and audited.
- **Signup setup:** capacity and waiting list, an optional signup code, and the
  signup form (custom questions, captain volunteering, co-captain).
- **Participants, teams and draft:** manage the signup pool, set up teams and
  captains, run the snake draft, and correct rosters after finalization.
- **Board:** build the board from the OSRS catalogue (bosses, drops, items with
  rates and prices), balance it with EHB, approve it and publish it.
- **Review:** work the evidence queue; approve, reject, correct or reverse
  submissions with reasons.
- **Final review:** check publication readiness, publish the official results, and
  reopen them for a correction if needed.
- **Wise Old Man (WOM):** link or create the event's competition so that player
  activity and EHB feed Luck and the statistics.
- **Catalogue, accounts and audit:** manage the catalogue, website accounts and
  roles, and read the full administrative history.

## How an event runs

1. **Draft:** an admin creates the private event and prepares its identity,
   schedule, signup form and board.
2. **Signups open:** players sign up; admins manage the pool and capacity.
3. **Signups closed:** the team draft is run and the rosters are finalized and
   published; the board is approved and published.
4. **Live:** the event starts at its scheduled time. Teams complete tiles, captains
   submit evidence and admins review it; standings update as drops are approved.
5. **Final review:** after the event ends and the upload deadline passes, admins
   resolve the last blockers and publish the official results.
6. **Archived:** publishing the results archives the event as permanent history.

## Main features

- Roles for visitors, players, captains and co-captains, admins and a Super Admin
- Automatic tile, line, board and ranking calculation, with EHB tie-breaking
- Evidence review with approval, rejection, reasoned corrections and reversals
- A reviewed OSRS catalogue with drop rates, item prices and Wise Old Man mapping
- Luck and kill-count statistics, boss leaderboards and live drop announcements
- Immutable board, roster and result snapshots that preserve competitive history
- Audit trail, notifications, English and Danish, light and dark themes

## Tech stack

ASP.NET Core (.NET 10) Razor Pages with SignalR, PostgreSQL through EF Core,
Cloudflare R2 evidence storage, Caddy and Docker on a single VPS, and GitHub Actions.

## Getting started

Setup, running locally, seeding test data, tests and CI are in
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md). For the product and its rules, start
with [`docs/PRODUCT.md`](docs/PRODUCT.md). Operating the
production site is covered in [`docs/OPERATIONS.md`](docs/OPERATIONS.md).
Agents and contributors: see [`AGENTS.md`](AGENTS.md) for the working rules and the
list of authority documents.
