using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteCheckStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteCheckStatuses",
                columns: table => new
                {
                    SiteCheckStatusId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    ClearnetFailStreak = table.Column<int>(type: "integer", nullable: false),
                    OnionFailStreak = table.Column<int>(type: "integer", nullable: false),
                    LastCheckedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteCheckStatuses", x => x.SiteCheckStatusId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteCheckStatuses_DirectoryEntryId",
                table: "SiteCheckStatuses",
                column: "DirectoryEntryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteCheckStatuses");
        }
    }
}
