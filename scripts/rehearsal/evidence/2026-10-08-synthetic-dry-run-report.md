# Local rehearsal report

- Outcome: **PASSED**
- Generated: 2026-10-08T16:19:07Z
- Candidate source SHA: `9c734a8f83a41480ab0c06e2952af7854941d2e4`
- Candidate image: `bingo-rehearsal-web:9c734a8f83a41480ab0c06e2952af7854941d2e4` id `sha256:498cf779de1f45d0e57bc74ce3188ef625a8910422aaceb20700509f8e2836d6` (OCI revision label `9c734a8f83a41480ab0c06e2952af7854941d2e4`)
- Dump SHA-256: `c1e38cbdb15ea6a23ea4ddfc002c1e87dd8e0e0d508dcd035a4d969fc61ca201` (502639 bytes)
- Fixture images: postgres `postgres:17-alpine@sha256:742f40ea20b9ff2ff31db5458d127452988a2164df9e17441e191f3b72252193`, S3 `versity/versitygw@sha256:30292fc2eeacc67a36993b01f7a7a5e3361a19cced0e80c1d71cfa2a4b0a2499`, aws-cli `amazon/aws-cli@sha256:48c3d4212e2f5b0e24bdc6af7708f9412ce65425a79575e0f78b8f8c0dcd70ab`
- Compose project: `bingo-rehearsal` (internal network, no published ports)

## Stages

| Stage | Status | Exit | Started (UTC) | Finished (UTC) |
| --- | --- | --- | --- | --- |
| 1-build-candidate-image | passed | 0 | 2026-10-08T16:17:41Z | 2026-10-08T16:17:44Z |
| 2-isolated-project | passed | 0 | 2026-10-08T16:17:44Z | 2026-10-08T16:17:59Z |
| 3-restore-dump | passed | 0 | 2026-10-08T16:17:59Z | 2026-10-08T16:18:01Z |
| 4-gate-counts | passed | 0 | 2026-10-08T16:18:01Z | 2026-10-08T16:18:09Z |
| 5-migrate | passed | 0 | 2026-10-08T16:18:09Z | 2026-10-08T16:18:17Z |
| 6-convert-luck-checkpoints | passed | 0 | 2026-10-08T16:18:17Z | 2026-10-08T16:18:23Z |
| 7-production-preflight | passed | 0 | 2026-10-08T16:18:23Z | 2026-10-08T16:18:29Z |
| 8-web-health | passed | 0 | 2026-10-08T16:18:29Z | 2026-10-08T16:19:06Z |
| 9-stop-and-report | passed | 0 | 2026-10-08T16:19:06Z | 2026-10-08T16:19:07Z |

## Recorded facts (aggregates only)

| Key | Value |
| --- | --- |
| dump.sha256 | c1e38cbdb15ea6a23ea4ddfc002c1e87dd8e0e0d508dcd035a4d969fc61ca201 |
| dump.bytes | 502639 |
| candidate | 9c734a8f83a41480ab0c06e2952af7854941d2e4 |
| candidate.migrations | 85 |
| exit.01-build | 0 |
| image.tag | bingo-rehearsal-web:9c734a8f83a41480ab0c06e2952af7854941d2e4 |
| image.id | sha256:498cf779de1f45d0e57bc74ce3188ef625a8910422aaceb20700509f8e2836d6 |
| image.revision | 9c734a8f83a41480ab0c06e2952af7854941d2e4 |
| exit.02-certs | 0 |
| isolation.compose | internal network only; no ports; no host network; read-only binds; no production paths/env files |
| exit.02-up | 0 |
| exit.02-egress-deny | 0 |
| isolation.egress | no default route; 198.51.100.1 unreachable |
| exit.02-wom-refusal | 0 |
| fixture.wom | refusal fixture returns 503 for every request |
| exit.02-s3-fixture | 0 |
| fixture.s3 | HTTPS (fixture CA, validation on), path-style, region auto; HeadBucket ok |
| exit.03-restore | 0 |
| history.before.count | 71 |
| history.before.manifest-match | identical to supplied migration-history.txt |
| migrations.pending | 14 |
| gates.check_time_utc | 2026-10-08T16:18:01Z |
| gate.au20.awaiting_final_review | 0 |
| gate.drop_tile_ehb_overrides.before | 1 |
| gate.luck_v1.before | 1 |
| gate.completion_corrections | 0 |
| gate.published_boards_without_active_roster | 0 |
| gate.future_effective_switches | 0 |
| gate.g4_setup_with_first_pick.before | 0 |
| gate.cat1_conditional_on_parent | 0 |
| gate.cat1_non_default_context | 0 |
| gate.banner_assets | 0 |
| gate.banner_cleanups | 0 |
| gate.banner_event_refs | 0 |
| gate.active_super_admins | 1 |
| events.by_state.before | Archived=1 |
| historical.events.before | 1 |
| historical.audits.before | 1 |
| exit.04-premigration-clone | 0 |
| premigration.clone | bingo_rehearsal_premigration (71 history rows) |
| exit.05-web-init | 0 |
| exit.05-migrate | 0 |
| history.after.count | 85 |
| history.after.matches_candidate | yes (all 85 candidate migrations applied, none extra) |
| gate.drop_tile_ehb_overrides.after | 0 |
| migrate.ehb_overrides_cleared | 1 (before 1, after 0) |
| gate.g4_backfill | eligible set == requires_fresh_order set (0 rows) |
| gate.g4_ineligible_default_false | 1 |
| historical.events.after-migrate | 1 |
| historical.audits.after-migrate | 1 |
| exit.06-convert | 0 |
| luck.summary | Luck checkpoint conversion summary: Converted=1; Already converted=0; Could not convert=0. |
| gate.luck_v1.after | 0 |
| gate.luck_converted_from_v1 | 1 |
| historical.events.after-convert | 1 |
| historical.audits.after-convert | 1 |
| exit.07-preflight | 0 |
| preflight | Production preflight passed. |
| events.by_state.before-web | Archived=1 |
| exit.08-up-web | 0 |
| web.container_health | healthy (Docker healthcheck = --health-probe) |
| exit.08-health-probe | 0 |
| web.health_probe_exit | 0 |
| exit.08-http-health | 0 |
| web.health_live | 200 |
| web.health_ready | 200 (includes storage HeadBucket and fresh lifecycle + competition-sync worker heartbeats, max age 90 s) |
| exit.08-http-health-again | 0 |
| web.health_ready_after_20s | 200 |
| web.log_errors | 0 error/critical lines in last 400 (redacted copy: logs/08-web-startup.redacted.log) |
| events.by_state.after-web | Archived=1 |
| exit.09-down | 0 |

## history-before (71 entries)

```
20260711062109_InitialFoundation
20260711155515_AddIdentityAndAudit
20260711172420_AddEventsAndSignups
20260711185809_AddCatalogueAndBoardFoundation
20260711191039_AddBoardSnapshots
20260711214333_AddCatalogueImages
20260712192541_SeedWiseOldManBossRates
20260712210645_SeedBarrowsAndLunarDrops
20260712212313_AddTeamsAndDraft
20260712222102_AllowReusingUndoneDraftPickNumbers
20260713101510_AddEvidenceSubmissionAndReview
20260713134514_LinkGeneratedCaptainAccounts
20260713145517_AddSubmissionPrivacyRequests
20260713162433_AddAdminConcurrencyHardening
20260713175710_AddBoardEditingLease
20260713181525_AddEventFinalizationWorkflow
20260720005059_AddSourceDropRateVariants
20260721232916_AddBoardEditorAndCatalogueRateMechanics
20260724223331_AddSlice1IdentityFoundation
20260725170951_AddEmergencyLifecycleAndPersonalNotifications
20260726195230_AddSlice2PersistenceFoundation
20260726201926_TransitionParticipantCharacterAuthority
20260727090952_AddSlice3LifecyclePersistenceFoundation
20260727094645_AddSlice3GuidedCreationAndIdentity
20260727104443_AddSlice3ScheduleReadinessAndSignupLifecycle
20260727121333_AddDevelopmentFixtureLifecycleExemption
20260727134959_AddScheduledLifecycleExecution
20260727192755_AddEventBannerCleanupOutbox
20260727223107_AddSlice4SignupPersistenceFoundation
20260728135529_RemoveSignupCompatibility
20260728143114_AddParticipantResponseRevisionAndProtectLegacyDiscord
20260728143651_AddSignupAnswerIntegrityRelations
20260728145446_NormalizeRetainedLegacyDiscordSystemField
20260729132140_AddSlice5PersistenceFoundation
20260729172938_AddFrozenPublicationIdentity
20260729223403_AddSlice6BoardApprovalSnapshotFoundation
20260729231001_ReconcileSlice6CatalogueModel
20260729234011_AddSlice6ManagedBoardTileImages
20260730160029_RemoveSlice6RateVariants
20260730183704_AddPublishedBoardCorrectionFlag
20260730212304_AddSlice7LiveAccountAndTeamFocusFoundation
20260731170051_RemoveTeamFocusEventTeamAlternateKey
20260731180603_NormalizeCompletedTileFocusMarkers
20260731204600_AddSlice8EvidenceFoundation
20260801160152_RemoveDeprecatedEvidenceCompatibility
20260802000213_AddAuthoritativeEventEndPersistence
20260802002639_AddLiveWithdrawalReplacementPersistence
20260802005536_AddFinalReviewCyclesAndSnapshotInputs
20260802204708_EnforceSlice9FinalReviewIntegrity
20260803085202_AddWiseOldManCompetitionSynchronization
20260818220852_AddWiseOldManCompetitionEhbBounds
20260823214522_NormalizeMyAccountsPreferredOrder
20260831142836_AddEventQuarantine
20260905221344_AddImmutableCatalogueItemIdentity
20260907185521_RepairDeletedSignupQuestions
20260910181345_AddCoCaptainSignupQuestion
20260912175815_AddDropAnnouncements
20260913121927_AddDropAnnouncementExpansionBoundary
20260914083036_RetainApprovalArtworkAfterTileRemoval
20260915142649_AddCatalogueApiMappingAndPrices
20260915165204_FreezeEventItemPrices
20260915170124_GuardSuspiciousPriceCandidates
20260915174600_CacheEventCompetitionBossActivity
20260915183337_AddEventStatsLuckCheckpoint
20260915190625_RetainLuckAfterAdditiveApproval
20260915192848_SaveStatsGuidanceAndArtwork
20260916100000_PopulateRetainedCatalogue
20260922091433_AddManagedWiseOldManCompetitionManagement
20260922163951_AddWiseOldManUpdateAllSlots
20260922204859_AddDerivedTileDescriptions
20260922214708_AddTileCompletionFactsAndCurrentScoreReachedAt
```

## history-after (85 entries)

```
20260711062109_InitialFoundation
20260711155515_AddIdentityAndAudit
20260711172420_AddEventsAndSignups
20260711185809_AddCatalogueAndBoardFoundation
20260711191039_AddBoardSnapshots
20260711214333_AddCatalogueImages
20260712192541_SeedWiseOldManBossRates
20260712210645_SeedBarrowsAndLunarDrops
20260712212313_AddTeamsAndDraft
20260712222102_AllowReusingUndoneDraftPickNumbers
20260713101510_AddEvidenceSubmissionAndReview
20260713134514_LinkGeneratedCaptainAccounts
20260713145517_AddSubmissionPrivacyRequests
20260713162433_AddAdminConcurrencyHardening
20260713175710_AddBoardEditingLease
20260713181525_AddEventFinalizationWorkflow
20260720005059_AddSourceDropRateVariants
20260721232916_AddBoardEditorAndCatalogueRateMechanics
20260724223331_AddSlice1IdentityFoundation
20260725170951_AddEmergencyLifecycleAndPersonalNotifications
20260726195230_AddSlice2PersistenceFoundation
20260726201926_TransitionParticipantCharacterAuthority
20260727090952_AddSlice3LifecyclePersistenceFoundation
20260727094645_AddSlice3GuidedCreationAndIdentity
20260727104443_AddSlice3ScheduleReadinessAndSignupLifecycle
20260727121333_AddDevelopmentFixtureLifecycleExemption
20260727134959_AddScheduledLifecycleExecution
20260727192755_AddEventBannerCleanupOutbox
20260727223107_AddSlice4SignupPersistenceFoundation
20260728135529_RemoveSignupCompatibility
20260728143114_AddParticipantResponseRevisionAndProtectLegacyDiscord
20260728143651_AddSignupAnswerIntegrityRelations
20260728145446_NormalizeRetainedLegacyDiscordSystemField
20260729132140_AddSlice5PersistenceFoundation
20260729172938_AddFrozenPublicationIdentity
20260729223403_AddSlice6BoardApprovalSnapshotFoundation
20260729231001_ReconcileSlice6CatalogueModel
20260729234011_AddSlice6ManagedBoardTileImages
20260730160029_RemoveSlice6RateVariants
20260730183704_AddPublishedBoardCorrectionFlag
20260730212304_AddSlice7LiveAccountAndTeamFocusFoundation
20260731170051_RemoveTeamFocusEventTeamAlternateKey
20260731180603_NormalizeCompletedTileFocusMarkers
20260731204600_AddSlice8EvidenceFoundation
20260801160152_RemoveDeprecatedEvidenceCompatibility
20260802000213_AddAuthoritativeEventEndPersistence
20260802002639_AddLiveWithdrawalReplacementPersistence
20260802005536_AddFinalReviewCyclesAndSnapshotInputs
20260802204708_EnforceSlice9FinalReviewIntegrity
20260803085202_AddWiseOldManCompetitionSynchronization
20260818220852_AddWiseOldManCompetitionEhbBounds
20260823214522_NormalizeMyAccountsPreferredOrder
20260831142836_AddEventQuarantine
20260905221344_AddImmutableCatalogueItemIdentity
20260907185521_RepairDeletedSignupQuestions
20260910181345_AddCoCaptainSignupQuestion
20260912175815_AddDropAnnouncements
20260913121927_AddDropAnnouncementExpansionBoundary
20260914083036_RetainApprovalArtworkAfterTileRemoval
20260915142649_AddCatalogueApiMappingAndPrices
20260915165204_FreezeEventItemPrices
20260915170124_GuardSuspiciousPriceCandidates
20260915174600_CacheEventCompetitionBossActivity
20260915183337_AddEventStatsLuckCheckpoint
20260915190625_RetainLuckAfterAdditiveApproval
20260915192848_SaveStatsGuidanceAndArtwork
20260916100000_PopulateRetainedCatalogue
20260922091433_AddManagedWiseOldManCompetitionManagement
20260922163951_AddWiseOldManUpdateAllSlots
20260922204859_AddDerivedTileDescriptions
20260922214708_AddTileCompletionFactsAndCurrentScoreReachedAt
20260926084448_AddImmediatePlayingSwitchOrder
20260926113811_RetireFormationTypeConstraint
20260926135604_AddDraftPublicationMethod
20260926201553_AddWiseOldManConnectionProvenance
20260926215725_AddBoardEstimateFreshness
20260926233834_RetireEventBanners
20261001165543_LuckCheckpointV2
20261002114016_AddEventCreationOperations
20261002143542_AddSignupQuestionCreationOperations
20261003184632_AllowCancelledDraftRestart
20261004002948_AddEventPlacementRule
20261004093705_ClearLegacyDropTileEhbOverrides
20261004112050_AddCompetitionEndUpdateState
20261004133947_AddBossActivityTeamSize
```

## candidate-migrations (85 entries)

```
20260711062109_InitialFoundation
20260711155515_AddIdentityAndAudit
20260711172420_AddEventsAndSignups
20260711185809_AddCatalogueAndBoardFoundation
20260711191039_AddBoardSnapshots
20260711214333_AddCatalogueImages
20260712192541_SeedWiseOldManBossRates
20260712210645_SeedBarrowsAndLunarDrops
20260712212313_AddTeamsAndDraft
20260712222102_AllowReusingUndoneDraftPickNumbers
20260713101510_AddEvidenceSubmissionAndReview
20260713134514_LinkGeneratedCaptainAccounts
20260713145517_AddSubmissionPrivacyRequests
20260713162433_AddAdminConcurrencyHardening
20260713175710_AddBoardEditingLease
20260713181525_AddEventFinalizationWorkflow
20260720005059_AddSourceDropRateVariants
20260721232916_AddBoardEditorAndCatalogueRateMechanics
20260724223331_AddSlice1IdentityFoundation
20260725170951_AddEmergencyLifecycleAndPersonalNotifications
20260726195230_AddSlice2PersistenceFoundation
20260726201926_TransitionParticipantCharacterAuthority
20260727090952_AddSlice3LifecyclePersistenceFoundation
20260727094645_AddSlice3GuidedCreationAndIdentity
20260727104443_AddSlice3ScheduleReadinessAndSignupLifecycle
20260727121333_AddDevelopmentFixtureLifecycleExemption
20260727134959_AddScheduledLifecycleExecution
20260727192755_AddEventBannerCleanupOutbox
20260727223107_AddSlice4SignupPersistenceFoundation
20260728135529_RemoveSignupCompatibility
20260728143114_AddParticipantResponseRevisionAndProtectLegacyDiscord
20260728143651_AddSignupAnswerIntegrityRelations
20260728145446_NormalizeRetainedLegacyDiscordSystemField
20260729132140_AddSlice5PersistenceFoundation
20260729172938_AddFrozenPublicationIdentity
20260729223403_AddSlice6BoardApprovalSnapshotFoundation
20260729231001_ReconcileSlice6CatalogueModel
20260729234011_AddSlice6ManagedBoardTileImages
20260730160029_RemoveSlice6RateVariants
20260730183704_AddPublishedBoardCorrectionFlag
20260730212304_AddSlice7LiveAccountAndTeamFocusFoundation
20260731170051_RemoveTeamFocusEventTeamAlternateKey
20260731180603_NormalizeCompletedTileFocusMarkers
20260731204600_AddSlice8EvidenceFoundation
20260801160152_RemoveDeprecatedEvidenceCompatibility
20260802000213_AddAuthoritativeEventEndPersistence
20260802002639_AddLiveWithdrawalReplacementPersistence
20260802005536_AddFinalReviewCyclesAndSnapshotInputs
20260802204708_EnforceSlice9FinalReviewIntegrity
20260803085202_AddWiseOldManCompetitionSynchronization
20260818220852_AddWiseOldManCompetitionEhbBounds
20260823214522_NormalizeMyAccountsPreferredOrder
20260831142836_AddEventQuarantine
20260905221344_AddImmutableCatalogueItemIdentity
20260907185521_RepairDeletedSignupQuestions
20260910181345_AddCoCaptainSignupQuestion
20260912175815_AddDropAnnouncements
20260913121927_AddDropAnnouncementExpansionBoundary
20260914083036_RetainApprovalArtworkAfterTileRemoval
20260915142649_AddCatalogueApiMappingAndPrices
20260915165204_FreezeEventItemPrices
20260915170124_GuardSuspiciousPriceCandidates
20260915174600_CacheEventCompetitionBossActivity
20260915183337_AddEventStatsLuckCheckpoint
20260915190625_RetainLuckAfterAdditiveApproval
20260915192848_SaveStatsGuidanceAndArtwork
20260916100000_PopulateRetainedCatalogue
20260922091433_AddManagedWiseOldManCompetitionManagement
20260922163951_AddWiseOldManUpdateAllSlots
20260922204859_AddDerivedTileDescriptions
20260922214708_AddTileCompletionFactsAndCurrentScoreReachedAt
20260926084448_AddImmediatePlayingSwitchOrder
20260926113811_RetireFormationTypeConstraint
20260926135604_AddDraftPublicationMethod
20260926201553_AddWiseOldManConnectionProvenance
20260926215725_AddBoardEstimateFreshness
20260926233834_RetireEventBanners
20261001165543_LuckCheckpointV2
20261002114016_AddEventCreationOperations
20261002143542_AddSignupQuestionCreationOperations
20261003184632_AllowCancelledDraftRestart
20261004002948_AddEventPlacementRule
20261004093705_ClearLegacyDropTileEhbOverrides
20261004112050_AddCompetitionEndUpdateState
20261004133947_AddBossActivityTeamSize
```

## Not covered (accepted limits)

No VM isolation (Docker Desktop internal network only), no real R2/WOM/Discord, no host bingo-deploy wrapper,
no public TLS/Caddy, no restic/GHCR, no production Data Protection keys, no G4 Down/re-Up clone check.

Private data (restored dump copy in Docker volumes, logs, G4 id sets) stays in `<scratch>/rehearsal/run-5/private`, `<scratch>/rehearsal/run-5/logs`
and the `bingo-rehearsal_*` volumes until `/Users/christopher/Documents/BingoWebpage-rehearsal/scripts/rehearsal/local-rehearsal.sh --cleanup --work <scratch>/rehearsal/run-5` and deletion of the work directory.
