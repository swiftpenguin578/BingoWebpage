# Codex brief: B3, AU20 (WOM configured window, early end, Resume, retries, fallback)

Status: **draft by Claude (planner), 4 October 2026. Authorized only when the user sends it.**
- **Authorized:** AU20 alone.
- **Still stopped:** B4–B5, UI integration, RC tickets, rehearsal tooling, push, merge to `main` and deployment.

## 1. Authority and baseline
- **Checkout:** `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`. Start from `b126c55` (B1 and B2 merged, scope check PASS) with a clean tree. If the branch has moved, stop and report.
- **Ticket:** `DELIVERY_PLAN.md` "AU20 — configured-window and end synchronization" (`:453-506`). This brief doesn't repeat it: every numbered point 1–7 and the "Focused acceptance" list are required.
- **Decisions:** `/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md`:
  - "WA-2 (decided, extends AU20)", items 1–7, including the corrected Resume rule;
  - D4 under "Step 4 cleanup decisions";
  - D2 under "B1/B2 lane review decisions".
- **Findings:** WA-2, WA-5 (option 1), WA-6 and WA-9 in `06b-wom-catalogue-accounts-audit.md`.
- **Conflicts:** where the ticket and `08-decisions.md` differ, `08-decisions.md` wins. Report the difference.
- **Approvals:** follow AGENTS.md. Record an approval as the user's only when it quotes or links the user's message.

## 2. What already exists that AU20 must build on
AU18 (B2) already touched the WOM sync service; the user approved this after review (D2):
- `EventCompetitionRefreshResult` has an optional `SkipReason`.
- `EventCompetitionRefreshSkipReason` has explicit values 0–7: EventUnavailable, EventNotInFinalReview, IncompleteEventWindow, NoCompetition, RefreshInProgress, RetryDelay, NotDue, ServiceUnavailable.
- The private `AcquireLeaseAsync` returns a `LeaseDecision` (lease, reason, next eligible time).

Required:
- **Extend this mechanism**; don't build a second one.
- **The enum is stored data:** its names are kept in published versions' `CalculationInputsJson`. Never rename, remove or renumber a member. Add new members only at the end, with a new explicit number.
- **Keep reading AU18's stored format** for versions already published.
- **Manual Fetch wording:** the WOM page's Fetch (AU15) keeps its generic cooldown wording (D4). AU20 adds the structured reasons, next permitted time and outcomes to the service result and readback. Showing them in the new UI is UI-integration work.

## 3. Work items, in order, one local commit each
1. **Exact configured window everywhere (ticket points 1 and 6).**
   - Replace the 5-minute tolerance at the three sites: shared link/replacement/fetch validation, Schedule save, and Resume.
   - Compare WOM start/end exactly with the configured UTC start/end at every stage, Final Review included.
   - Actual times keep driving upload cutoff, drop eligibility and the review window.
   - Add the structured eligibility/reason/next-permitted-time/credential/operation outcomes (WA-6) on the AU18 mechanism.
2. **Early end and Resume (points 2 and 3).**
   - Both always succeed locally; a WOM failure never blocks them.
   - **Early end:** configured end = click time rounded up to the next whole minute (21:59:55 → 22:00:00); the WOM end gets the same value; actual end keeps the precise click time.
   - **Resume:** uses the admin's validated future replacement end, never the click time, with no rounding.
   - **WOM update:** both record a pending WOM end update, which item 3 carries out.
3. **WOM end update with spaced retries (point 4).**
   - **When:** the existing management worker (or the existing operation mechanism) performs the update with spaced backoff.
   - **Schedule (planner technical choice):** retry after 1, 2, 4, 8 and 16 minutes, then every 30 minutes, until results are published.
   - **Concurrency:** never a fast loop, and never two update attempts for the same event at once.
   - **Errors:**
     - HTTP 400 `COMPETITION_START_DATE_AFTER_END_DATE` is non-transient; stop retrying.
     - A missing or invalid verification code is non-transient.
     - Other 4xx: report to `/root` before classifying.
   - **Guards:** keep every existing lease, operation and history guard.
4. **Unmatched end: fetch suppression and publication fallback (point 5).**
   - **While unmatched:** while the WOM end doesn't match the configured end, no fetch runs after the actual end. That covers the scheduled, manual and final fetch.
   - **After a successful update:** normal fetches resume with the right window.
   - **If still unmatched at publication:**
     - Persist the event state "WOM end could not be updated".
     - Skip the final fetch, recorded in AU18's outcome with a new reason added at the end of the enum.
     - The last fetch before the actual end is the official WOM data.
     - Luck shows that fetch's age; no invented fresh values or zeros. Check that Luck already does this; change Luck only if it doesn't, and report it.
   - **ID-only external links** (no verification code) always take this fallback after an early end.
   - **Display (user decision, 4 October):** backend only. Persist the state and expose it in the WOM and Final Review read models/service results. **Don't add it to the current pages.** The new UI shows it, and its placement is decided in the UI integration plan.
5. **External link option 1 and WA-9 (point 6).**
   - **Option 1:** external replacement before or during Live, and disconnection before first Live, follow approved option 1 (`DELIVERY_PLAN.md:1116`, WA-5). It applies regardless of a stored or rejected code. It keeps the exact-window rule and the active/unresolved-operation guards. It never reuses the old code and never deletes on WOM.
   - **WA-9:** external linking waits for a website Create that is Sending or Unknown, so its late completion can't overwrite a newly saved link.
   - **Readback** never performs a fetch, and an old success never proves a new operation.
6. **Documentation.**
   - **Remove the "pending AU20" markers:** update the early-end texts in DATA_MODEL, TECHNICAL_ARCHITECTURE and PRODUCT_REQUIREMENTS to the implemented behaviour.
   - **Record decisions:** the retry schedule and the new enum members.
   - **Design-reference gaps register:** add the row for "WOM end could not be updated" to the `DELIVERY_PLAN.md` register "Bindings not shown in the design references". It is backend only, and its placement is open, to be decided in the UI integration plan. Do the same for any other AU20 output the references don't show, such as structured fetch reasons.
   - **Accepted limit:** WOM end values come from each player's last snapshot inside the window (point 7). No compensation logic.

If any item needs a product decision, stop that item, send a proposal to `/root`, and continue with the next item that doesn't depend on it.

## 4. Files B3 owns
- WOM sync and management services and their workers.
- The admin WOM page, only where existing handlers must keep working; no new display on current pages.
- Event lifecycle early end, Resume and Schedule save.
- The Final Review/finalization WOM fetch and its outcome.
- Migrations: expected, for the pending-update and "could not be updated" state.
- Their tests, translations (`SharedResource*.resx`; B3 runs alone) and ticket docs.
- **Luck:** only the freshness display, and only if item 4 requires it.
- **Not:** Board, scoring, catalogue, draft, Accounts or Audit.

## 5. Proof
- The ticket's whole "Focused acceptance" list, with controlled provider doubles and never live WOM. Real PostgreSQL wherever persistence, timing precision or concurrency is involved.
- **Specifically:**
  - early end at 21:59:55, and early end exactly on a minute;
  - Resume with a future replacement end;
  - timezone-equivalent configured windows through Final Review;
  - each former tolerance site;
  - retry spacing measured with a controllable clock;
  - the non-transient 400;
  - no fetch after the actual end while unmatched, then a successful update and resumed fetches;
  - the fallback reached at publication, with Luck freshness;
  - an ID-only link after early end;
  - external delete refused;
  - the Create/link race in both orders;
  - a late provider response after replacement.
- **Migration:** Up on a populated database, plus a Down check; record any backfill.
- **Old AU18 data:** the AU18 enum test still passes, and an old published version still reads its stored outcome.
- **Checks:** Release build clean; `git diff --check`.
- **Evidence:** under `docs/references/admin-ui/reviews/2026-10-04/au-b3/`, one file per item.

## 6. Routing, stop and review
- **Planner:** UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59` (`/root`).
- **Implementer:** one, `gpt-6-astra` / `high`. No orchestrator, no self-review, no push.
- **Reporting:** report before each turn-ending response, and immediately for a blocker or product question.
- **Stop:** after item 6, for Claude's independent review (one reviewer agent per substantial item), with a remediation round if needed. Don't start B4.

---

### Planner's notes
- **Display of "WOM end could not be updated":** the user chose backend only (4 October). The reason: all functionality targets the new UI and is not merged to `main` before that UI exists, so showing it on today's pages would be wasted work.
- **Retry spacing** (1, 2, 4, 8, 16 minutes, then every 30 minutes) is a planner technical choice. It matches the user's WA-2 rule: spaced backoff, never a fast loop, and retries may continue until publication.
