using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_AuthEntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "InviteTokens",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedById",
                table: "InviteTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "InviteTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_InviteTokens_InvitedById",
                table: "InviteTokens",
                column: "InvitedById");

            migrationBuilder.AddForeignKey(
                name: "FK_InviteTokens_Users_InvitedById",
                table: "InviteTokens",
                column: "InvitedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InviteTokens_Users_InvitedById",
                table: "InviteTokens");

            migrationBuilder.DropIndex(
                name: "IX_InviteTokens_InvitedById",
                table: "InviteTokens");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "InviteTokens");

            migrationBuilder.DropColumn(
                name: "InvitedById",
                table: "InviteTokens");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "InviteTokens");
        }
    }
}
