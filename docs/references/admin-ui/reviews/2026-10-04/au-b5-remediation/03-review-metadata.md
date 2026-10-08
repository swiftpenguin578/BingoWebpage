# Item 3 — B1, S9 and B2

Backend remediation implemented; external Claude recheck and RC07 binding pending.
Authority: supplied brief35 item 3; report31b M1; `08-decisions.md` Reference
sweep decisions S9 (line 191). S9 clarifies that the dropped per-account context
was the prior-approved count, not the team-leave warning. No current UI changed.

Latest review action exposes nullable BeforeVersion/AfterVersion from existing
snapshots. Legacy missing values remain null; ordering still prefers versioned
new actions and omits tied attribution. This is current-state metadata, never a
request receipt, a success claim or retry instruction. Review detail exposes the
credited participant's latest leave time for this team; any current membership
makes that field null. No migration or roster rewrite.

New PostgreSQL proofs check versioned approval against the exact committed
submission version, a realistically legacy action history with no Version fields,
current/former leave-time detail, and exhausted/partial drop caps while the
objective has six remaining. Cap numbers are compared with actual approval or
exact no-write refusal. The register defers cap wording to RC07.
No existing test or assertion changed.

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationReview|FullyQualifiedName~B5RemediationContribution|FullyQualifiedName~B5Review|FullyQualifiedName~B5Contribution' --logger 'trx;LogFileName=item3-corrected.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Result: 13 passed, 0 failed, 0 skipped; diff check passed. First execution had
12 passes and 1 failure: the new legacy fixture retained a versioned creation
action, which correctly sorted ahead of an unversioned action. The setup now
models an entirely legacy history and gives its correction a later timestamp;
all assertions retained. Final unfiltered suite remains pending.
