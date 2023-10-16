using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SponsoredListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SponsoredListingInvoices",
                columns: table => new
                {
                    SponsoredListingInvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectoryEntryId = table.Column<int>(type: "int", nullable: false),
                    CampaignStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CampaignEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    PaymentProcessor = table.Column<int>(type: "int", nullable: false),
                    ProcessorInvoiceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    InvoiceRequest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingInvoices", x => x.SponsoredListingInvoiceId);
                    table.ForeignKey(
                        name: "FK_SponsoredListingInvoices_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListings",
                columns: table => new
                {
                    SponsoredListingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CampaignEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DirectoryEntryId = table.Column<int>(type: "int", nullable: false),
                    SponsoredListingInvoiceId = table.Column<int>(type: "int", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListings", x => x.SponsoredListingId);
                    table.ForeignKey(
                        name: "FK_SponsoredListings_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SponsoredListings_SponsoredListingInvoices_SponsoredListingInvoiceId",
                        column: x => x.SponsoredListingInvoiceId,
                        principalTable: "SponsoredListingInvoices",
                        principalColumn: "SponsoredListingInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingInvoices_DirectoryEntryId",
                table: "SponsoredListingInvoices",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_DirectoryEntryId",
                table: "SponsoredListings",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_SponsoredListingInvoiceId",
                table: "SponsoredListings",
                column: "SponsoredListingInvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SponsoredListings");

            migrationBuilder.DropTable(
                name: "SponsoredListingInvoices");
        }
    }
}
