using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "DirectoryEntryReviews",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderUrl",
                table: "DirectoryEntryReviews",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewTags",
                columns: table => new
                {
                    ReviewTagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewTags", x => x.ReviewTagId);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewTags",
                columns: table => new
                {
                    DirectoryEntryReviewId = table.Column<int>(type: "int", nullable: false),
                    ReviewTagId = table.Column<int>(type: "int", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewTags", x => new { x.DirectoryEntryReviewId, x.ReviewTagId });
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewTags_DirectoryEntryReviews_DirectoryEntryReviewId",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewTags_ReviewTags_ReviewTagId",
                        column: x => x.ReviewTagId,
                        principalTable: "ReviewTags",
                        principalColumn: "ReviewTagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewTags_ReviewTagId",
                table: "DirectoryEntryReviewTags",
                column: "ReviewTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTags_Slug",
                table: "ReviewTags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewTags");

            migrationBuilder.DropTable(
                name: "ReviewTags");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "OrderUrl",
                table: "DirectoryEntryReviews");
        }
    }
}
