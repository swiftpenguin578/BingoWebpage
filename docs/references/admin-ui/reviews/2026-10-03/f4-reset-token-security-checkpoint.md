# F4 reset-token security evidence

Date: 2026-10-03

Reset links now retain their original issuer and recipient policy boundary. Role
grant/revoke, account disable, and ownership-transfer transactions supersede all
unused `Reset` tokens for affected accounts. Consumption locks the recipient and
issuer account rows in ID order before locking the token row, then checks the
recipient is an active non-SuperAdmin website account and that the recorded issuer
is still active and authorized to issue that reset now. Owner-recovery handling is
unchanged apart from the same ordered recipient lock.

The concurrency tests use independent PostgreSQL connections and force both lock
orders. When the role mutation wins, consumption observes the new authority and
fails without a password-reset success audit. When a valid consumption commits
first, the later role mutation completes without inventing a retroactive refusal;
the consumed token remains used rather than being relabeled as superseded.

## Executed checks

- `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --filter FullyQualifiedName~ResetConsumption --logger "console;verbosity=minimal"`
  — passed 7 tests against controlled PostgreSQL, covering recipient grant,
  disable, ownership transfer, issuer revoke/disable, a normal Admin reset, and
  recipient/issuer races in both lock orders.
- Existing focused compatibility filter for the HTTP reset flow, concurrent
  single-use consumption, owner recovery, and token expiry/supersession — passed
  4 tests against controlled PostgreSQL.

No provider calls, user-owned databases, participant data, or secrets were used.
Independent source review remains assigned to Claude.
