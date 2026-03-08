using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MetaOAuthIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaChannelConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalBusinessId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccessTokenCiphertext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshTokenCiphertext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScopesCsv = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaChannelConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetaOAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaOAuthStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelConnections_TenantId_Channel",
                table: "MetaChannelConnections",
                columns: new[] { "TenantId", "Channel" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetaChannelConnections_TenantId_Status",
                table: "MetaChannelConnections",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MetaOAuthStates_State",
                table: "MetaOAuthStates",
                column: "State",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetaOAuthStates_TenantId_Channel_ExpiresAtUtc",
                table: "MetaOAuthStates",
                columns: new[] { "TenantId", "Channel", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetaChannelConnections");

            migrationBuilder.DropTable(
                name: "MetaOAuthStates");
        }
    }
}
