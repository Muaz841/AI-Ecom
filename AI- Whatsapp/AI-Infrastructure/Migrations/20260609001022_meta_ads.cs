using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class meta_ads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageGenerationPrompt",
                table: "TenantAIProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoseExtractionPrompt",
                table: "TenantAIProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageGenerationModelName",
                table: "PlatformAiConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessagingModelName",
                table: "PlatformAiConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionModelName",
                table: "PlatformAiConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContextSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<double>(type: "float(5)", precision: 5, scale: 4, nullable: false),
                    IsDryRun = table.Column<bool>(type: "bit", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutcomeLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformMarketingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaudeApiKeyProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaudeDecisionModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClaudeSummaryModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetaAdsAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetaAdsAccessTokenProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DryRun = table.Column<bool>(type: "bit", nullable: false),
                    MaxActionsPerDay = table.Column<int>(type: "int", nullable: false),
                    DailySpendCapUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformMarketingConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosePrescripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PoseScript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosePrescripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosePrescripts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisions_TenantId_RunAt",
                table: "AgentDecisions",
                columns: new[] { "TenantId", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisions_TenantId_Status",
                table: "AgentDecisions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_TenantId_IsActive",
                table: "KnowledgeChunks",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PosePrescripts_TenantId_IsActive",
                table: "PosePrescripts",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDecisions");

            migrationBuilder.DropTable(
                name: "KnowledgeChunks");

            migrationBuilder.DropTable(
                name: "PlatformMarketingConfigs");

            migrationBuilder.DropTable(
                name: "PosePrescripts");

            migrationBuilder.DropColumn(
                name: "ImageGenerationPrompt",
                table: "TenantAIProfiles");

            migrationBuilder.DropColumn(
                name: "PoseExtractionPrompt",
                table: "TenantAIProfiles");

            migrationBuilder.DropColumn(
                name: "ImageGenerationModelName",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "MessagingModelName",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "VisionModelName",
                table: "PlatformAiConfigs");
        }
    }
}
