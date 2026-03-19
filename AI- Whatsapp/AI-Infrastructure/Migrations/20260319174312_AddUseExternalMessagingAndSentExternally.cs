using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUseExternalMessagingAndSentExternally : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseExternalMessaging",
                table: "PlatformAiConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SentExternally",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseExternalMessaging",
                table: "PlatformAiConfigs");

            migrationBuilder.DropColumn(
                name: "SentExternally",
                table: "Messages");
        }
    }
}
