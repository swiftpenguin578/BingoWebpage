# TS item3 — preserved dedicated exceptions

After item2 `1d63f144e09739791a0069d3a7c21521e231dc7a`, the 66 PostgreSQL
Integration fixture owners comprise **41 migrated-template classes** and **25
dedicated-container classes**. The following 25 retain their original per-test
container builder, bootstrap database and schema provisioning. Item4 adds only
the common credentialed readiness check. No migration case was split out of its
existing class or converted to a fully migrated template.

| Dedicated class | Source / reason |
| --- | --- |
| `AccessAndAuditTests` | `AccessAndAuditTests.cs`: model-created `EnsureCreated` schema; preserve the original schema path. |
| `AccountOverviewTests` | `AccountOverviewTests.cs`: model-created `EnsureCreated` schema and synthetic seed; preserve the original schema path. |
| `SubmissionWorkflowTests` | `SubmissionWorkflowTests.cs` and partials: `EnsureCreated` schema and actual-backend blocking checks; preserve the original server/schema setup. |
| `PostgreSqlConnectivityTests` | `PostgreSqlConnectivityTests.cs`: fresh migrated-database and fresh model-schema facts exercised inside test bodies. |
| `ProductionCataloguePreflightIntegrationTests` | `ProductionCataloguePreflightIntegrationTests.cs`: deletes/recreates its database during each test's controlled reset. |
| `Slice1MigrationRehearsalTests` | `Slice1MigrationRehearsalTests.cs`: retained/intermediate schema conversions, backfills, rejection and clean rebuild. |
| `Slice2MigrationRehearsalTests` | `Slice2MigrationRehearsalTests.cs`: retained/intermediate schema conversions and clean rebuild. |
| `Slice3LifecyclePersistenceIntegrationTests` | `Slice3LifecyclePersistenceIntegrationTests.cs`: legacy schema upgrade and database deletion/rebuild. |
| `Slice4AuthenticatedSignupIntegrationTests` | `Slice4AuthenticatedSignupIntegrationTests.cs`: intermediate signup/question repair migrations with retained rows. |
| `Slice5MigrationRejectionTests` | `Slice5MigrationRejectionTests.cs`: intermediate schema and fail-closed migration rejection. |
| `Slice6CatalogueAdministrationIntegrationTests` | Base plus `StatsPass1CatalogueIntegrationTests.cs`, `BoardApprovalTicketBatchIntegrationTests.cs`: catalogue backfill/downgrade/upgrade and historical completion migration. |
| `Slice6RateVariantRetirementMigrationTests` | `Slice6RateVariantRetirementMigrationTests.cs`: pre-correction retained schema and retirement upgrade. |
| `Slice7Pass71IntegrationTests` | `Slice7Pass71IntegrationTests.cs`: intermediate foundation, normalization and migration ordering. |
| `Slice8Pass81PersistenceIntegrationTests` | `Slice8Pass81PersistenceIntegrationTests.cs`: intermediate schema conversion/rejection and removal facts. |
| `Slice3ScheduledLifecycleIntegrationTests` | `StatsPass2LifecycleIntegrationTests.cs` partial: historical migration downgrade/upgrade. |
| `Slice10Pass102CompetitionSynchronizationTests` | `StatsPass3MetricCacheIntegrationTests.cs`, `StatsPass4BoundaryIntegrationTests.cs`, `StatsPass4ReviewCorrectionsIntegrationTests.cs`, `StatsPass5PresentationIntegrationTests.cs`, `Au20EndLifecycleIntegrationTests.cs`: intermediate schemas, migration rejection/backfill and rollback. |
| `CataloguePopulationMigrationIntegrationTests` | `CataloguePopulationMigrationIntegrationTests.cs`: migration population, rejection, repair and retained rows. |
| `BannerRetirementMigrationTests` | `BannerRetirementMigrationTests.cs`: retirement migration, blocked/repeated upgrades and recovery. |
| `BoardEstimateFreshnessMigrationTests` | `BoardEstimateFreshnessMigrationTests.cs`: pre-freshness schema and retained backfill. |
| `WiseOldManConnectionProvenanceMigrationTests` | `WiseOldManConnectionProvenanceMigrationTests.cs`: pre-provenance schema and retained backfill. |
| `Cat1TeamSizeMigrationIntegrationTests` | Second class in `Cat1TeamSizeIntegrationTests.cs`: fail-closed intermediate schema, repair/backfill and rollback. |
| `Au12PlacementRuleIntegrationTests` | Base plus `Au11LegacyOverrideMigrationTests.cs`: placement-rule migration and irreversible legacy-override cleanup/down. |
| `C33FinalizationFreshnessTests` | `C33FinalizationFreshnessTests.cs`: retained completion-fact downgrade/upgrade/backfill. |
| `EventQuarantineIntegrationTests` | `EventQuarantineIntegrationTests.cs`: retained-schema audit/notification migration rejection/repair. |
| `PublishedContentRetentionIntegrationTests` | `PublishedContentRetentionIntegrationTests.cs`: retained-publication intermediate schema/backfill. |

Partial files were mapped to their owning fixture before conversion. The dedicated
set includes ambiguous/fresh-schema setups conservatively. No cross-test data
dependency was found in the converted C20 seeds or other converted setup paths;
tests continue to receive independent databases. Database-scoped triggers,
functions, advisory locks and `pg_stat_activity WHERE datname = current_database()`
checks in converted classes operate on each test's own clone.

The Browser project's existing `BrowserTestApplicationFactory` collection fixture
is outside the Integration per-test setup conversion: its application/database
lifetime is preserved, and item4 adds credentialed readiness only. The existing
AdminDesignParityFixture already has ruling58's real-credential bounded readiness
and is unchanged.

Scoped checks: 41 files contain `IClassFixture<PostgreSqlTestFixture>`; 25
Integration files still declare a `private readonly PostgreSqlContainer`.
Compared each dedicated source file byte-for-byte with starting `8fd559c`: all
unchanged at this checkpoint. `git diff --check` passed. Documentation-only item;
no redundant build or suite run. All database/server/schema exceptions stay owned
by this test run's Testcontainers; no user-owned or UR database is accessed.
