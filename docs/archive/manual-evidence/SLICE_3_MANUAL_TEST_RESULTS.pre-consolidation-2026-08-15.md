# Slice 3 Manual Test Results

**Status:** Pass 3.5 manual acceptance and all Slice 3 manual acceptance are approved. Slice 3 is ready for one bounded independent review but is not yet accepted.

## How to test the final lifecycle actions

Use an enabled Admin account and ordinary browser forms. Repeat the same checks once with JavaScript disabled where noted. Do not use Development fixtures as evidence for Production current-event rules.

| ID | Journey | Expected result | Result |
| --- | --- | --- | --- |
| S3-15 | Discard an empty but setup-heavy Draft | Confirmation is required. The event leaves Admin/public lists, its old link returns not found, and creating another event with the same link is rejected. | Passed: “Event discarded.” appeared exactly once on Admin Events. |
| S3-16 | Attempt discard with a participant, team, event credential, submission, or evidence | Discard is rejected with readable guidance to cancel; the event and all setup/history remain unchanged. | Passed |
| S3-17 | Cancel a populated Draft that was never public | Confirmation and a written reason are required. Admin history remains; the public event link returns not found and no reason is exposed. | Passed |
| S3-18 | Cancel a previously public pre-live event | Existing public signup information remains read-only with the generic “Event cancelled” status. The private reason appears only to Admins. Owned active participants receive a generic personal notification without the private reason. | Passed. Explicitly account-owned confirmed/waiting participant notifications are accepted as Automated for Slice 3 because the retained pre-Slice-4 signup UI creates unowned participants. |
| S3-19 | Archive a Finalized event | Confirmation is required. The event disappears from current navigation, appears under Previous events, and its board/team/tile/result URLs remain unchanged and read-only. | Passed |
| S3-20 | Reopen an Archived event with no competing current event | Confirmation and a reason are required. The event returns to Final review and prior official snapshots remain in history. | Passed |
| S3-21 | Reopen an Archived event while another event is Live, in Final review, or Finalized | The action is rejected with the competing event named; neither event nor official history changes. | Automated |
| S3-22 | Repeat destructive actions and wait past old scheduled times | No duplicate transition/audit appears, and Cancelled, Discarded, or Archived events do not reopen, start, or end automatically. | Passed |
| S3-23 | No-JavaScript and enhanced-form agreement | Discard/cancel/archive/unfinalize work through ordinary forms. Enhanced responses and fresh GETs show the same lifecycle state and available controls. | Passed |

## Already accepted

S3-11, S3-12, S3-13, S3-14A, and S3-14B passed. Pass 3.4 is approved.

## Acceptance boundary

Pass 3.5 manual acceptance and all Slice 3 manual acceptance are approved. Slice 3 is ready for one bounded independent review but is not yet accepted. Preserve the earlier `394/394` result and later focused remediation results; the complete suite has not been rerun. Recommended next action: independent review, concrete remediation only, then final complete suite and acceptance/commit/push.
