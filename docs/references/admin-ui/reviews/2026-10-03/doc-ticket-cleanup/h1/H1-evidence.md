# H1 durable evidence checkpoint

Date: 3 October 2026

This checkpoint preserves compact historical evidence from the temporary roots
listed in the cleanup brief, using Claude's backup where available. The copied
files are evidence records, not a new test run or an independent review. Text
was inspected for credentials and real participant data before inclusion.

## Durable copy

The selected source evidence contains 61 files and 134,376 bytes. Including
this manifest and the sanitized manual-demo note, the H1 addition is 63 files
and 137,885 bytes:

- `participants/`: 28 files, 13,744 bytes. This includes all available `.meta`
  execution records from the backup and five final compact result summaries.
- `dashboard/`: 3 files, 12,947 bytes. These are the backend handoff and final
  review metadata records. The backup had TRX files but no separate compact
  summaries; TRX artifacts were omitted.
- `luck/`: 28 files, 96,342 bytes. This includes the approved/queued plans,
  review verdicts, handoffs, result summaries and hash manifests selected from
  the Luck evidence, remediation and recovery backups.
- `provenance/`: 2 files, 11,343 bytes. These are the explicitly requested AU04
  `review-initial.md` and AU09 `initial-review.md` provenance reports.

For the backup inventory, the source/selected counts were: Participants 87/28
files (274,270/13,744 bytes); Dashboard 23/3 (273,077/12,947); Luck evidence,
remediation, final-remediation and recovery 125/28 (1,073,543/96,342); the two
standalone Luck plans 2/2 (36,683/36,683); and the AU04/AU09 provenance reports
2/2 (11,343/11,343). The disposable manual-demo folder had 17 files and
2,009,409 bytes; no raw file from it was copied.

The source folders were present in Claude's backup for Participants, Dashboard,
Luck evidence/remediation/final-remediation, Luck recovery and the manual demo.
The live temporary roots were not used for writes. Standalone Luck plan files
were present in the backup and copied. No listed source folder was missing from
the backup.

## Omitted material

- All build output, DLL/PDB files, package/cache directories and source/demo
  projects were omitted.
- All TRX result artifacts, large logs, intermediate diagnostics, failed/retry
  captures and raw diff files were omitted when a compact summary or metadata
  record covered the result.
- The manual-demo route HTML, JSON/stat payloads, headers and web log were
  omitted because they may contain runtime or participant-shaped data. Its
  cleanup outcome is recorded in `manual-demo-sanitized.md`.
- Manual-demo metadata containing a local password, fixture IDs,
  process/container IDs and a stop-file path was not copied. No credentials or
  real participant data are present in this durable set.

## Source boundaries

Participants and Dashboard records preserve their original command/result
metadata, including the historical temporary result-directory names; the
owning active-document pointers now target this durable copy. The copied Luck records include prior
review limitations, including the earlier Node runtime limitation and the
recorded independent-review dispositions. Those historical outcomes are not
recast as fresh execution by this checkpoint.

## H1-4/H1-5/H1-6 correction — 4 October

The original counts above describe the 3 October copy only. Two small Participants
logs are now retained byte-for-byte from `participants-backend-remediation-20260930`
in the private evidence backup: `web-build-after-test-fixes.log` (0 warnings/errors)
and `ehb-correction-proof-parsed.log` (1/1). Their paths are `participants/` here.
Raw TRX files remain omitted, including the historically reported Dashboard 9/9
fixturefixed result; no claim that its raw evidence is retained is made.

Two flat-name collisions are restored in source-named subfolders of `luck/`:
- `luck-redesign-remediation-evidence-20261001/candidate-manifest.txt` preserves
  that earlier manifest; the flat `candidate-manifest.txt` is the Sol-recovery copy.
- `luck-redesign-final-remediation-20261001/scoped-diff-check.txt` preserves that
  empty successful-check artifact; the flat file is the Sol-recovery copy.
Both restored files match the corresponding backup source byte-for-byte. This
source mapping disambiguates them without changing the final recovery identity.
The demo note now preserves the inspected route and aggregate fixture values.
