using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LtcCognitive.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialNineTableSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cognitive");

            migrationBuilder.EnsureSchema(
                name: "ltc");

            migrationBuilder.CreateTable(
                name: "cognitive_domain",
                schema: "cognitive",
                columns: table => new
                {
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_zh_tw = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cognitive_domain", x => x.domain_id);
                });

            migrationBuilder.CreateTable(
                name: "item",
                schema: "ltc",
                columns: table => new
                {
                    item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    item_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    item_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    name_zh_tw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item", x => x.item_id);
                    table.CheckConstraint("ck_item_price", "price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "player",
                schema: "ltc",
                columns: table => new
                {
                    player_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    player_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    auth_provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    auth_subject_hash = table.Column<string>(type: "character(64)", nullable: false),
                    display_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    birth_year = table.Column<short>(type: "smallint", nullable: true),
                    education_level_code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player", x => x.player_id);
                    table.CheckConstraint("ck_player_birth_year", "birth_year IS NULL OR birth_year BETWEEN 1900 AND 2100");
                    table.CheckConstraint("ck_player_status", "status IN ('active', 'suspended', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "game",
                schema: "cognitive",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_zh_tw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    evidence_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game", x => x.game_id);
                    table.ForeignKey(
                        name: "FK_game_cognitive_domain_domain_id",
                        column: x => x.domain_id,
                        principalSchema: "cognitive",
                        principalTable: "cognitive_domain",
                        principalColumn: "domain_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coin_transaction",
                schema: "ltc",
                columns: table => new
                {
                    transaction_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    reference_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coin_transaction", x => x.transaction_id);
                    table.CheckConstraint("ck_coin_amount_nonzero", "amount <> 0");
                    table.ForeignKey(
                        name: "FK_coin_transaction_player_player_id",
                        column: x => x.player_id,
                        principalSchema: "ltc",
                        principalTable: "player",
                        principalColumn: "player_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_inventory",
                schema: "ltc",
                columns: table => new
                {
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    acquired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_inventory", x => new { x.player_id, x.item_id });
                    table.CheckConstraint("ck_inventory_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_player_inventory_item_item_id",
                        column: x => x.item_id,
                        principalSchema: "ltc",
                        principalTable: "item",
                        principalColumn: "item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_player_inventory_player_player_id",
                        column: x => x.player_id,
                        principalSchema: "ltc",
                        principalTable: "player",
                        principalColumn: "player_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_session",
                schema: "cognitive",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completion_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    exit_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_session", x => x.session_id);
                    table.CheckConstraint("ck_assessment_time", "ended_at_utc >= started_at_utc");
                    table.ForeignKey(
                        name: "FK_assessment_session_game_game_id",
                        column: x => x.game_id,
                        principalSchema: "cognitive",
                        principalTable: "game",
                        principalColumn: "game_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assessment_session_player_player_id",
                        column: x => x.player_id,
                        principalSchema: "ltc",
                        principalTable: "player",
                        principalColumn: "player_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "derived_metric",
                schema: "cognitive",
                columns: table => new
                {
                    metric_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    calculation_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quality_flag = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_derived_metric", x => x.metric_id);
                    table.ForeignKey(
                        name: "FK_derived_metric_assessment_session_session_id",
                        column: x => x.session_id,
                        principalSchema: "cognitive",
                        principalTable: "assessment_session",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_derived_metric_cognitive_domain_domain_id",
                        column: x => x.domain_id,
                        principalSchema: "cognitive",
                        principalTable: "cognitive_domain",
                        principalColumn: "domain_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trial_event",
                schema: "cognitive",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_index = table.Column<short>(type: "smallint", nullable: false),
                    trial_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    stimulus_json = table.Column<string>(type: "jsonb", nullable: true),
                    expected_response = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    actual_response = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    reaction_time_ms = table.Column<int>(type: "integer", nullable: true),
                    presentation_duration_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trial_event", x => new { x.session_id, x.trial_index });
                    table.ForeignKey(
                        name: "FK_trial_event_assessment_session_session_id",
                        column: x => x.session_id,
                        principalSchema: "cognitive",
                        principalTable: "assessment_session",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "cognitive",
                table: "cognitive_domain",
                columns: new[] { "domain_id", "domain_code", "description", "name_zh_tw" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "attention_inhibition", null, "注意力與抑制控制" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "processing_speed", null, "處理速度與視覺搜尋" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "executive_reasoning", null, "執行功能與數字推理" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "visual_working_memory", null, "視覺工作記憶" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "visuospatial_planning", null, "視空間規劃與問題解決" }
                });

            migrationBuilder.InsertData(
                schema: "cognitive",
                table: "game",
                columns: new[] { "game_id", "game_code", "domain_id", "evidence_note", "is_active", "mapping_version", "name_zh_tw" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "stroop_color_match", new Guid("10000000-0000-0000-0000-000000000001"), "Stroop 作業：選擇性注意與反應抑制。", true, "1.0", "顏色文字判斷" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "number_order", new Guid("10000000-0000-0000-0000-000000000002"), "視覺搜尋、排序與處理速度。", true, "1.0", "數字由小到大" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "number_sum", new Guid("10000000-0000-0000-0000-000000000003"), "工作記憶、策略選擇與數字推理。", true, "1.0", "數字組合加總" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "card_memory", new Guid("10000000-0000-0000-0000-000000000004"), "視覺位置的編碼、保持與提取。", true, "1.0", "翻卡牌" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "pipe_rotation", new Guid("10000000-0000-0000-0000-000000000005"), "心像旋轉、空間規劃與問題解決。", true, "1.0", "旋轉接水管" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_player_started",
                schema: "cognitive",
                table: "assessment_session",
                columns: new[] { "player_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_session_game_id",
                schema: "cognitive",
                table: "assessment_session",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "uq_cognitive_domain_code",
                schema: "cognitive",
                table: "cognitive_domain",
                column: "domain_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_coin_source",
                schema: "ltc",
                table: "coin_transaction",
                columns: new[] { "player_id", "transaction_type", "reference_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_derived_metric_domain_id",
                schema: "cognitive",
                table: "derived_metric",
                column: "domain_id");

            migrationBuilder.CreateIndex(
                name: "uq_metric_session_domain_code",
                schema: "cognitive",
                table: "derived_metric",
                columns: new[] { "session_id", "domain_id", "metric_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_domain_id",
                schema: "cognitive",
                table: "game",
                column: "domain_id");

            migrationBuilder.CreateIndex(
                name: "uq_game_code",
                schema: "cognitive",
                table: "game",
                column: "game_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_code",
                schema: "ltc",
                table: "item",
                column: "item_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_player_auth_subject",
                schema: "ltc",
                table: "player",
                columns: new[] { "auth_provider", "auth_subject_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_player_code",
                schema: "ltc",
                table: "player",
                column: "player_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_inventory_item_id",
                schema: "ltc",
                table: "player_inventory",
                column: "item_id");

            migrationBuilder.Sql("""
                CREATE VIEW ltc.player_directory AS
                SELECT player_id, player_code, display_name, birth_year, education_level_code,
                       status, created_at_utc, last_login_at_utc FROM ltc.player;
                CREATE VIEW ltc.coin_balance AS
                SELECT p.player_id, p.player_code, p.display_name,
                       COALESCE(SUM(t.amount), 0)::bigint AS coin_balance
                FROM ltc.player p LEFT JOIN ltc.coin_transaction t ON t.player_id = p.player_id
                GROUP BY p.player_id, p.player_code, p.display_name;
                CREATE VIEW cognitive.assessment_summary AS
                SELECT s.session_id, s.player_id, p.player_code, p.display_name,
                       g.game_code, g.name_zh_tw AS game_name, d.domain_code, d.name_zh_tw AS domain_name,
                       s.started_at_utc, s.ended_at_utc,
                       ROUND(EXTRACT(EPOCH FROM (s.ended_at_utc - s.started_at_utc)) * 1000)::bigint AS duration_ms,
                       s.completion_status, COUNT(m.metric_id)::integer AS metric_count
                FROM cognitive.assessment_session s
                JOIN ltc.player p ON p.player_id = s.player_id
                JOIN cognitive.game g ON g.game_id = s.game_id
                JOIN cognitive.cognitive_domain d ON d.domain_id = g.domain_id
                LEFT JOIN cognitive.derived_metric m ON m.session_id = s.session_id
                GROUP BY s.session_id, s.player_id, p.player_code, p.display_name,
                         g.game_code, g.name_zh_tw, d.domain_code, d.name_zh_tw;
                CREATE VIEW cognitive.cognitive_trend AS
                SELECT s.player_id, p.player_code, p.display_name, s.ended_at_utc AS assessed_at_utc,
                       g.game_code, d.domain_code, d.name_zh_tw AS domain_name,
                       m.metric_code, m.value, m.unit, m.calculation_version, m.quality_flag
                FROM cognitive.derived_metric m
                JOIN cognitive.assessment_session s ON s.session_id = m.session_id
                JOIN ltc.player p ON p.player_id = s.player_id
                JOIN cognitive.game g ON g.game_id = s.game_id
                JOIN cognitive.cognitive_domain d ON d.domain_id = m.domain_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS cognitive.cognitive_trend;
                DROP VIEW IF EXISTS cognitive.assessment_summary;
                DROP VIEW IF EXISTS ltc.coin_balance;
                DROP VIEW IF EXISTS ltc.player_directory;
                """);

            migrationBuilder.DropTable(
                name: "coin_transaction",
                schema: "ltc");

            migrationBuilder.DropTable(
                name: "derived_metric",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "player_inventory",
                schema: "ltc");

            migrationBuilder.DropTable(
                name: "trial_event",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "item",
                schema: "ltc");

            migrationBuilder.DropTable(
                name: "assessment_session",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "game",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "player",
                schema: "ltc");

            migrationBuilder.DropTable(
                name: "cognitive_domain",
                schema: "cognitive");
        }
    }
}
