# SR3 — retain legacy-shell coverage after Schedule binding

A10: Participants replaces Schedule as the still-legacy page in the three named shell tests, including the matching model-type assertion. All original HTML, Danish terminology, cookie/security/session and opt-in assertions remain. No production change.

Release real-PostgreSQL AdminDesignShellIntegrationTests: **26 passed /0 failed /0 skipped**, exit0; [exact results/times](sr3.json). The first run passed25 and exposed the same stale ScheduleModel Assert.False; corrected its subject to ParticipantsModel, then the complete class passed. `git diff --check` passed.
