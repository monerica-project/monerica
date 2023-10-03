using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SelectedDirectoryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryEntrySelections",
                columns: table => new
                {
                    DirectoryEntrySelectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirectoryEntryId = table.Column<int>(type: "int", nullable: false),
                    EntrySelectionType = table.Column<int>(type: "int", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntrySelections", x => x.DirectoryEntrySelectionId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntrySelections_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntrySelections_DirectoryEntryId",
                table: "DirectoryEntrySelections",
                column: "DirectoryEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntrySelections");
        }
    }
}
