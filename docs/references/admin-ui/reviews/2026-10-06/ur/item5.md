# UR item 5 — documentation and stop handoff

README now owns complete create/refresh/stop commands, live/final-review profiles,
fixed ports/container/database, owned storage/marker refusal boundaries, local WOM
fake, synthetic credentials and all 11 accounts. It describes the 21-event scenario
set and links to the generated per-run guide. DELIVERY_PLAN UI pass rule8a requires
every later page brief to add its required states and working account links to UR.
CURRENT_STATUS records completion, exact execution identities and the next owner.

Items 1–4 are committed at `2c14bb1`, `90c67e4`, `ff807ce`, `48d3895`; this commit
is item5. Scoped documentation consistency/reference checks and `git diff --check`
passed; frozen reference tokens.css/components.css and legacy seeder unchanged.
Item5 changes only README, DELIVERY_PLAN, CURRENT_STATUS and this evidence file;
passing implementation gates from item4 are reused. No new baseline suite or
independent self-review was run for documentation.

Required next action: Claude/user independently reviews the final reported SHA,
runs the **unfiltered** whole .NET suite with **0 failed / 0 skipped**, and the user
walks through the isolated review environment. Whole .NET is **pending**, not
claimed passed. Exact command from the assigned checkout:

```sh
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-ur-final-suite-trx --logger "trx"
```

User shell/Identity acceptance remains the supplied 6 October decision. Supplied
planner source dates labelled 7 October remain attributed; this evidence uses
actual client date 6 October. CI and UR walkthrough are unrun; no later UI binding,
lane T, migration rehearsal, push, merge or deployment. Owned review environment
is stopped; existing user databases/processes were untouched. Stop/report after
this local commit, with its SHA delivered through the assigned dispatcher callback.
