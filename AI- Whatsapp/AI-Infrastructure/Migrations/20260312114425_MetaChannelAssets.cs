using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MetaChannelAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaChannelAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PageAccessTokenCiphertext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaChannelAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetaChannelAssets_MetaChannelConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "MetaChannelConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelAssets_ConnectionId",
                table: "MetaChannelAssets",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelAssets_ExternalId_Channel_IsActive",
                table: "MetaChannelAssets",
                columns: new[] { "ExternalId", "Channel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelAssets_TenantId_Channel_IsActive",
                table: "MetaChannelAssets",
                columns: new[] { "TenantId", "Channel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelAssets_TenantId_ConnectionId_AssetType_ExternalId",
                table: "MetaChannelAssets",
                columns: new[] { "TenantId", "ConnectionId", "AssetType", "ExternalId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetaChannelAssets");
        }
    }
}
