using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConfigAdvancedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableStructuredOutput",
                table: "PlatformAiConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableToolCalling",
                table: "PlatformAiConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxTokens",
                table: "PlatformAiConfigs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Temperature",
                table: "PlatformAiConfigs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TopP",
                table: "PlatformAiConfigs",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableStructuredOutput",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "EnableToolCalling",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "MaxTokens",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "TopP",
                table: "PlatformAiConfigs");
        }
    }
}
