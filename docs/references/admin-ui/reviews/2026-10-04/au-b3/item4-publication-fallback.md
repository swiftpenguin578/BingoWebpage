# B3 item 4 — unmatched end and publication fallback

Authority: supplied-brief.md §3 item 4 and §5; WA-2/WA-6 in the supplied decisions named there. Backend only; no new page display. Implemented and executed; external Claude review pending.

- Shared AU18 eligibility suppresses manual, scheduled and final fetches after actual end when the configured end is unmatched. An already-running fetch also cannot overwrite the pre-end cache after early end.
- Successful end synchronization resumes fetching with the configured window; cache generation remains intact for an end-only update.
- Publication persists CouldNotUpdate and records AU18 Skipped / EndCouldNotBeUpdated. New stored enum values append EndWindowUnmatched=8 and EndCouldNotBeUpdated=9; original names/numbers 0–7 remain unchanged.
- WOM and Final Review service/read models expose the persisted state. Finalization retains the pre-end WOM cache and official values, including ID-only and permanently rejected updates.
- Existing Luck checkpoint behavior retains original fetched/calculated timestamps and values and derives stale age. Executable PostgreSQL checks confirmed this; no Luck production change was required.

Checks: four PostgreSQL fallback/in-flight cases passed (pending, ID-only, rejected, in-flight); resumed-fetch case passed with item-3 backoff test; six existing old-AU18 published-outcome readback cases passed. Stored enum contract test passed (1). Release solution build passed, zero warnings/errors; git diff --check passed. Controlled doubles only, no live WOM or user-owned database. Initial test assertions incorrectly assumed board EHB=1 and record equality across deserialized list instances; corrected to compare prior official EHB and structural serialized Luck values, then all four passed. The unrelated StatsPass4Boundary fixture limitation remains as recorded in item 1.
