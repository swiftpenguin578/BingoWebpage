# Post-review fix-batch checkpoint — 3 October 2026

This durable checkpoint records the authorized F1–F9 remediation handoff for
`codex/participants-functionality`. The source reports and decision brief remain
outside the checkout under `/Users/christopher/Documents/BingoWebpage/review-notes/`;
the hashes below identify the exact inputs used for this batch. This repository copy
is a sanitized scope/evidence index and contains no secrets or participant data.

## Baseline and routing

- Review candidate: `ee187bb0472c552ec04c8a70b10200a50c65400f`
- Base `main`: `22af254c893bb51e7820d84fc4154ff9af3bcc90`
- Branch: `codex/participants-functionality`
- Reference checkpoint: `1e8d457416a275514f4a7f0822dda7a192bbff3f`
- Scope: F1 banner retirement rollout, F2 Luck partial replacement, F3 explicit
  v1 Luck conversion, F4 reset-link reauthorization, F5 event slug allocation,
  F6 late end catch-up, F7 Participants expected-version enforcement, F8 AU13
  editable planning estimate, and F9 finalization wording/reopen validation.
- Local commits are authorized per item; no push, merge, deployment, production
  access, live provider calls, or user-owned database mutation. Claude owns the
  final independent review after the item checkpoints.

## Source provenance

| Input | SHA-256 |
| --- | --- |
| `00-index.md` | `2397179a267e864b64a9007c60f3107f0dfee651a635e1049e4fc49c0e56759a` |
| `03-luck-kc.md` | `f08ed6b69e1b9bc6e02e7aee2b14a326d129a0e5798a1a5445546793bf43caf3` |
| `07-cross-cutting.md` | `51a3b31785257e03b7c453e5afd1474d4ca3338ef4b5bd23079d15c1a745f2db` |
| `08-decisions.md` | `77343a451a710ffcbad75a258f24b4beca36c2c36444f1839d1a4cb72315c70d` |
| `09-codex-handoff-fix-batch.md` | `6d48b0b1bba27ae9c38fd248f846069bb27d33c61822c58a5c0e058a5bb4d45f` |

## Product and proof boundaries

- F1 must first receive a rollout recommendation. The standard deploy path must
  migrate from the actual production migration state, pass production preflight,
  and preserve retained-data and cleanup requirements without a destructive SQL
  shortcut. The single production banner asset and zero cleanup rows are facts,
  not proof of the exact migration history.
- F2 retains a compatible prior complete snapshot as a whole. Without one, a
  newer incomplete batch may replace an older incomplete batch while complete
  scopes remain independently usable. Reads stay database-only.
- F3 is an explicit bounded idempotent operator conversion. Every event reports
  Converted, Already converted, or Could not convert with a reason; any failure
  leaves the v1 result unchanged and produces a non-success operator outcome.
- F4 checks both reset recipient and issuing account under PostgreSQL locking.
  Role, active-state and ownership changes supersede unused reset tokens in the
  same transaction; refused consumes create no success audit.
- F5–F9 remain narrowly scoped to the brief. Durable item reports record the exact
  commit, checks and unresolved limitations as the batch progresses.

This checkpoint is an implementation handoff, not an independent review or manual
acceptance. The final review remains Claude's responsibility.
