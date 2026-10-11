using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSponsorships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoIdeas",
                columns: table => new
                {
                    VideoIdeaId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDedicated = table.Column<bool>(type: "boolean", nullable: false),
                    HeldByOrderId = table.Column<int>(type: "integer", nullable: true),
                    HeldUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SponsoredByOrderId = table.Column<int>(type: "integer", nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoIdeas", x => x.VideoIdeaId);
                });

            migrationBuilder.CreateTable(
                name: "VideoSponsorOffers",
                columns: table => new
                {
                    VideoSponsorOfferId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PriceUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequiresVideoSelection = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSponsorOffers", x => x.VideoSponsorOfferId);
                });

            migrationBuilder.CreateTable(
                name: "VideoSponsorOrders",
                columns: table => new
                {
                    VideoSponsorOrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    VideoSponsorOfferId = table.Column<int>(type: "integer", nullable: false),
                    OfferName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountBasisTier = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    PaidInCurrency = table.Column<int>(type: "integer", nullable: false),
                    PaymentRate = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    PaidUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessorInvoiceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    InvoiceExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoiceDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InvoiceRequest = table.Column<string>(type: "text", nullable: false),
                    InvoiceResponse = table.Column<string>(type: "text", nullable: false),
                    PaymentResponse = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RefundAddress = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HasConflict = table.Column<bool>(type: "boolean", nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSponsorOrders", x => x.VideoSponsorOrderId);
                    table.ForeignKey(
                        name: "FK_VideoSponsorOrders_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoSponsorOrders_VideoSponsorOffers_VideoSponsorOfferId",
                        column: x => x.VideoSponsorOfferId,
                        principalTable: "VideoSponsorOffers",
                        principalColumn: "VideoSponsorOfferId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoSponsorOrderItems",
                columns: table => new
                {
                    VideoSponsorOrderItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VideoSponsorOrderId = table.Column<int>(type: "integer", nullable: false),
                    VideoIdeaId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionEmailSentUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionEmailResult = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSponsorOrderItems", x => x.VideoSponsorOrderItemId);
                    table.ForeignKey(
                        name: "FK_VideoSponsorOrderItems_VideoIdeas_VideoIdeaId",
                        column: x => x.VideoIdeaId,
                        principalTable: "VideoIdeas",
                        principalColumn: "VideoIdeaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoSponsorOrderItems_VideoSponsorOrders_VideoSponsorOrder~",
                        column: x => x.VideoSponsorOrderId,
                        principalTable: "VideoSponsorOrders",
                        principalColumn: "VideoSponsorOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoIdeas_Status_Sort_Id",
                table: "VideoIdeas",
                columns: new[] { "Status", "SortOrder", "VideoIdeaId" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrderItems_VideoIdeaId",
                table: "VideoSponsorOrderItems",
                column: "VideoIdeaId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrderItems_VideoSponsorOrderId",
                table: "VideoSponsorOrderItems",
                column: "VideoSponsorOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrders_DirectoryEntryId",
                table: "VideoSponsorOrders",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrders_InvoiceId",
                table: "VideoSponsorOrders",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrders_ProcessorInvoiceId",
                table: "VideoSponsorOrders",
                column: "ProcessorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrders_Status_Create",
                table: "VideoSponsorOrders",
                columns: new[] { "PaymentStatus", "CreateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoSponsorOrders_VideoSponsorOfferId",
                table: "VideoSponsorOrders",
                column: "VideoSponsorOfferId");

            // Starting video types (prices are editable in the admin afterwards).
            migrationBuilder.Sql(@"
INSERT INTO ""VideoSponsorOffers"" (""Name"", ""Description"", ""PriceUsd"", ""RequiresVideoSelection"", ""IsEnabled"", ""SortOrder"", ""CreateDate"")
VALUES
('1-minute sponsor segment', 'A 1-minute sponsor segment in the middle of an upcoming video. Choose one or more videos; priced per video.', 1000.00, TRUE, TRUE, 1, NOW()),
('Dedicated video', 'A dedicated video of about 5 minutes made for your listing. Describe what you want and we produce it.', 2500.00, FALSE, TRUE, 2, NOW());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoSponsorOrderItems");

            migrationBuilder.DropTable(
                name: "VideoIdeas");

            migrationBuilder.DropTable(
                name: "VideoSponsorOrders");

            migrationBuilder.DropTable(
                name: "VideoSponsorOffers");
        }
    }
}
