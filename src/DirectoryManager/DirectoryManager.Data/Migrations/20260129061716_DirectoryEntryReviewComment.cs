using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectoryEntryReviewComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewComments",
                columns: table => new
                {
                    DirectoryEntryReviewCommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirectoryEntryReviewId = table.Column<int>(type: "int", nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ModerationStatus = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    AuthorFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DirectoryEntryReviewCommentId1 = table.Column<int>(type: "int", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewComments", x => x.DirectoryEntryReviewCommentId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                        column: x => x.DirectoryEntryReviewCommentId1,
                        principalTable: "DirectoryEntryReviewComments",
                        principalColumn: "DirectoryEntryReviewCommentId");
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviewComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "DirectoryEntryReviewComments",
                        principalColumn: "DirectoryEntryReviewCommentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviews_DirectoryEntryReviewId",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewCommentId1",
                table: "DirectoryEntryReviewComments",
                column: "DirectoryEntryReviewCommentId1");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewComments_DirectoryEntryReviewId",
                table: "DirectoryEntryReviewComments",
                column: "DirectoryEntryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewComments_ParentCommentId",
                table: "DirectoryEntryReviewComments",
                column: "ParentCommentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewComments");
        }
    }
}
