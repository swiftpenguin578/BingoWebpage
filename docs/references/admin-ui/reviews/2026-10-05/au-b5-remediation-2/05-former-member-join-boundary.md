# Item 5 — D12 former-member join boundary

Authority: brief38 item5; review36a F1; `review-notes/08-decisions.md`, “B5 review
decisions” D12 and “B5 remediation recheck decisions” D18(b).

No production change. Two new real PostgreSQL cases prove the shared picker and
correction rule for ended memberships: joined1 second after upload is absent from
the picker and refused with the exact existing message and unchanged full evidence
state; joined exactly at upload is selectable and accepted with exact participant,
character, team, original SubmittedAt and incremented version. Both assert the
persisted JoinedAt/LeftAt boundaries exactly; their timestamps derive from the
persisted submission's deterministic fixture timestamp plus whole seconds, so
PostgreSQL precision is respected. Both memberships end2 seconds after upload.

D18(b) is preserved: **current members count whenever they joined**. No test implies
a join-time restriction on current members. D12's former-member rule remains
JoinedAt <= SubmittedAt and LeftAt >= SubmittedAt; the latest Playing-role rule is
unchanged. Prior relevant membership/role passes remain applicable.

No existing test, expectation or assertion changed. Initial compilation failed
CS0246 for the new file's missing `Bingo.Domain.Access` import (`OsrsCharacter`).
Adding that import changed neither setup nor assertions; the same command passed.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2FormerMemberJoinBoundary' --logger 'trx;LogFileName=item5.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
git diff --check
```

PASS: Integration2, 0 failed, 0 skipped; final compilation without warnings/errors;
diff check passed. Isolated Testcontainers PostgreSQL only. Claude recheck and
binding pending; final batch gate follows item6's documentation commit.
