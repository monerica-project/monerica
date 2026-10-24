using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationPaidXmr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidXmr",
                table: "VerificationRequests",
                type: "numeric(20,12)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidXmr",
                table: "Submissions",
                type: "numeric(20,12)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidXmr",
                table: "ProblemReports",
                type: "numeric(20,12)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidXmr",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaidXmr",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaidXmr",
                table: "ProblemReports");
        }
    }
}
