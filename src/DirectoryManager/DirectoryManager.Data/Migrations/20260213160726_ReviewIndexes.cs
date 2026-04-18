using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryTags_TagId",
                table: "DirectoryEntryTags");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId_ModerationStatus",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewId",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_DirectoryEntryId_DirectoryStatus",
                table: "DirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_SubCategoryId_DirectoryEntryKey",
                table: "DirectoryEntries");

            migrationBuilder.RenameIndex(
                name: "IX_DirectoryEntryReviewComments_ParentCommentId",
                table: "DirectoryEntryReviewComments",
                newName: "IX_ReviewComments_ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryTags_Tag_Entry",
                table: "DirectoryEntryTags",
                columns: new[] { "TagId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Entry_Mod_Create_Id",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "ModerationStatus", "CreateDate", "DirectoryEntryReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Mod_Entry",
                table: "DirectoryEntryReviews",
                columns: new[] { "ModerationStatus", "DirectoryEntryId" })
                .Annotation("SqlServer:Include", new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_Mod_Review",
                table: "DirectoryEntryReviewComments",
                columns: new[] { "ModerationStatus", "DirectoryEntryReviewId" })
                .Annotation("SqlServer:Include", new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_Review_Mod_Create_Id",
                table: "DirectoryEntryReviewComments",
                columns: new[] { "DirectoryEntryReviewId", "ModerationStatus", "CreateDate", "DirectoryEntryReviewCommentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Status",
                table: "DirectoryEntries",
                column: "DirectoryStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Update_Create",
                table: "DirectoryEntries",
                columns: new[] { "UpdateDate", "CreateDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntryTags_Tag_Entry",
                table: "DirectoryEntryTags");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_Entry_Mod_Create_Id",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_Mod_Entry",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_ReviewComments_Mod_Review",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewComments_Review_Mod_Create_Id",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_Status",
                table: "DirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_Update_Create",
                table: "DirectoryEntries");

            migrationBuilder.RenameIndex(
                name: "IX_ReviewComments_ParentCommentId",
                table: "DirectoryEntryReviewComments",
                newName: "IX_DirectoryEntryReviewComments_ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryTags_TagId",
                table: "DirectoryEntryTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId",
                table: "DirectoryEntryReviews",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId_ModerationStatus",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewId",
                table: "DirectoryEntryReviewComments",
                column: "DirectoryEntryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_DirectoryEntryId_DirectoryStatus",
                table: "DirectoryEntries",
                columns: new[] { "DirectoryEntryId", "DirectoryStatus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_SubCategoryId_DirectoryEntryKey",
                table: "DirectoryEntries",
                columns: new[] { "SubCategoryId", "DirectoryEntryKey" },
                unique: true);
        }
    }
}
