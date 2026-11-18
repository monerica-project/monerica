using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberPgpKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PgpFingerprint",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PgpLinkedUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PgpFingerprint",
                table: "AspNetUsers",
                column: "PgpFingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PgpFingerprint",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PgpFingerprint",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PgpLinkedUtc",
                table: "AspNetUsers");
        }
    }
}
