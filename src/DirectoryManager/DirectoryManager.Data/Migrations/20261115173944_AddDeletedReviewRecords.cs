using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedReviewRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedReviewRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OriginalId = table.Column<int>(type: "integer", nullable: false),
                    ParentReviewId = table.Column<int>(type: "integer", nullable: true),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    ListingName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    AuthorUsername = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AuthorFingerprint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Rating = table.Column<byte>(type: "smallint", nullable: true),
                    StatusAtDelete = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true),
                    RestoredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestoredAsId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedReviewRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedReviewRecords_DeletedUtc",
                table: "DeletedReviewRecords",
                column: "DeletedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedReviewRecords");
        }
    }
}
