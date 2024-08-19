using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectoryEntryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries");

            migrationBuilder.AlterColumn<int>(
                name: "SubCategoryId",
                table: "DirectoryEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectoryEntryKey",
                table: "DirectoryEntries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "SubCategoryId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "DirectoryEntryKey",
                table: "DirectoryEntries");

            migrationBuilder.AlterColumn<int>(
                name: "SubCategoryId",
                table: "DirectoryEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "SubCategoryId");
        }
    }
}
