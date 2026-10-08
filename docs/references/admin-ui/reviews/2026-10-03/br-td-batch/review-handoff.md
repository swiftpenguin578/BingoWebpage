# G1–G6 independent review handoff

Implementation/checks complete; Claude independent review pending. Planner verified
the commit inventory and clean worktree; did not independently review code or rerun tests.

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Base: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`.
- Implementation tip: `02d19dbf699d5d4781e68d09f0fd1ccfd031d8ea`.
- Activation: `14d88b0`; subsequent planner handoff reconciliation is documentation only.
- Approved scope: [handoff.md](handoff.md). Original findings and decisions:
  `/Users/christopher/Documents/BingoWebpage/review-notes/06a-board-review-final.md`,
  `08-decisions.md` and `11-teams-draft.md` in that same directory.

| Item | Commit | Recorded checks |
| --- | --- | --- |
| G1 | `24c30fc66af9cb56087f0d60959d858e5420c788` | [G1 evidence](G1-evidence.md) |
| G2 | `2b7ddbc017ee7b5d83e7a539be18699eafe28bc0` | [G2 evidence](G2-evidence.md) |
| G3a | `a8a6c24d32016eb96a13cfd5addf06e38aa1021b` | [G3a evidence](G3a-evidence.md) |
| G3b | `56020b861c795e7abaafe4ffb44580e8b4a0d8ac` | [G3b evidence](G3b-evidence.md) |
| G4 | `1b5a139d6709ce499c87181363bd81b44ce1bdc7` | [G4 evidence](G4-evidence.md) |
| G5 | `b9bb40575a1ff4db9c8c77cfbfea30ba1ca0d6e8` | [G5 evidence](G5-evidence.md) |
| G6 | `02d19dbf699d5d4781e68d09f0fd1ccfd031d8ea` | [G6 evidence](G6-evidence.md) |

The implementer records passing focused executable checks and builds in each evidence
file, including real PostgreSQL ordering/race checks. These are implementation
evidence, not independent review results. G1 and G3a merit the brief’s extra attention.

Implementation choices/limits:

- G4 adds `requires_fresh_order` and its migration to allow restart while retaining
  the original first-pick timestamp, cancelled picks and prior publications.
- G6 creates a new role-only roster publication during permitted Live/Final Review
  changes; earlier publications remain. Includes bounded serialization retries.
- TD-8 additional movement races were not included in G3a.
- No unresolved decision/blocker was reported. No production access, provider calls,
  user database mutation, push, merge or deployment occurred.
- R-3 remains unexecuted. Other AU/RC work, UI integration and the separate cleanup
  brief remain deferred. Stop for Claude’s review.
