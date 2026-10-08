# Item2 — R12 unclassified Events pages fail closed

The filter returns404 for an unclassified model whose actual page path is under
`/Pages/Admin/Events/`, including subfolders and independent of model namespace.
Existing classified pages and unrelated pages keep their behavior. The classification
test now enumerates real `@page` files recursively and resolves their `@model` types,
so a nested or differently-namespaced model cannot silently evade the page-set check.

Assertion change: exact namespace-only model enumeration → recursive Razor page
inventory; all expected page/handler/gate/state assertions retained. Authority R12,
brief50 item2. New direct boundary tests cover unknown root/nested Events pages and
an unrelated page; no DB access is needed for refusal. Unchanged D16 test14 exercises
existing routes over real PostgreSQL.

Executed classification + test14 filter: **22 passed / 0 failed / 0 skipped**.
Command/log: `/private/tmp/bingo-u1-remediation-item2.log`, TRX beside it in
`/private/tmp/bingo-u1-remediation-item2/`. `git diff --check` clean.
Initial fixture constructor compile error corrected; no expectation changed for it.
Independent recheck and final whole-suite/user visual gates remain pending.
