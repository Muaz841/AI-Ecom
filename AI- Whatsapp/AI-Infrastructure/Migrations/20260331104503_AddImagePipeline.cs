using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImagePipeline : Migration
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
                name: "VisionModelName",
                table: "PlatformAiConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

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
                name: "IX_PosePrescripts_TenantId_IsActive",
                table: "PosePrescripts",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "VisionModelName",
                table: "PlatformAiConfigs");
        }
    }
}
