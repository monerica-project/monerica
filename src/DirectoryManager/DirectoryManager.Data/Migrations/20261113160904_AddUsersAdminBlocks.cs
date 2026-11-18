using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAdminBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostingBlockReason",
                table: "AspNetUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingBlockedBy",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostingBlockedUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingUnblockedBy",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostingUnblockedUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlockedReviewerKeys",
                columns: table => new
                {
                    BlockedReviewerKeyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BlockedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HidPosts = table.Column<bool>(type: "boolean", nullable: false),
                    UnblockedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnblockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedReviewerKeys", x => x.BlockedReviewerKeyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_AuthorFingerprint",
                table: "DirectoryEntryReviewComments",
                column: "AuthorFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedReviewerKeys_Fingerprint",
                table: "BlockedReviewerKeys",
                column: "Fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedReviewerKeys");

            migrationBuilder.DropIndex(
                name: "IX_ReviewComments_AuthorFingerprint",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "PostingBlockReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostingBlockedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostingBlockedUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostingUnblockedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostingUnblockedUtc",
                table: "AspNetUsers");
        }
    }
}
