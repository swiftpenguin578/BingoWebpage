# B-M2 Danish payload followup

The A-M2 runtime-localizer audit detected `SharedResource: Confirmed` in the existing Signup JS payload after Accounts/Audit passed. Added the page-qualified resource key `SignupSetup.Confirmed` with `Bekræftet`, consistent with the existing lowercase resource. The audit is unchanged; no page exception. This direct Settings translation correction is isolated from other findings.

The initial capitalized key produced MSB3568 because resource compilation treats it as a duplicate of lowercase `confirmed`. The page-qualified key removes that collision while keeping the public JS label key `Confirmed`. No casing collision is suppressed.
