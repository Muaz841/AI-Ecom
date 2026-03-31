using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingModelName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessagingModelName",
                table: "PlatformAiConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessagingModelName",
                table: "PlatformAiConfigs");
        }
    }
}
