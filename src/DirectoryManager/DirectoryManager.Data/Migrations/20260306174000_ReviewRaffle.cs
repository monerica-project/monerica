using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewRaffle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewRaffleEntries",
                columns: table => new
                {
                    DirectoryEntryReviewRaffleEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirectoryEntryReviewId = table.Column<int>(type: "int", nullable: false),
                    CryptoType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CryptoAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewRaffleEntries", x => x.DirectoryEntryReviewRaffleEntryId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewRaffleEntries_DirectoryEntryReviews_DirectoryEntryReviewId",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaffleEntry_Status_Create_Id",
                table: "DirectoryEntryReviewRaffleEntries",
                columns: new[] { "Status", "CreateDate", "DirectoryEntryReviewRaffleEntryId" });

            migrationBuilder.CreateIndex(
                name: "UX_RaffleEntry_Review",
                table: "DirectoryEntryReviewRaffleEntries",
                column: "DirectoryEntryReviewId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewRaffleEntries");
        }
    }
}
