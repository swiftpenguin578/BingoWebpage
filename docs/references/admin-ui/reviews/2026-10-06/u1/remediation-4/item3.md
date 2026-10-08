# Round4 item3 — cancellation date

Authority: brief60 item3 / user's cancellation-date option a. GetAdminDesignAsync now projects CancelledAt and formats a Cancelled event as `on {date}` / `den {date}` in its stored timezone. Finalized/Archived still use `ended` with EventEndsAt. New scoped AdminDesign.on key has English/Danish values; no legacy wording changes.

Exact changed assertion: AdminDesignShellIntegrationTests terminal loop formerly expected `ended EventEndsAt` for all three states; now only Cancelled requires `on CancelledAt`, preserving exact prior expectations for Finalized/Archived. Authority brief60 item3 /59 N2; no eligibility/order/access assertions changed. That existing test passed.

New actual PostgreSQL/HTTP test persists cancellation at2026-10-04T22:30:00Z in Europe/Copenhagen, with planned end still in the future. Exact rendered metadata is `Cancelled · on 5 Oct` and `Aflyst · den 5 okt.`. Both executed cases **2/0/0** in final run. Initial test development corrected a span selector to the reference div and used the actual Accept-Language request provider rather than unsupported query-string culture; production expectations were not relaxed. Whole BrowserTests latest gate remains item2 **150/0/0**, rerun at final batch after remaining resource changes. Diff clean; final whole .NET remains Claude execution.
