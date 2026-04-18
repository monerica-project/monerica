using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Reviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviews",
                columns: table => new
                {
                    DirectoryEntryReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirectoryEntryId = table.Column<int>(type: "int", nullable: false),
                    AuthorFingerprint = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AuthorPublicKeyArmor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorHandle = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DisplayNameSignatureArmor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<byte>(type: "tinyint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModerationStatus = table.Column<int>(type: "int", nullable: false),
                    PostSignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeletionSignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceIpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviews", x => x.DirectoryEntryReviewId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviews_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerKeys",
                columns: table => new
                {
                    ReviewerKeyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublicKeyBlock = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerKeys", x => x.ReviewerKeyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_AuthorFingerprint",
                table: "DirectoryEntryReviews",
                column: "AuthorFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId",
                table: "DirectoryEntryReviews",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_DirectoryEntryId_ModerationStatus",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerKeys_Fingerprint",
                table: "ReviewerKeys",
                column: "Fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryReviews");

            migrationBuilder.DropTable(
                name: "ReviewerKeys");
        }
    }
}
