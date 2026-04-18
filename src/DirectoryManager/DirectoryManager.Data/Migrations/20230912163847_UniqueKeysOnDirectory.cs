using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueKeysOnDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntry_Category_CategoryId",
                table: "DirectoryEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntry_SubCategory_SubCategoryId",
                table: "DirectoryEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Category_CategoryId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_SubCategory_SubCategoryId",
                table: "Submissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubCategory",
                table: "SubCategory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DirectoryEntry",
                table: "DirectoryEntry");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Category",
                table: "Category");

            migrationBuilder.RenameTable(
                name: "SubCategory",
                newName: "SubCategories");

            migrationBuilder.RenameTable(
                name: "DirectoryEntry",
                newName: "DirectoryEntries");

            migrationBuilder.RenameTable(
                name: "Category",
                newName: "Categories");

            migrationBuilder.RenameIndex(
                name: "IX_DirectoryEntry_SubCategoryId",
                table: "DirectoryEntries",
                newName: "IX_DirectoryEntries_SubCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_DirectoryEntry_CategoryId",
                table: "DirectoryEntries",
                newName: "IX_DirectoryEntries_CategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubCategories",
                table: "SubCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DirectoryEntries",
                table: "DirectoryEntries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_SubCategoryKey",
                table: "SubCategories",
                column: "SubCategoryKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Link",
                table: "DirectoryEntries",
                column: "Link",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryKey",
                table: "Categories",
                column: "CategoryKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntries_Categories_CategoryId",
                table: "DirectoryEntries",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Categories_CategoryId",
                table: "Submissions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_SubCategories_SubCategoryId",
                table: "Submissions",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntries_Categories_CategoryId",
                table: "DirectoryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntries_SubCategories_SubCategoryId",
                table: "DirectoryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Categories_CategoryId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_SubCategories_SubCategoryId",
                table: "Submissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubCategories",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_SubCategories_SubCategoryKey",
                table: "SubCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DirectoryEntries",
                table: "DirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_Link",
                table: "DirectoryEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryKey",
                table: "Categories");

            migrationBuilder.RenameTable(
                name: "SubCategories",
                newName: "SubCategory");

            migrationBuilder.RenameTable(
                name: "DirectoryEntries",
                newName: "DirectoryEntry");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Category");

            migrationBuilder.RenameIndex(
                name: "IX_DirectoryEntries_SubCategoryId",
                table: "DirectoryEntry",
                newName: "IX_DirectoryEntry_SubCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_DirectoryEntries_CategoryId",
                table: "DirectoryEntry",
                newName: "IX_DirectoryEntry_CategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubCategory",
                table: "SubCategory",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DirectoryEntry",
                table: "DirectoryEntry",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Category",
                table: "Category",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntry_Category_CategoryId",
                table: "DirectoryEntry",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntry_SubCategory_SubCategoryId",
                table: "DirectoryEntry",
                column: "SubCategoryId",
                principalTable: "SubCategory",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Category_CategoryId",
                table: "Submissions",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_SubCategory_SubCategoryId",
                table: "Submissions",
                column: "SubCategoryId",
                principalTable: "SubCategory",
                principalColumn: "Id");
        }
    }
}
