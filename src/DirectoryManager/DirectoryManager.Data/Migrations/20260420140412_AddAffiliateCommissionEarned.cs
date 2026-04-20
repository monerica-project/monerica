using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateCommissionEarned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateCommissionsEarned",
                columns: table => new
                {
                    AffiliateCommissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirectoryEntryId = table.Column<int>(type: "int", nullable: false),
                    CommissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsdValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentCurrency = table.Column<int>(type: "int", nullable: false),
                    PaymentCurrencyAmount = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissionsEarned", x => x.AffiliateCommissionId);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionsEarned_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_Date",
                table: "AffiliateCommissionsEarned",
                column: "CommissionDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_Entry_Date",
                table: "AffiliateCommissionsEarned",
                columns: new[] { "DirectoryEntryId", "CommissionDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_TransactionId",
                table: "AffiliateCommissionsEarned",
                column: "TransactionId",
                filter: "[TransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateCommissionsEarned");
        }
    }
}
