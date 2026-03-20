using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnumConversionsForMessageAndThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop columns that may exist from a previously reverted migration.
            // Uses conditional SQL so the migration is safe on both fresh and existing DBs.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME='PlatformAiConfigs' AND COLUMN_NAME='UseExternalMessaging')
                    ALTER TABLE [PlatformAiConfigs] DROP COLUMN [UseExternalMessaging];

                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME='Messages' AND COLUMN_NAME='SentExternally')
                    ALTER TABLE [Messages] DROP COLUMN [SentExternally];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
    }
}
