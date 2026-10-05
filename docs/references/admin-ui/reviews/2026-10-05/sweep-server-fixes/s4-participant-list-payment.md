# S4 — participant-list payment handler

Status: server part complete in brief40; P-1 binding remains pending.

Authority: brief40 S4, `review-notes/08-decisions.md` S4 option (a), and
`SignupService.SetPaymentAsync` state policy. The list handler is exempted only
for `Payment`; the service remains the authority and the list display is unchanged.

Implementation commit: `c174f03cd82edb2389f334e404ad6ecd0b1a90bf`.

The route filter now treats `Participants.cshtml` `Payment` exactly like the
existing participant-detail exemption. The real PostgreSQL/HTTP proof is
`C11FinalizedRosterIntegrationTests.ParticipantListPaymentRemainsEditableThroughFinalReviewAndFinalizedButNotDiscarded`.

Evidence:

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~C11FinalizedRosterIntegrationTests.ParticipantListPaymentRemainsEditableThroughFinalReviewAndFinalizedButNotDiscarded' --logger 'trx;LogFileName=/tmp/bingo-brief40-s4.trx'` — **failed** during test setup because the Discarded GET correctly returned 404 and the first test version tried to load it for an antiforgery token.
- The same command with `--logger 'trx;LogFileName=/tmp/bingo-brief40-s4-rerun.trx'` after reusing the pre-discard page token — **passed 1, failed 0, skipped 0**. It proves Live payment success and `participant.payment_updated`, Finalized payment success and audit, Discarded refusal with no added audit, and a non-payment Live list handler refusal.

Test-change mapping: this is a new regression test; no existing expectation was
changed or weakened. The first failed run was a test-fixture setup correction,
not a production behavior failure.
