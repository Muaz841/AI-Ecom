#nullable disable
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AI_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MetaOAuthStateReturnUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnUrl",
                table: "MetaOAuthStates",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnUrl",
                table: "MetaOAuthStates");
        }
    }
}
