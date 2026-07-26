# Slice 1 Manual Test Results

Use `MANUAL_TEST_CHECKLIST.md` for the authoritative test steps and expected behavior.

## Test run

- Date:
- Revision / working tree:
- Tester:
- Browser / device:
- Database / scenario:
- Overall result:

Use `Not run`, `Passed`, `Failed`, `Automated`, `Postponed`, or `Blocked` in the Result column.

- `Automated` means the completed automated suite already covers the check; no manual repetition is needed.
- Start with rows marked `Manual priority`.
- Rows marked `Optional spot-check` can be skipped unless a related problem appears.
- The detailed steps and exact expected behavior remain in `MANUAL_TEST_CHECKLIST.md`.

| ID | Test | Result | Notes |
|---|---|---|---|
| S1-01 | Check an old database before upgrading | Automated | Automated coverage passed; no manual action needed. |
| S1-02 | Choose the owner when upgrading old data | Passed | — |
| S1-03 | Convert old captain logins safely | Automated | Automated coverage passed; no manual action needed. |
| S1-04 | Set up a brand-new database | Automated | Automated coverage passed; no manual action needed. |
| S1-05 | Sign in with Discord for the first time | Passed | — |
| S1-06 | Finish creating a new account | Passed | — |
| S1-07 | Check the password-length rules | Passed | — |
| S1-08 | Try a username that is already taken | Automated | Focused Discord-onboarding collision coverage retains the submitted values, preserves the existing account, creates no duplicate, and permits a different username. |
| S1-09 | Sign in to the same account with Discord and password | Passed | — |
| S1-10 | Check safe wrong-password messages and login limits | Passed | Manual check passed; automated coverage also passed. |
| S1-11 | Check how long login and Remember me last | Automated | Focused automated coverage verifies the 12-hour and absolute non-sliding 30-day ticket limits; no manual wait is needed. |
| S1-12 | Check old sessions after changing a password | Passed | Current password session remained signed in; prior password sessions were rejected and the independent Discord session remained valid. |
| S1-13 | Check maximum login time and separate login limits | Automated | Duplicate of S1-10/S1-11; their automated coverage removes the need for a separate manual run. |
| S1-14 | Open the Forgot password page | Passed | — |
| S1-15 | Let an Admin create a User reset link | Passed | Authorized User reset link rendered and was usable. |
| S1-16 | Let the owner create an Admin reset link | Passed | Retest passed after the one-time link was scoped to its intended account and purpose. |
| S1-17 | Use a password-reset link | Passed | Generated link was consumable and reuse failed safely. |
| S1-18 | Disconnect Discord from an account | Passed | Account settings was discoverable and unlink succeeded. |
| S1-19 | Connect or change Discord on an account | Passed | Relink retest passed after the OAuth transition was changed to native browser navigation. |
| S1-20 | Check other sessions and account history after changing Discord | Passed | Relink/session behavior passed. |
| S1-21 | Give and remove Admin access | Passed | Super Admin sees Admin tools and the Admin notification view. |
| S1-22 | Check old sessions after role or owner changes | Automated | Focused coverage verifies grant/revoke and ownership-transfer session invalidation, transfer confirmation, and the access-changed sign-in-again outcome. |
| S1-23 | Keep exactly one owner during simultaneous transfers | Automated | Automated coverage passed; no manual action needed. |
| S1-24 | Disable and restore accounts with the correct permissions | Passed | Manual priority |
| S1-25 | Check what is kept after disabling and restoring an account | Passed | The account is still available when disabled. What else to check for? |
| S1-26 | Confirm there are no merge or permanent-delete buttons | Passed | As far as i can see |
| S1-27 | Create an emergency captain login | Passed | Manual priority |
| S1-28 | Set up and enable an emergency captain login | Passed | Manual priority |
| S1-29 | Check emergency access at the submission deadline | Automated | Automated coverage passed; no manual action needed. |
| S1-30 | Search, filter, and page through both account lists | Automated | Automated coverage passed; no manual action needed. |
| S1-31 | Check website-account details and hidden secrets | Passed | - |
| S1-32 | Check emergency-account details and available actions | Passed | Manual priority |
| S1-33 | Search, filter, and page through the audit log | Automated | Automated coverage passed; no manual action needed. |
| S1-34 | Check which login and security actions are recorded | Passed | Optional spot-check |
| S1-35 | Check notifications after Admin and restore changes | Passed | Notification overview and empty-state retest passed. |
| S1-36 | Recover the owner from the command line | Automated | Automated coverage passed; no manual action needed. |
| S1-37 | Check success and error messages without JavaScript | Passed | Representative feedback and fallback checks passed. Field-specific or form-level feedback appearing inside the affected element is intentional. |
| S1-38 | Check desktop, mobile, keyboard, and focus behavior | Passed | Accepted as fully passed. |
| S1-39 | Check English and Danish text | Passed | Retested account navigation, field labels, and representative feedback successfully. |
