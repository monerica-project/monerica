using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeSubCatUniqueOnKeyCat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubCategories_SubCategoryKey",
                table: "SubCategories");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_SubCategoryKey_CategoryId",
                table: "SubCategories",
                columns: new[] { "SubCategoryKey", "CategoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubCategories_SubCategoryKey_CategoryId",
                table: "SubCategories");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_SubCategoryKey",
                table: "SubCategories",
                column: "SubCategoryKey",
                unique: true);
        }
    }
}
