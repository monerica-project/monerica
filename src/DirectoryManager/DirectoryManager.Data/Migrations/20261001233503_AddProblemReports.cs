using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProblemReports",
                columns: table => new
                {
                    ProblemReportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PaymentToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BtcPayInvoiceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaidUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    PaidCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemReports", x => x.ProblemReportId);
                    table.ForeignKey(
                        name: "FK_ProblemReports_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_DirectoryEntryId",
                table: "ProblemReports",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_PaymentToken",
                table: "ProblemReports",
                column: "PaymentToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_Status_Create_Id",
                table: "ProblemReports",
                columns: new[] { "Status", "CreateDate", "ProblemReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemReports");
        }
    }
}
