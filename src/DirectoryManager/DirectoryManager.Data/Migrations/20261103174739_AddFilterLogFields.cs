using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterLogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KycPolicies",
                table: "DirectoryFilterLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Liquidities",
                table: "DirectoryFilterLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateTypes",
                table: "DirectoryFilterLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sort",
                table: "DirectoryFilterLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VerifiedOrders",
                table: "DirectoryFilterLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KycPolicies",
                table: "DirectoryFilterLogs");

            migrationBuilder.DropColumn(
                name: "Liquidities",
                table: "DirectoryFilterLogs");

            migrationBuilder.DropColumn(
                name: "RateTypes",
                table: "DirectoryFilterLogs");

            migrationBuilder.DropColumn(
                name: "Sort",
                table: "DirectoryFilterLogs");

            migrationBuilder.DropColumn(
                name: "VerifiedOrders",
                table: "DirectoryFilterLogs");
        }
    }
}
