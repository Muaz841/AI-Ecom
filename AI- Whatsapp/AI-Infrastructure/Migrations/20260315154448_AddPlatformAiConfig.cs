using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAiConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAiConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DebugModeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OllamaEndpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OllamaModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OpenAIModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OpenAIApiKeyProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeminiModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GeminiApiKeyProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAiConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAiConfigs");
        }
    }
}
