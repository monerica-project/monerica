using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorInterestLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SponsorInterestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    SponsorshipType = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: true),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorInterestLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsorInterestLogs_CreateDate",
                table: "SponsorInterestLogs",
                column: "CreateDate");

            migrationBuilder.CreateIndex(
                name: "IX_SponsorInterestLogs_DirectoryEntryId_CreateDate",
                table: "SponsorInterestLogs",
                columns: new[] { "DirectoryEntryId", "CreateDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SponsorInterestLogs");
        }
    }
}
