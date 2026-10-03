# H cleanup remediation handoff — 4 October 2026

Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
branch `codex/participants-functionality`. Direct implementer
`/root/cleanup_astra_remediator` (`gpt-6-astra` / high), planner `/root`.
Claude owns independent named recheck; no independent PASS is claimed here.

## Checkpoints

| Checkpoint | Commit | Disposition |
| --- | --- | --- |
| Activation | `a75bbe8` | Explicit extra documentation-commit approval received after initial automatic rejection. |
| H1 | `b8156b3` | H1-1–H1-7 corrected, checked; awaiting Claude. |
| H2 | `ed19466` | H2-1–H2-3 corrected; AU13 commit-origin wording further clarified with H5. |
| H3 | `f2bdb5d` | Named corrections committed; precise H3-4 wording superseded by the follow-up below. |
| H3-4 follow-up | `6b8331d` | Explicitly approved additional H3-only commit; Start/Scramble wording source-matched. |
| H5 | `c3a3474` | H5-1–H5-9 tickets and owning docs only; no implementation. |
| H6 | `610c842` | H6-1 resource only. |
| H4 | `fe395f2` | H4-1/3/4/5 corrected; H4-2 was proposal-only at this checkpoint. |
| Final reconciliation | `1c16356` | User-authorized two-file status/handoff commit; prior pending-proposal state preserved in Git. |
| H4-2 adoption | This documentation follow-up after `1c16356` | User approved the procedure and coverage limits on 4 October 2026; runbook/gate adopted. Tooling not built/tested, R3 unexecuted, Claude recheck pending. |

The user explicitly approved one additional documentation-only commit containing
this handoff and CURRENT_STATUS.md after the final reconciliation was initially
rejected by automatic approval review. That approval covers these two files only;
that earlier approval did not approve H4-2, R3 execution or independent review. The
user subsequently approved H4-2 procedure/coverage adoption separately, as below. Prior
checkpoints and original evidence remain unchanged in Git and their durable paths.

## Open decisions and resolved approval boundaries

- **H4-2 procedure approved, 4 October 2026:** the user accepted the disposable
  isolated VM/restored database approach **and its stated coverage limits**. The
  runbook now owns the documented procedure and DELIVERY_PLAN the release gate;
  `r3-isolation-proposal.md` records approval under its existing provenance filename.
  The procedure requires denied external access and local WOM refusal/HTTPS S3 fixtures for the exact
  candidate's migration, conversion, preflight, enabled workers and web-health stages.
  Never invoke host `bingo-deploy` as rehearsal. Coverage does not include the
  production host wrapper, real provider/restic/GHCR connectivity/integration,
  public DNS/TLS or production-key recovery. Those limits are accepted, not verified.
- **Execution/review still pending:** no harness/config/fixtures built or tested;
  no credentials/backup/data transfer, production access, provider calls or R3 run.
  Procedure approval authorizes documenting the procedure only. Later implementation,
  backup transfer/access, execution and deployment require separate assignments.
  Claude's independent named recheck remains pending; no independent PASS claimed.
- **H3-4 resolved:** source check found the initial restart sentence inaccurate.
  Start (`src/Bingo.Web/Pages/Admin/Events/Draft.cshtml.cs:480`) clears positions
  for never-picked/RequiresFreshOrder attempts; separate Scramble (`:413`)
  randomizes before picks. Automatic approval review initially rejected the
  correction under the one-commit boundary; no workaround was attempted. The user
  then explicitly approved the exact correction and one additional H3-only commit.
  `6b8331d` applies it without amending an existing checkpoint.
- **Final reconciliation resolved:** automatic approval review initially rejected
  updating CURRENT_STATUS.md and this handoff as an unauthorized overwrite. No
  rejected write occurred. The user subsequently explicitly approved updating
  exactly these files and saving them in one additional documentation-only commit.
- D8 remains a proposal; this round starts no AU/RC/UI work. H7 unchanged. Tile-Luck
  visual acceptance and pending C33 states are as recorded in UI_PAGE_MATRIX.

## Exact finding proof map

Line references below target this saved candidate. Historical empty evidence has
no text line; its conventional :1 locator is labelled empty rather than invented.

| Finding | Exact changed file:line | Outcome |
| --- | --- | --- |
| H1-1 | `DELIVERY_PLAN.md:38`; `DELIVERY_PLAN.md:253` | Restored durable Luck pointer, manifest identity and PASS filename. |
| H1-2 | `DELIVERY_PLAN.md:1346`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:704` | Qualified reviewed 846aed/871051 identities and later AU04 GetEventParticipationAsync review. |
| H1-3 | `MANUAL_TEST_CHECKLIST.md:22` | Dashboard citations now point to durable metadata and say recorded. |
| H1-4 | `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/participants/web-build-after-test-fixes.log:7`; `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/participants/ehb-correction-proof-parsed.log:11`; `DELIVERY_PLAN.md:1532` | Recovered both small logs; raw TRX omission stated accurately. |
| H1-5 | `DELIVERY_PLAN.md:1332`; `FUNCTIONAL_CONTRACTS.md:87`; `MANUAL_TEST_CHECKLIST.md:29` | Historical 9/9 report explicitly not retained in durable copy; no false retention claim. |
| H1-6 | `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/H1-evidence.md:72`; `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/luck/luck-redesign-remediation-evidence-20261001/candidate-manifest.txt:1`; `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/luck/luck-redesign-final-remediation-20261001/scoped-diff-check.txt:1` (empty, 0 bytes) | Recovered distinct source-folder artifacts; mapping explains flat Sol-recovery names. |
| H1-7 | `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/manual-demo-sanitized.md:15` | Preserved route, team 35.2%/-100 and two player 42.8%/-50, 38.9%/-50 aggregates; no names/credentials/runtime IDs. |
| H2-1 | `DELIVERY_PLAN.md:1096`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:686`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:697` | Removed dirty-baseline claim; historical extraction status and durable Dashboard pointer. |
| H2-2 | `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:87`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:89`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:90` | AU16/20/21/22 approved status distinguished from RC proposals; Catalogue row adjacent. |
| H2-3 | `DELIVERY_PLAN.md:269`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:308` | Historical next-action replaced by active status pointer; AU13 done via F8 6d33ce6 (525d5d1 is lock origin, clarified in H5). |
| H3-1 | `DATA_MODEL.md:1892`; `PRODUCT_REQUIREMENTS.md:763` | Retained finish/time/lines/tiles/score-time/EHB order and new-event AU12 boundary. |
| H3-2 | `UI_PAGE_MATRIX.md:131` | User confirmed visual inspection on 2 October; confirmation attributed to 4 October, functional proof remains automated. |
| H3-3 | `docs/references/admin-ui/README.md:538`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:766` | Unique 1 vs first-time 1+1 equal-start caveat. |
| H3-4 | `DATA_MODEL.md:894`; `DATA_MODEL.md:1011`; `DATA_MODEL.md:1099`; `DATA_MODEL.md:1100` | Fields/retirement recorded; approved follow-up 6b8331d states that Start clears prior positions and separate Scramble establishes the new order before picks. |
| H3-5 | `DELIVERY_PLAN.md:387` | OS-4 dead ConfigureSchedule capacity rule recorded as observation in OS-1. |
| H3-6 | `UI_PAGE_MATRIX.md:76` | C33 freshness/reinspection/error states still awaiting manual acceptance. |
| H4-1 | `docs/PRODUCTION_RUNBOOK.md:278`; `DELIVERY_PLAN.md:2735` | NOT EXISTS on unsuperseded publication + Finalized draft + roster rows; previous operator zero must be rechecked. |
| H4-2 | `docs/references/admin-ui/reviews/2026-10-04/cleanup-remediation/r3-isolation-proposal.md:1`; `docs/PRODUCTION_RUNBOOK.md:342`; `DELIVERY_PLAN.md:2738` | PROCEDURE APPROVED 4 October 2026: accepted approach/coverage adopted in runbook and gate; tooling not built/tested, R3 unexecuted, independent recheck pending. |
| H4-3 | `docs/PRODUCTION_RUNBOOK.md:316`; `DELIVERY_PLAN.md:2737` | Pre/post eligible backfill set/count, clone-only Down/re-Up and no production downgrade; unexecuted. |
| H4-4 | `docs/PRODUCTION_RUNBOOK.md:267`; `DELIVERY_PLAN.md:2734` | All-row count replaces undefined global cycle timestamp; prior zero not promoted to new-query proof. |
| H4-5 | `docs/PRODUCTION_RUNBOOK.md:239`; `DELIVERY_PLAN.md:2732` | Verification only; historical deletion sequence explicitly one-time, no repeat/provider operation. |
| H5-1 | `DELIVERY_PLAN.md:426`; `DELIVERY_PLAN.md:1038`; `DATA_MODEL.md:342`; `TECHNICAL_ARCHITECTURE.md:618`; `PRODUCT_REQUIREMENTS.md:616` | Full decided WA-2 rules, accepted snapshot limit, WA-6 outcomes and WA-9 Create-link race; all owners pending implementation. |
| H5-2 | `DELIVERY_PLAN.md:385` | DB-2 fingerprint/roster defect and DB-3 mixed-history headline owned by integration; DB-4/5 separate metadata/date. |
| H5-3 | `DELIVERY_PLAN.md:417` | Render uncertain posted draft without DB read and lost-commit test, not database-dependent recovery. |
| H5-4 | `DELIVERY_PLAN.md:421` | Problem-only observation; separate swallowed/no-op/raw-text audit issues; mechanism undecided. |
| H5-5 | `DELIVERY_PLAN.md:348`; `DELIVERY_PLAN.md:403`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:312` | Own approved AU17a ID and slot; full-pool owners updated, remaining AU17 proposed. |
| H5-6 | `DELIVERY_PLAN.md:382`; `DELIVERY_PLAN.md:383`; `DELIVERY_PLAN.md:422`; `DELIVERY_PLAN.md:423`; `DELIVERY_PLAN.md:391` | Named slots/owners for RL-1 lines, proof gaps, RC01–RC11; BR-10 explicitly binds RC07 structured data. |
| H5-7 | `DELIVERY_PLAN.md:384`; `DELIVERY_PLAN.md:416`; `DELIVERY_PLAN.md:405` | Private source filenames + finding IDs and actionable problem text in tickets themselves. |
| H5-8 | `DELIVERY_PLAN.md:408`; `DELIVERY_PLAN.md:481`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:266`; `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:319` | Ordinary rate-text N x on add/edit/reactivation, default new group, advanced role boundary and future reference refusal removal. |
| H5-9 | `DELIVERY_PLAN.md:487`; `DELIVERY_PLAN.md:505`; `DELIVERY_PLAN.md:516`; `DATA_MODEL.md:1342`; `PRODUCT_REQUIREMENTS.md:776` | Final chance; retire parent input without history/columns rewrite; no parent-EHB ticket; decided panel and Admin-editable informational activity team size with migration recheck. |
| H6-1 | `src/Bingo.Web/Resources/SharedResource.da.resx:2985` | Only production-file edit: Danish resource key/value. |

## Executed checks and limits

- `git diff --check` per item and `git diff f6b5bd9 --check`: passed.
- Four recovered H1 artifacts byte-compared with exact private-backup source files:
  passed. Final Luck manifest identity exists in retained independent PASS. Small
  logs preserve 0-warning/error build and 1/1 proof; no new test execution claimed.
- H3 targeted source comparison: ranking matches PublicProgressCalculator; cohort
  matches AdminDashboardIntegrationTests (unique 1, first-time 1+1). Restart check
  exposed the wording error; the user-approved follow-up now matches Start/Scramble
  source. This is source comparison, not new behavior-test execution.
- H4 runbook/gate query normalized-text parity and model predicate/column comparison:
  passed. No SQL executed, no migration/Down/rehearsal run and no production counts
  independently verified. Queries require later authorized operator execution.
- H5 named-rule/ID checks, three pending-AU20 markers, private report filename
  existence and retired-phrase checks passed. Each ticket carries the needed finding
  text; private original reports are provenance, not a prerequisite to understand it.
- H6 resource XML parsed; exact caller key occurs once with a nonempty Danish value.
  No build/test rerun: string-only change, previous H6 behavior proof retained.
- `git diff f6b5bd9 --name-only -- src tests`: only SharedResource.da.resx.
  AGENTS.md, CLAUDE.md and frozen `.dc.html` references unchanged.
- No provider calls, user-owned DB operations, production access, rehearsal,
  deployment, push, merge, amended commits or extra worker/reviewer started.

## Required searches, paths and omissions

Search set: root DELIVERY_PLAN, FUNCTIONAL_CONTRACTS, MANUAL_TEST_CHECKLIST,
UI_PAGE_MATRIX, DATA_MODEL, TECHNICAL_ARCHITECTURE, PRODUCT_REQUIREMENTS, TICKETS,
and admin-ui README/FUNCTIONALITY_CHANGES.

- “Rewrite the ticket around”: zero matches. “must read the database”: zero matches.
- Active-document `/private/tmp` search is **not globally clean**: DELIVERY_PLAN 8,
  MANUAL_TEST_CHECKLIST 10, UI_PAGE_MATRIX 6 and TICKETS 93 matches remain, all older
  out-of-scope historical references. Review 15a explicitly excluded these from H1.
  No Participants/Dashboard/Luck H1 evidence-root citation remains in the named
  active owners. Historical copied evidence retains original command paths.
- Restored evidence/durable pointer paths and new H4 proposal links were checked for
  existence. The copied original review's relative 15a/15b links refer to private
  originals in `/Users/christopher/Documents/BingoWebpage/review-notes/`; those reports
  were read in place and not copied/relabelled. This handoff does not claim those
  relative links resolve inside the new evidence directory.
- Dashboard 9/9 raw TRX and other large/raw evidence remain omitted, explicitly
  labelled. No absent evidence, counts, hashes, provenance or manual acceptance
  was fabricated. No credentials, participant names or runtime identifiers copied.

## H4-2 documentation-adoption checks

The user explicitly authorized this five-file documentation follow-up after
`1c16356`: PRODUCTION_RUNBOOK.md, DELIVERY_PLAN.md, the existing proposal/approval
record, CURRENT_STATUS.md and this handoff. Exact staged scope, diff whitespace,
changed links/anchors and approval/execution wording were checked. The accepted
procedure retains all original isolation stages and R-1/final-candidate gates;
no executable tooling, production/provider check or independent review ran.
The terminal callback supplies this follow-up's exact commit SHA and tree status.

## Next permitted action

Claude independently rechecks the named remediation commits including this H4-2
adoption. The procedure and coverage limits are user-approved; harness/tooling
validation and R3 execution remain pending and release-blocking. No additional
procedure-approval request is needed for the unchanged accepted approach. A later
explicit assignment is required to implement tooling, transfer/access backups or
execute the final-candidate rehearsal. Deployment remains separately authorized.
Stop here; no further ticket, code/config, broader audit, execution, push or merge.
