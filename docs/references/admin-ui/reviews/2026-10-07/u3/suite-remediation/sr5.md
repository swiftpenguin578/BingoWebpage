# SR5 — restore retired Participants handler selection

Restored the `SignupAdministration` POST stub, returning the pinned302 Manage redirect without mutations. Restored its original `Setup` handler classification and the corresponding exact reflection expectation, preserving global authorization, hidden/discarded and lifecycle gates. The C11 regression test is unchanged.

Release checks: real-PostgreSQL C11FinalizedRosterIntegrationTests **60/0/0** plus AdminEventHandlerClassificationTests **21/0/0**, combined **81 passed /0 failed /0 skipped**, exit0; [exact results/times](sr5.json). Existing C11 payment-through-final-review/finalized/discarded and auth/antiforgery checks pass. `git diff --check` passed.

No page JS or Razor markup changed in SR1/3/4/5, so no fullJS repeat is needed under this assignment. Release test invocations built the changed C#/resources. Prior116/0 JS evidence remains historical evidence, not a fresh broad pass. No whole.NET or flake repair.
