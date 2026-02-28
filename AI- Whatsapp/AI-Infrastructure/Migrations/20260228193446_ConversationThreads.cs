using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConversationThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConversationThreadId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "Messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "Messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalMessageId",
                table: "Messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "Messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ConversationThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastMessageDirection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    AssignmentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationThreads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ConversationThreadId_ReceivedAt",
                table: "Messages",
                columns: new[] { "TenantId", "ConversationThreadId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ExternalMessageId",
                table: "Messages",
                columns: new[] { "TenantId", "ExternalMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_TenantId_LastMessageAt",
                table: "ConversationThreads",
                columns: new[] { "TenantId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_TenantId_Platform_CustomerIdentifier_BusinessIdentifier",
                table: "ConversationThreads",
                columns: new[] { "TenantId", "Platform", "CustomerIdentifier", "BusinessIdentifier" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationThreads");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_ConversationThreadId_ReceivedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_ExternalMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ConversationThreadId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ExternalMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");
        }
    }
}
