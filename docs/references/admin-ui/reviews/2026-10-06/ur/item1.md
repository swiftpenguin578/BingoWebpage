# UR item 1 — isolated command checkpoint

Added `scripts/ui-review.py` create/refresh/stop with fixed loopback ports, owned
container label/ID and local/PostgreSQL markers, refusal of appsettings.Local
database names, matched process identities and local WOM enforcement at startup.
Storage lives under gitignored `artifacts/ui-review/`. No user databases or
containers were touched.

Checkpoint dependency: the scenario command is supplied by item 2; real create /
refresh execution and date proof are deferred to item 4. Applicable command syntax,
safety boundary and Release checks are recorded below before commit. Independent
review and user walkthrough remain pending.

Checks executed 6 October 2026: Python CLI help and owned name/ID/label
rejection, occupied foreign port refusal and forced-fake environment assertions
passed. Release solution build: 0 warnings / 0 errors. `git diff --check` passed.
No runtime create/refresh claim at this dependent checkpoint.
