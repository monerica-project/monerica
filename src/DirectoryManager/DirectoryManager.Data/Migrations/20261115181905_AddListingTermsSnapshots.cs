using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingTermsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingTermsSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    LastCheckedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReadUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Pages = table.Column<string>(type: "text", nullable: true),
                    Sentences = table.Column<string>(type: "text", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Diff = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Suggestion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NeedsReview = table.Column<bool>(type: "boolean", nullable: false),
                    HandledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadFailures = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingTermsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingTermsSnapshots_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingTermsSnapshots_DirectoryEntryId",
                table: "ListingTermsSnapshots",
                column: "DirectoryEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingTermsSnapshots_NeedsReview",
                table: "ListingTermsSnapshots",
                column: "NeedsReview");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingTermsSnapshots");
        }
    }
}
