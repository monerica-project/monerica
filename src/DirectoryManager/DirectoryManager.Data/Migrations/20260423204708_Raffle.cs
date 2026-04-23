using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Raffle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RaffleId",
                table: "DirectoryEntryReviewRaffleEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Raffles",
                columns: table => new
                {
                    RaffleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raffles", x => x.RaffleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaffleEntry_RaffleId",
                table: "DirectoryEntryReviewRaffleEntries",
                column: "RaffleId");

            migrationBuilder.CreateIndex(
                name: "IX_Raffle_IsEnabled",
                table: "Raffles",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Raffle_StartEnd",
                table: "Raffles",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Raffle_Name",
                table: "Raffles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntryReviewRaffleEntries_Raffles_RaffleId",
                table: "DirectoryEntryReviewRaffleEntries",
                column: "RaffleId",
                principalTable: "Raffles",
                principalColumn: "RaffleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntryReviewRaffleEntries_Raffles_RaffleId",
                table: "DirectoryEntryReviewRaffleEntries");

            migrationBuilder.DropTable(
                name: "Raffles");

            migrationBuilder.DropIndex(
                name: "IX_RaffleEntry_RaffleId",
                table: "DirectoryEntryReviewRaffleEntries");

            migrationBuilder.DropColumn(
                name: "RaffleId",
                table: "DirectoryEntryReviewRaffleEntries");
        }
    }
}
