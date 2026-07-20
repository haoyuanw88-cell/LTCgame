using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LtcCognitive.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCognitiveSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cognitive");

            migrationBuilder.CreateTable(
                name: "CognitiveDomains",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    NameZhTw = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveDomains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    ScreenWidth = table.Column<int>(type: "integer", nullable: true),
                    ScreenHeight = table.Column<int>(type: "integer", nullable: true),
                    Dpi = table.Column<float>(type: "real", nullable: true),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    NameZhTw = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnonymousCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    EducationBand = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameDomainMappings",
                schema: "cognitive",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingVersion = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    EvidenceNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameDomainMappings", x => new { x.GameId, x.DomainId, x.MappingVersion });
                    table.ForeignKey(
                        name: "FK_GameDomainMappings_CognitiveDomains_DomainId",
                        column: x => x.DomainId,
                        principalSchema: "cognitive",
                        principalTable: "CognitiveDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameDomainMappings_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "cognitive",
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentSessions",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskVersion = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    CompletionStatus = table.Column<string>(type: "text", nullable: false),
                    ExitReason = table.Column<string>(type: "text", nullable: true),
                    ClientTimezoneOffsetMinutes = table.Column<int>(type: "integer", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentSessions", x => x.Id);
                    table.CheckConstraint("ck_session_duration", "\"DurationMs\" >= 0");
                    table.ForeignKey(
                        name: "FK_AssessmentSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "cognitive",
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSessions_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "cognitive",
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssessmentSessions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "cognitive",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DerivedMetrics",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetricCode = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    CalculationVersion = table.Column<string>(type: "text", nullable: false),
                    QualityFlag = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DerivedMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DerivedMetrics_AssessmentSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "cognitive",
                        principalTable: "AssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DerivedMetrics_CognitiveDomains_DomainId",
                        column: x => x.DomainId,
                        principalSchema: "cognitive",
                        principalTable: "CognitiveDomains",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TrialEvents",
                schema: "cognitive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrialIndex = table.Column<int>(type: "integer", nullable: false),
                    TrialType = table.Column<string>(type: "text", nullable: true),
                    StimulusJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpectedResponse = table.Column<string>(type: "text", nullable: true),
                    ActualResponse = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    ReactionTimeMs = table.Column<int>(type: "integer", nullable: true),
                    PresentationDurationMs = table.Column<int>(type: "integer", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrialEvents_AssessmentSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "cognitive",
                        principalTable: "AssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "cognitive",
                table: "CognitiveDomains",
                columns: new[] { "Id", "Code", "Description", "NameZhTw" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "attention_inhibition", null, "注意力與抑制控制" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "processing_speed", null, "處理速度與視覺搜尋" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "executive_reasoning", null, "執行功能與數字推理" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "visual_working_memory", null, "視覺空間工作記憶" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "visuospatial_planning", null, "視空間規劃與問題解決" }
                });

            migrationBuilder.InsertData(
                schema: "cognitive",
                table: "Games",
                columns: new[] { "Id", "Code", "Description", "IsActive", "NameZhTw" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "stroop", null, true, "顏色文字判斷" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "number_order", null, true, "數字排序" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "number_combination", null, true, "數字組合" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "card_memory", null, true, "翻卡牌" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "pipe_rotation", null, true, "旋轉接水管" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSessions_DeviceId",
                schema: "cognitive",
                table: "AssessmentSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSessions_GameId",
                schema: "cognitive",
                table: "AssessmentSessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSessions_ParticipantId_StartedAtUtc",
                schema: "cognitive",
                table: "AssessmentSessions",
                columns: new[] { "ParticipantId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveDomains_Code",
                schema: "cognitive",
                table: "CognitiveDomains",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DerivedMetrics_DomainId",
                schema: "cognitive",
                table: "DerivedMetrics",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_DerivedMetrics_SessionId_MetricCode_DomainId",
                schema: "cognitive",
                table: "DerivedMetrics",
                columns: new[] { "SessionId", "MetricCode", "DomainId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_InstallationId",
                schema: "cognitive",
                table: "Devices",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameDomainMappings_DomainId",
                schema: "cognitive",
                table: "GameDomainMappings",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Code",
                schema: "cognitive",
                table: "Games",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_AnonymousCode",
                schema: "cognitive",
                table: "Participants",
                column: "AnonymousCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrialEvents_SessionId_TrialIndex",
                schema: "cognitive",
                table: "TrialEvents",
                columns: new[] { "SessionId", "TrialIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DerivedMetrics",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "GameDomainMappings",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "TrialEvents",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "CognitiveDomains",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "AssessmentSessions",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "Devices",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "cognitive");

            migrationBuilder.DropTable(
                name: "Participants",
                schema: "cognitive");
        }
    }
}
