using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class TagDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "Tags",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 7, 6, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateDate",
                table: "Tags",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "DirectoryEntryTags",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 7, 6, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateDate",
                table: "DirectoryEntryTags",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "UpdateDate",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "DirectoryEntryTags");

            migrationBuilder.DropColumn(
                name: "UpdateDate",
                table: "DirectoryEntryTags");
        }
    }
}
