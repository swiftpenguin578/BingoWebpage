# A-L3 — retain the display timezone in readback

Schedule snapshots preserve `displayTimezone` independently of the stored timezone ID. Older payloads retain their existing timezone as the display default.

`schedule-readback.transport.js`: PASS, including a real GET for stored `Review/Unknown` with display `UTC`, both in the immutable baseline and returned current snapshot. Existing precision/version/read-only recovery assertions remain passing. `git diff --check`: PASS.
