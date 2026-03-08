using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantClientSecretsRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [Tenants] ([Id], [Name], [BusinessName], [CreatedAt], [LastSyncedAt], [TenantId])
                SELECT [Id], [Name], [BusinessName], [CreatedAt], [LastSyncedAt], COALESCE([TenantId], [Id])
                FROM [Clients];
                """);

            migrationBuilder.CreateTable(
                name: "ClientsSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantRefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaPageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShopifyStoreId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WooCommerceStoreId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientsSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientsSecrets_Tenants_TenantRefId",
                        column: x => x.TenantRefId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [ClientsSecrets] (
                    [Id], [TenantRefId], [MetaAccessToken], [MetaPageId], [WhatsAppBusinessAccountId],
                    [ShopifyStoreId], [WooCommerceStoreId], [CreatedAt], [LastSyncedAt], [TenantId])
                SELECT
                    [Id], [Id], [MetaAccessToken], [MetaPageId], [WhatsAppBusinessAccountId],
                    [ShopifyStoreId], [WooCommerceStoreId], [CreatedAt], [LastSyncedAt], COALESCE([TenantId], [Id])
                FROM [Clients];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_ClientId",
                table: "UserAccounts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientsSecrets_TenantId",
                table: "ClientsSecrets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientsSecrets_TenantRefId",
                table: "ClientsSecrets",
                column: "TenantRefId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_Tenants_ClientId",
                table: "UserAccounts",
                column: "ClientId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_Tenants_ClientId",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_ClientId",
                table: "UserAccounts");

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetaAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaPageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShopifyStoreId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WooCommerceStoreId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [Clients] (
                    [Id], [BusinessName], [CreatedAt], [LastSyncedAt], [MetaAccessToken], [MetaPageId],
                    [Name], [ShopifyStoreId], [TenantId], [WhatsAppBusinessAccountId], [WooCommerceStoreId])
                SELECT
                    [t].[Id],
                    [t].[BusinessName],
                    [t].[CreatedAt],
                    [t].[LastSyncedAt],
                    [cs].[MetaAccessToken],
                    [cs].[MetaPageId],
                    [t].[Name],
                    [cs].[ShopifyStoreId],
                    [t].[TenantId],
                    [cs].[WhatsAppBusinessAccountId],
                    [cs].[WooCommerceStoreId]
                FROM [Tenants] AS [t]
                LEFT JOIN [ClientsSecrets] AS [cs] ON [cs].[TenantRefId] = [t].[Id];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId",
                table: "Clients",
                column: "TenantId");

            migrationBuilder.DropTable(
                name: "ClientsSecrets");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
