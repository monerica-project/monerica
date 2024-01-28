using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SponsoredListingReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "AspNetUserRoles",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "SponsoredListingReservations",
                columns: table => new
                {
                    SponsoredListingReservationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpirationDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingReservations", x => x.SponsoredListingReservationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingReservations_ExpirationDateTime",
                table: "SponsoredListingReservations",
                column: "ExpirationDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingReservations_ReservationGuid",
                table: "SponsoredListingReservations",
                column: "ReservationGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SponsoredListingReservations");

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "AspNetUserRoles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(34)",
                oldMaxLength: 34);
        }
    }
}
