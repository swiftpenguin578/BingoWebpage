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
- F1's proposal was sent before implementation. The user selected a one-time
  manual cleanup, so its reconciliation is now limited to the runbook and
  controlled fixture evidence; the migration and guard remain unchanged.
- Claude completed the independent read-only source review of F1–F9; no tests
  were executed by Claude. R-2 is the only authorized test-only follow-up in
  this handoff. After its stable commit, await Claude's named R-2 recheck; no
  further ticket starts here.

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
- User-reported production facts before the selected banner cleanup: 1 banner
  asset, 0 cleanup rows, one exact event reference, and zero evidence/team/tile
  image references. The user then cleared that reference and incremented the
  event version in a targeted transaction, deleted exactly one asset row, saw
  zero rows afterward, and confirmed exact PNG deletion in Cloudflare R2. No
  agent production access, live provider call, or user-owned database mutation
  occurred. The exact migration baseline remains unknown. Other user-run facts:
  1 v1 Luck checkpoint, 0 completion corrections, 0 visible published boards
  missing roster publication.

## Next permitted action

F1's selected one-time manual banner cleanup path is documented, focused-tested,
and committed as `2d62b59`, with durable reconciliation in
`docs/references/admin-ui/reviews/2026-10-03/f1-banner-rollout-proposal.md`.
The migration, temporary-ledger behavior, and pending-key guard are unchanged;
no automated cleanup or retention/schema change was added. The focused
`BannerRetirementMigrationTests` run passed all three tests, including the
manually-cleaned one-asset shape, empty path, and populated guard/shared-object
path. The exact production migration baseline and a full controlled
`bingo-deploy` rehearsal through `--migrate`, `--production-preflight`, and web
replacement remain unverified, so F1 deploy-proof is not claimed complete.
F2 is implemented, focused PostgreSQL tested, and committed as
`41eaab760c4454a85612c86e44ab7a7137d0a33d`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f2-luck-checkpoint.md`.
F3 is implemented, focused PostgreSQL tested, and committed as
`9b1ea5fd2c3d6c40abf9334810080b46c7690f6b`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f3-luck-conversion-checkpoint.md`.
F4 is implemented, focused PostgreSQL tested, and committed as
`e7a6464f62499b1d1bbdec7590e5831a69188ac0`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f4-reset-token-security-checkpoint.md`.
F5 is implemented, focused PostgreSQL tested, and committed as
`d6fc7fc2d56eafc99dc90993f9f3f9f081eeb1fa`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f5-event-slug-checkpoint.md`.
F6 is implemented, focused PostgreSQL tested, and committed as
`444bfc46c793a761d635099f0778ede00ea846f2`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f6-event-end-checkpoint.md`.
F7 is implemented, focused PostgreSQL tested, and committed as
`36ef73e4292c348de7d2ba3e53366b2ecefdf356`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f7-participant-version-checkpoint.md`.
F8 AU13 is implemented, focused PostgreSQL and board-markup tested, and
committed as `6d33ce67d048d3452fa34c4bc0b06461f431ea10`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f8-au13-checkpoint.md`.
F9 BR-5/BR-6 is implemented, focused PostgreSQL tested, and committed as
`a3f3f1a288df62c0a634db4d053fc71f0a865e6d`, with durable evidence in
`docs/references/admin-ui/reviews/2026-10-03/f9-finalization-validation-checkpoint.md`.
The earlier F1 automated rollout proposal is superseded by the selected manual
cleanup reconciliation above; its prior commit remains in history. R-2 is now
authorized and corrected in the publication test only: the concurrent loser
may be the expected stale-session refusal or a verified `AlreadyPublished`
result, while all finalization, placement, version, audit, rollback, and retry
assertions remain mandatory. Its focused real-PostgreSQL check is recorded in
`docs/references/admin-ui/reviews/2026-10-03/r2-results-publication-checkpoint.md`.
R-1 is user-resolved by keeping conversion failure blocking this release until
a successful isolated production-copy rehearsal. R-3 remains unexecuted and
deferred; no production deployment is authorized. R-4 standing per-item local
commit authority is confirmed. Claude's named R-2 recheck remains pending; no
independent PASS claim is made here.
