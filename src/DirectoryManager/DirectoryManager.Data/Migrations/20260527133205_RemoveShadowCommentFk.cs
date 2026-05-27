using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShadowCommentFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments",
                column: "DirectoryEntryReviewCommentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments",
                column: "DirectoryEntryReviewCommentId1",
                principalTable: "DirectoryEntryReviewComments",
                principalColumn: "DirectoryEntryReviewCommentId");
        }
    }
}
