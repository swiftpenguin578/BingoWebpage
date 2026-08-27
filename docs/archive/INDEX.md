# Historical archive index

Every archive entry must define these fields:

- Original path
- Archived path
- Archive date
- Source commit/hash
- Reason
- Current authority/destination
- Time range
- Retrieval note

## Entries

### 1. Pre-consolidation current status

- Original path: `CURRENT_STATUS.md`
- Archived path: `docs/archive/historical-handoffs/CURRENT_STATUS.pre-consolidation-2026-08-14.md`
- Archive date: `2026-08-14`
- Source commit/hash: working-tree commit `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; pre-rewrite SHA-256 `d64c628353ca37916dc3b71f6ea641cb8bf9112b6a6c8c7b1f9cfa7e5623938e`
- Reason: Preserve the exact dirty handoff before replacing the status diary with a concise active handoff.
- Current authority/destination: Current state and approval routing are promoted to [`CURRENT_STATUS.md`](../../CURRENT_STATUS.md); remaining delivery is promoted to [`DELIVERY_PLAN.md`](../../DELIVERY_PLAN.md).
- Time range: Through `2026-08-14` pre-consolidation.
- Retrieval note: Use only for an explicitly named provenance or manual-evidence investigation; it is historical and non-authoritative.

### 2. Pre-consolidation Admin UI contract

- Original path: `ADMIN_UI_CONTRACT.md`
- Archived path: `docs/archive/superseded-ui/ADMIN_UI_CONTRACT.pre-consolidation-2026-08-14.md`
- Archive date: `2026-08-14`
- Source commit/hash: working-tree commit `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; pre-rewrite SHA-256 `be0679c744604c0e1a75f26244e75e38631ea28b0e8462f15bb4e77f979f3360`
- Reason: Preserve the exact Admin visual contract before promoting durable global rules into the consolidated UI authority.
- Current authority/destination: Global rules are promoted to [`UI_SYSTEM.md`](../../UI_SYSTEM.md); page families, references, exceptions, and approvals are promoted to [`UI_PAGE_MATRIX.md`](../../UI_PAGE_MATRIX.md).
- Time range: Through `2026-08-14` pre-consolidation.
- Retrieval note: Use only for explicitly named provenance or migration evidence; it is historical and non-authoritative.

### 3. Pre-consolidation UI overhaul roadmap

- Original path: `UI_OVERHAUL_ROADMAP.md`
- Archived path: `docs/archive/superseded-roadmaps/UI_OVERHAUL_ROADMAP.pre-consolidation-2026-08-14.md`
- Archive date: `2026-08-14`
- Source commit/hash: working-tree commit `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; pre-rewrite SHA-256 `d3a93ee2b4e67a690820d5a2875cf20454e5483c37e250cf0613308b453ac950`
- Reason: Preserve the exact UI pass/history roadmap after promoting current page families and approval state into active authority.
- Current authority/destination: Page families, protected composition, current approval, and next gates are promoted to [`UI_PAGE_MATRIX.md`](../../UI_PAGE_MATRIX.md), [`CURRENT_STATUS.md`](../../CURRENT_STATUS.md), and [`DELIVERY_PLAN.md`](../../DELIVERY_PLAN.md); global rules are in [`UI_SYSTEM.md`](../../UI_SYSTEM.md).
- Time range: Through `2026-08-14` pre-consolidation.
- Retrieval note: Use only for explicitly named provenance or manual-evidence investigation; it is historical and non-authoritative.

### 4. Pre-consolidation functional workflow specification

- Original path: `FUNCTIONAL_WORKFLOWS.md`
- Archived path: `docs/archive/superseded-workflows/FUNCTIONAL_WORKFLOWS.pre-consolidation-2026-08-14.md`
- Archive date: `2026-08-14`
- Source commit/hash: working-tree commit `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; pre-rewrite SHA-256 `b9a0fd439e69aebfdcc52905d6d0af51ad85039745b4bbe78a2507077fef0a45`
- Reason: Preserve the exact mixed planning/workflow source before promoting final end-to-end journeys into the concise active contract.
- Current authority/destination: Final workflow journeys, actors, reachability, authority handoffs, failure/recovery behavior, and acceptance outcomes are promoted to [`FUNCTIONAL_CONTRACTS.md`](../../FUNCTIONAL_CONTRACTS.md); the root path is a non-authoritative historical tombstone.
- Time range: Through `2026-08-14` pre-consolidation.
- Retrieval note: Use only for an explicitly named provenance, migration, or manual-evidence investigation; it is historical and non-authoritative.

### 5. Completed implementation roadmap, slice plans, and manual evidence

- Archive date: `2026-08-15`
- Source working-tree/base identification: branch `codex/admin-ui-overhaul-v2`,
  working-tree `HEAD`/base `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; the
  checkout was intentionally dirty and each hash below was captured from the
  exact current working-tree bytes before removal.
- Reason: Preserve completed implementation history, planning records, and
  manual-acceptance evidence while removing superseded root-document clutter;
  these records are historical evidence, not active authority.
- Current authority/destination: Current rules and workflow contracts are in
  `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_CONTRACTS.md`, `DATA_MODEL.md`,
  `TECHNICAL_ARCHITECTURE.md`, `UI_SYSTEM.md`, and `UI_PAGE_MATRIX.md`;
  current status and sequencing are in `CURRENT_STATUS.md` and
  `DELIVERY_PLAN.md`; durable manual journeys remain in
  `MANUAL_TEST_CHECKLIST.md`.
- Time range: `2026-07-24` through `2026-08-11`, with the captured dirty
  working-tree state as of `2026-08-15`.
- Retrieval note: Use only for explicitly named provenance, migration, or
  manual-evidence investigation. Archived bytes are non-authoritative and
  must not override active documents.

| Original path | Archived path | SHA-256 |
| --- | --- | --- |
| `IMPLEMENTATION_ROADMAP.md` | `docs/archive/superseded-roadmaps/IMPLEMENTATION_ROADMAP.pre-consolidation-2026-08-15.md` | `8db2fb36412bcd065863560ce14e871e57c12b0c05d051ea21ebf8d8dce0bdd1` |
| `SLICE_1_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_1_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `c689aab70d79407f4cbcd914cb217fcfa5e8596c46fa9a1ab9c94688b11c8dbe` |
| `SLICE_2_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_2_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `f8cb416e667e8059962e3cb3fc8dc99c7634d35ee993d87069eb122d9d0ba3a0` |
| `SLICE_3_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_3_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `308f83e8740012792bb8c9d2e00a7a3859d723dd7d5d75cd435b31aa4d3f0ba5` |
| `SLICE_4_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_4_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `e60cc9cd970136d31a752ce73d3ccddfe9564bd31932f64c979dc1b8c577efd4` |
| `SLICE_5_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_5_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `8f0a8b8793c0a0f4984345e881d5c940dbdb40343e3639ec1c748bc66c1a0046` |
| `SLICE_6_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_6_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `43eda8902f5f4907d154a1b83d05eed62c81fa89f788a8e94cf41f470fd89fab` |
| `SLICE_7_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_7_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `71d45bc452cf4870c365f921b22c3bab3b4ae12cc8a77c16293d315d37cf4701` |
| `SLICE_8_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_8_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `0ebf35ded9a07d417bfea25cf6a44708891296d24099e917a52cd0ecd8d0ff28` |
| `SLICE_9_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_9_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `542e7375d5e57737149cc7dc24f2e062f3dc635a551794c29b88229e940afd51` |
| `SLICE_10_IMPLEMENTATION_PLAN.md` | `docs/archive/completed-slice-plans/SLICE_10_IMPLEMENTATION_PLAN.pre-consolidation-2026-08-15.md` | `236f6953c12eada433d31a9277c9cfaa7854dd7407cd6a96193316ba6493c1d4` |
| `SLICE_1_MANUAL_TEST_RESULTS.md` | `docs/archive/manual-evidence/SLICE_1_MANUAL_TEST_RESULTS.pre-consolidation-2026-08-15.md` | `75fbe36dbc05c09154d03031d6e7998b0947597d0581b640bf2b44561fa8fc8e` |
| `SLICE_2_MANUAL_TEST_RESULTS.md` | `docs/archive/manual-evidence/SLICE_2_MANUAL_TEST_RESULTS.pre-consolidation-2026-08-15.md` | `b1ddfb0196036870615175805d269b01672318615769b0bb22c71a8a9faa5df5` |
| `SLICE_3_MANUAL_TEST_RESULTS.md` | `docs/archive/manual-evidence/SLICE_3_MANUAL_TEST_RESULTS.pre-consolidation-2026-08-15.md` | `956dc4543d5e12cf0ce654763f0c744be8c23ece5a793d4324037729bf1c6f9c` |

### 6. Pre-consolidation Application Atlas Markdown and HTML

- Original paths: `APPLICATION_ATLAS.md` and `APPLICATION_ATLAS.html`
- Archived paths: `docs/archive/superseded-assessments/APPLICATION_ATLAS.pre-consolidation-2026-08-15.md` and `docs/archive/superseded-assessments/APPLICATION_ATLAS.pre-consolidation-2026-08-15.html`
- Archive date: `2026-08-15`
- Source working-tree/base identification: branch `codex/admin-ui-overhaul-v2`,
  working-tree `HEAD`/base `34bad2fee6c2f7196e4946e198e5eb186d87e36c`; the
  checkout was intentionally dirty and both hashes were captured before root
  removal.
- Reason: Retire the competing inventory/review presentation after routing
  every durable finding to its active owner or explicitly classifying it as
  archive-only; preserve the historical evidence and exact static companion.
- Current authority/destination: Workflow journeys/reachability are owned by
  [`FUNCTIONAL_CONTRACTS.md`](../../FUNCTIONAL_CONTRACTS.md); page families,
  composition, and approval by [`UI_PAGE_MATRIX.md`](../../UI_PAGE_MATRIX.md);
  architecture/security/persistence by [`TECHNICAL_ARCHITECTURE.md`](../../TECHNICAL_ARCHITECTURE.md)
  and [`DATA_MODEL.md`](../../DATA_MODEL.md); current status and order by
  [`CURRENT_STATUS.md`](../../CURRENT_STATUS.md) and [`DELIVERY_PLAN.md`](../../DELIVERY_PLAN.md).
- Time range: Atlas generated `2026-08-03`; preserved dirty working-tree
  bytes captured and archived on `2026-08-15`.
- Retrieval note: Use only for explicitly named Atlas provenance or manual-
  evidence investigation. The Markdown and HTML are historical,
  non-authoritative, and must not be used as active route, product, UI, or
  implementation authority.

| Original path | Archived path | SHA-256 |
| --- | --- | --- |
| `APPLICATION_ATLAS.md` | `docs/archive/superseded-assessments/APPLICATION_ATLAS.pre-consolidation-2026-08-15.md` | `f7b31fd1177cf2374fc5d10ec27aa767cda5c3e7f2bc40f05d3fd5010afc5101` |
| `APPLICATION_ATLAS.html` | `docs/archive/superseded-assessments/APPLICATION_ATLAS.pre-consolidation-2026-08-15.html` | `a6ea62317a82515d395e9fde8f32328f27782bff1bce53a9790f578550cc3ab5` |
