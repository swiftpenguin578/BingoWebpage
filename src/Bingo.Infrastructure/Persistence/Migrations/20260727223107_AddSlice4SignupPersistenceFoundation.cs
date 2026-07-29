using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable IDE0161, CA1861

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlice4SignupPersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_signup_questions_event_id_key",
                table: "signup_questions");

            migrationBuilder.AddColumn<string>(
                name: "account_answer_role",
                table: "signup_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disabled_at",
                table: "signup_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "disabled_by_account_id",
                table: "signup_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "disabled_reason",
                table: "signup_questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "public_on_signup_board",
                table: "signup_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "replaced_by_signup_question_id",
                table: "signup_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signup_form_id",
                table: "signup_questions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "system_field",
                table: "signup_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "signup_questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "osrs_character_id",
                table: "signup_answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "payment_received",
                table: "event_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "withdrawn_by_account_id",
                table: "event_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_signup_questions_event_id_id",
                table: "signup_questions",
                columns: new[] { "event_id", "id" });

            migrationBuilder.CreateTable(
                name: "signup_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_response_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    require_signup_code = table.Column<bool>(type: "boolean", nullable: false),
                    signup_code_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signup_forms", x => x.id);
                    table.UniqueConstraint("AK_signup_forms_event_id_id", x => new { x.event_id, x.id });
                    table.CheckConstraint("ck_signup_forms_code", "(require_signup_code AND signup_code_hash IS NOT NULL) OR (NOT require_signup_code AND signup_code_hash IS NULL)");
                    table.ForeignKey(
                        name: "FK_signup_forms_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO signup_forms (id, event_id, version, created_at, published_at, closed_at, first_response_at, require_signup_code, signup_code_hash)
                SELECT md5('slice4:form:' || id::text)::uuid, id, 0, created_at, first_public_at, NULL,
                       (SELECT min(p.signed_up_at) FROM event_participants p WHERE p.event_id = e.id),
                       require_signup_code, signup_code_hash
                FROM events e;

                UPDATE signup_questions q
                SET signup_form_id = md5('slice4:form:' || q.event_id::text)::uuid,
                    type = CASE WHEN type IN ('ShortText', 'LongText') THEN 'Text' ELSE type END,
                    public_on_signup_board = TRUE,
                    system_field = 'None';

                INSERT INTO signup_questions (id, signup_form_id, event_id, key, label, help_text, type, required, position, options, active, system_field, account_answer_role, public_on_signup_board, version)
                SELECT md5('slice4:primary:' || e.id::text)::uuid, f.id, e.id, 'primary_regular_account', 'Account', NULL, 'Account', TRUE, 0, NULL, TRUE, 'PrimaryRegularAccount', 'Playing', TRUE, 0
                FROM events e JOIN signup_forms f ON f.event_id = e.id;
                INSERT INTO signup_questions (id, signup_form_id, event_id, key, label, help_text, type, required, position, options, active, system_field, account_answer_role, public_on_signup_board, version)
                SELECT md5('slice4:captain:' || e.id::text)::uuid, f.id, e.id, 'captain_volunteer', 'Captain volunteer', NULL, 'YesNo', FALSE, 1, NULL, TRUE, 'CaptainVolunteer', NULL, TRUE, 0
                FROM events e JOIN signup_forms f ON f.event_id = e.id;
                INSERT INTO signup_questions (id, signup_form_id, event_id, key, label, help_text, type, required, position, options, active, disabled_at, disabled_reason, system_field, account_answer_role, public_on_signup_board, version)
                SELECT md5('slice4:legacy-alt:' || e.id::text)::uuid, f.id, e.id, 'legacy_alt_account', 'Legacy alt account', 'Retained historical secondary account.', 'Account', FALSE, 9990, NULL, FALSE, now(), 'Retained conversion', 'LegacySecondaryAltAccount', 'Informational', TRUE, 0
                FROM events e JOIN signup_forms f ON f.event_id = e.id
                WHERE EXISTS (SELECT 1 FROM event_participant_characters c WHERE c.event_id = e.id AND c.event_role = 'Informational');

                UPDATE event_participant_characters c
                SET signup_question_id = CASE WHEN c.event_role = 'Playing' THEN md5('slice4:primary:' || c.event_id::text)::uuid ELSE md5('slice4:legacy-alt:' || c.event_id::text)::uuid END;

                INSERT INTO signup_questions (id, signup_form_id, event_id, key, label, help_text, type, required, position, options, active, disabled_at, disabled_reason, system_field, public_on_signup_board, version)
                SELECT md5('slice4:legacy-discord:' || e.id::text)::uuid, f.id, e.id, 'legacy_discord_identity', 'Discord identity', 'Retained historical signup value.', 'Text', FALSE, 9991, NULL, FALSE, now(), 'Retained conversion', 'LegacyDiscordIdentity', TRUE, 0
                FROM events e JOIN signup_forms f ON f.event_id = e.id WHERE EXISTS (SELECT 1 FROM event_participants p WHERE p.event_id = e.id AND p.discord_identity IS NOT NULL);
                INSERT INTO signup_questions (id, signup_form_id, event_id, key, label, help_text, type, required, position, options, active, disabled_at, disabled_reason, system_field, public_on_signup_board, version)
                SELECT md5('slice4:legacy-comments:' || e.id::text)::uuid, f.id, e.id, 'legacy_comments', 'Comments', 'Retained historical signup value.', 'Text', FALSE, 9992, NULL, FALSE, now(), 'Retained conversion', 'LegacyComments', TRUE, 0
                FROM events e JOIN signup_forms f ON f.event_id = e.id WHERE EXISTS (SELECT 1 FROM event_participants p WHERE p.event_id = e.id AND p.comments IS NOT NULL);
                INSERT INTO signup_answers (id, event_participant_id, signup_question_id, question_label_snapshot, value)
                SELECT md5('slice4:discord-answer:' || p.id::text)::uuid, p.id, md5('slice4:legacy-discord:' || p.event_id::text)::uuid, 'Discord identity', p.discord_identity
                FROM event_participants p WHERE p.discord_identity IS NOT NULL;
                INSERT INTO signup_answers (id, event_participant_id, signup_question_id, question_label_snapshot, value)
                SELECT md5('slice4:comments-answer:' || p.id::text)::uuid, p.id, md5('slice4:legacy-comments:' || p.event_id::text)::uuid, 'Comments', p.comments
                FROM event_participants p WHERE p.comments IS NOT NULL;

                UPDATE event_participants SET payment_received = payment_status = 'Paid';
                UPDATE event_participants SET signup_status = 'Withdrawn', withdrawn_at = COALESCE(withdrawn_at, removed_at) WHERE signup_status = 'Removed';
                """);

            migrationBuilder.DropColumn(name: "payment_status", table: "event_participants");

            migrationBuilder.CreateIndex(
                name: "IX_signup_questions_disabled_by_account_id",
                table: "signup_questions",
                column: "disabled_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_signup_questions_replaced_by_signup_question_id",
                table: "signup_questions",
                column: "replaced_by_signup_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_signup_questions_signup_form_id_key",
                table: "signup_questions",
                columns: new[] { "signup_form_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signup_questions_signup_form_id_system_field",
                table: "signup_questions",
                columns: new[] { "signup_form_id", "system_field" },
                unique: true,
                filter: "system_field <> 'None'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_signup_questions_account_shape",
                table: "signup_questions",
                sql: "(type = 'Account' AND account_answer_role IS NOT NULL) OR (type <> 'Account' AND account_answer_role IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_signup_questions_choice_shape",
                table: "signup_questions",
                sql: "(type = 'SingleChoice' AND options IS NOT NULL AND length(btrim(options)) > 0) OR (type <> 'SingleChoice' AND options IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_signup_questions_disabled_history",
                table: "signup_questions",
                sql: "(active AND disabled_at IS NULL AND disabled_by_account_id IS NULL AND disabled_reason IS NULL) OR (NOT active AND disabled_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_signup_questions_key",
                table: "signup_questions",
                sql: "length(btrim(key)) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_signup_answers_osrs_character_id",
                table: "signup_answers",
                column: "osrs_character_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_signup_answers_account_value",
                table: "signup_answers",
                sql: "osrs_character_id IS NOT NULL OR value IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_withdrawn_by_account_id",
                table: "event_participants",
                column: "withdrawn_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_signup_forms_event_id",
                table: "signup_forms",
                column: "event_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_event_participants_accounts_withdrawn_by_account_id",
                table: "event_participants",
                column: "withdrawn_by_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_signup_answers_osrs_characters_osrs_character_id",
                table: "signup_answers",
                column: "osrs_character_id",
                principalTable: "osrs_characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_signup_questions_accounts_disabled_by_account_id",
                table: "signup_questions",
                column: "disabled_by_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_signup_questions_signup_forms_signup_form_id",
                table: "signup_questions",
                column: "signup_form_id",
                principalTable: "signup_forms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_signup_questions_signup_questions_replaced_by_signup_questi~",
                table: "signup_questions",
                column: "replaced_by_signup_question_id",
                principalTable: "signup_questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_event_participant_characters_signup_questions_event_id_signup_question_id",
                table: "event_participant_characters",
                columns: new[] { "event_id", "signup_question_id" },
                principalTable: "signup_questions",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.DropIndex(name: "IX_event_participant_characters_signup_question_id", table: "event_participant_characters");
            migrationBuilder.CreateIndex(name: "IX_event_participant_characters_event_id_signup_question_id", table: "event_participant_characters", columns: new[] { "event_id", "signup_question_id" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION validate_signup_answer_shape() RETURNS trigger AS $$
                DECLARE question_type text; question_event uuid; participant_event uuid;
                BEGIN
                    SELECT type, event_id INTO question_type, question_event FROM signup_questions WHERE id = NEW.signup_question_id;
                    SELECT event_id INTO participant_event FROM event_participants WHERE id = NEW.event_participant_id;
                    IF question_type IS NULL OR participant_event IS NULL OR question_event <> participant_event THEN RAISE EXCEPTION 'Signup answer question and participant must belong to the same event'; END IF;
                    IF question_type = 'Account' AND (NEW.osrs_character_id IS NULL OR NEW.value <> '') THEN RAISE EXCEPTION 'Account answers require only an OSRS character reference'; END IF;
                    IF question_type <> 'Account' AND NEW.osrs_character_id IS NOT NULL THEN RAISE EXCEPTION 'Only Account answers may reference an OSRS character'; END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_signup_answers_shape BEFORE INSERT OR UPDATE ON signup_answers FOR EACH ROW EXECUTE FUNCTION validate_signup_answer_shape();
                CREATE OR REPLACE FUNCTION validate_signup_assignment_shape() RETURNS trigger AS $$
                DECLARE question_type text; question_role text;
                BEGIN
                    IF NEW.signup_question_id IS NULL THEN RETURN NEW; END IF;
                    SELECT type, account_answer_role INTO question_type, question_role FROM signup_questions WHERE id = NEW.signup_question_id AND event_id = NEW.event_id;
                    IF question_type <> 'Account' OR question_role IS NULL OR question_role <> NEW.event_role THEN RAISE EXCEPTION 'Signup assignment must reference an Account question with the matching role'; END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_signup_assignments_shape BEFORE INSERT OR UPDATE ON event_participant_characters FOR EACH ROW EXECUTE FUNCTION validate_signup_assignment_shape();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_participants_accounts_withdrawn_by_account_id",
                table: "event_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_signup_answers_osrs_characters_osrs_character_id",
                table: "signup_answers");

            migrationBuilder.DropForeignKey(
                name: "FK_signup_questions_accounts_disabled_by_account_id",
                table: "signup_questions");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_signup_answers_shape ON signup_answers; DROP FUNCTION IF EXISTS validate_signup_answer_shape(); DROP TRIGGER IF EXISTS trg_signup_assignments_shape ON event_participant_characters; DROP FUNCTION IF EXISTS validate_signup_assignment_shape();");
            migrationBuilder.DropForeignKey(
                name: "FK_event_participant_characters_signup_questions_event_id_signup_question_id",
                table: "event_participant_characters");
            migrationBuilder.DropIndex(name: "IX_event_participant_characters_event_id_signup_question_id", table: "event_participant_characters");
            migrationBuilder.CreateIndex(name: "IX_event_participant_characters_signup_question_id", table: "event_participant_characters", column: "signup_question_id");

            migrationBuilder.DropForeignKey(
                name: "FK_signup_questions_signup_forms_signup_form_id",
                table: "signup_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_signup_questions_signup_questions_replaced_by_signup_questi~",
                table: "signup_questions");

            migrationBuilder.DropTable(
                name: "signup_forms");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_signup_questions_event_id_id",
                table: "signup_questions");

            migrationBuilder.DropIndex(
                name: "IX_signup_questions_disabled_by_account_id",
                table: "signup_questions");

            migrationBuilder.DropIndex(
                name: "IX_signup_questions_replaced_by_signup_question_id",
                table: "signup_questions");

            migrationBuilder.DropIndex(
                name: "IX_signup_questions_signup_form_id_key",
                table: "signup_questions");

            migrationBuilder.DropIndex(
                name: "IX_signup_questions_signup_form_id_system_field",
                table: "signup_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_signup_questions_account_shape",
                table: "signup_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_signup_questions_choice_shape",
                table: "signup_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_signup_questions_disabled_history",
                table: "signup_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_signup_questions_key",
                table: "signup_questions");

            migrationBuilder.DropIndex(
                name: "IX_signup_answers_osrs_character_id",
                table: "signup_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_signup_answers_account_value",
                table: "signup_answers");

            migrationBuilder.DropIndex(
                name: "IX_event_participants_withdrawn_by_account_id",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "account_answer_role",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "disabled_at",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "disabled_by_account_id",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "disabled_reason",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "public_on_signup_board",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "replaced_by_signup_question_id",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "signup_form_id",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "system_field",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "signup_questions");

            migrationBuilder.DropColumn(
                name: "osrs_character_id",
                table: "signup_answers");

            migrationBuilder.DropColumn(
                name: "payment_received",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "withdrawn_by_account_id",
                table: "event_participants");

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "event_participants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_signup_questions_event_id_key",
                table: "signup_questions",
                columns: new[] { "event_id", "key" },
                unique: true);
        }
    }
}
