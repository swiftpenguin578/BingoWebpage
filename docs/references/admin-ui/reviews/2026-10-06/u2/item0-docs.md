# U2 item 0 — authority bindings

Assigned baseline: clean `codex/participants-functionality` at
`8355680a4eee6a74ae905c5c69a8f50c5f021dcf`. Implementer only, brief67 items 0–5.

## Changes

- PRODUCT_REQUIREMENTS Events-directory rule now states the A-Events-4 exception:
  Cancelled shows the retained confirmed count at cancellation. Actual-interval
  rules for Live/past participation are unchanged.
- DELIVERY_PLAN U2 batch is authorized/in progress; U3+ and lane T remain stopped.
- Existing A-Events-3 register row clarified: reference Events.dc.html silently
  drops invalid non-hidden link parts; the decided short notice also covers those.
- Added U2-5 partial-attention banner, retaining usable rows.
- Added already-decided reference differences concretely identified by inventory
  42b: Live provisional rows/count wording (D-8), current/overdue/unset card values
  (D-9…11), shared winners (D-12), signup-opening failure / Start postponed (E-8),
  and ambiguous Create 404 recovery (E-14, brief67 §2). No new behavior decision.
- U2-1 needs no missing-reference row: Dashboard.dc.html:631 chart tooltip,
  :637 recap facts and :647 recap winner subline already show team counts.
  Its backend work remains item 1, including history contract data.

## Checks and acceptance

Scoped consistency check against brief67 §2/item0, inventory42b D-8…12/E-3/E-8/E-14
and decisions08 A-Events-3/4 plus U2-1/5: passed. `git diff --check`: exit 0.
Reference team-count locations verified directly. No runtime or
.NET checks required for this documentation-only item. No independent source
review or manual acceptance claimed. Dashboard/Events binding remains awaiting
Claude review, then user visual acceptance; owning matrix rows are updated with
their page implementation. Frozen references and CSS are not edited.
