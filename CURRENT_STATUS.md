# Current project status

## Active handoff — 3 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`; planner chat
  `01a0ec9a-76e3-7252-9850-3f260c612e59` (UI Planner), collaboration parent `/root`.
- User authorized the post-review fix batch F1–F9 only, with local commits per item.
  Other AU/RC tickets and UI integration remain deferred. No push, merge, deployment,
  production access, live provider calls or user-owned database mutation.
- Assignment brief: `/Users/christopher/Documents/BingoWebpage/review-notes/09-codex-handoff-fix-batch.md`;
  decisions: sibling `08-decisions.md`; original review: sibling `00-index.md` and reports.
  Preserve relevant assignment/evidence in the repository with the batch checkpoint.
- User override: no orchestrator, one direct `gpt-5.6-luna` / `max` collaboration
  implementer. It may make the scoped authorized local commits; no self-review.
  Send reports directly to `/root` before ending each turn, and for blockers/decisions.
  No `wait_threads` or planner polling. Native callback delivery is not a guarantee
  that the planner has already processed a message; record exact checkpoint.
- First send F1 banner rollout proposal; await its resolution while working F2, F3
  consecutively (separate commits), then F4–F9. No F1 implementation before resolution.
- Claude performs the final independent review per the recorded workflow decision.
  Stop with a stable commit-by-commit handoff awaiting that review; no next ticket.

## Baseline and authorized documentation checkpoint

- Review candidate: `ee187bb0472c552ec04c8a70b10200a50c65400f` (clean before policy edits).
- Base main: `22af254c893bb51e7820d84fc4154ff9af3bcc90`.
- Pushed implementation/reference checkpoint: `1e8d457416a275514f4a7f0822dda7a192bbff3f`.
- Expected initial changes: AGENTS.md commit policy and this activation handoff.
  Inspect and preserve them in a separate documentation commit before item commits.
  Do not reset them, stage unrelated changes, or treat planned commits as branch drift.
- Each implemented/tested item gets its own local commit and durable evidence before
  moving on. Those checkpoints await independent review. Review corrections use
  follow-up commits; a commit alone is not technical completion or manual acceptance.

## Retained project boundaries

- AU01–AU10, Participants F01–F06 and Dashboard D01–D09 have prior backend completion;
  new UI integration remains deferred. Luck was user accepted; this batch addresses
  subsequently reported defects. F8 pulls AU13 forward; no other AU11+ resumption.
- Owning product/contracts/data/architecture documents and the approved functionality
  register remain authorities. Promote relevant new approved decisions before code;
  do not implement out-of-batch WOM/AU20 or BR-1/BR-3 decisions.
- UI_PAGE_MATRIX.md alone owns visual acceptance. All 15 named references were accepted;
  canvas 42 / artifact `1790965722-e7ad` remains frozen. Do not edit reference HTML/CSS/JS.
- Prior durable evidence: `docs/references/admin-ui/reviews/2026-10-02/README.md`.
  New compact evidence belongs under `docs/references/admin-ui/reviews/`, not only tmp.
- User-run production SELECTs: 1 banner asset, 0 cleanup rows, 1 v1 Luck checkpoint,
  0 completion corrections, 0 visible published boards missing roster publication.
  These counts do not establish the exact production migration history.

## Next permitted action

F1 rollout proposal is sent to `/root`; implementation awaits proposal resolution
and the exact production migration/object-storage facts listed in the proposal.
F2 is implemented, focused PostgreSQL tested, and committed as
`41eaab760c4454a85612c86e44ab7a7137d0a33d`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f2-luck-checkpoint.md`.
F3 is implemented, focused PostgreSQL tested, and committed as
`9b1ea5fd2c3d6c40abf9334810080b46c7690f6b`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f3-luck-conversion-checkpoint.md`.
F4 is implemented, focused PostgreSQL tested, and committed as
`e7a6464f62499b1d1bbdec7590e5831a69188ac0`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f4-reset-token-security-checkpoint.md`.
F5 is implemented and focused PostgreSQL tested; its separate local commit is
the next checkpoint. Continue F6–F9 after that commit while F1 remains unresolved.
Every checkpoint awaits Claude's independent review; report exact blockers and
unverified checks, and stop after the stable batch handoff.
