using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidityAndGuaranteeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuaranteeAmountUSD",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuaranteeLink",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Liquidity",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GuaranteeAmountUSD",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuaranteeLink",
                table: "DirectoryEntriesAudit",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Liquidity",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GuaranteeAmountUSD",
                table: "DirectoryEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuaranteeLink",
                table: "DirectoryEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Liquidity",
                table: "DirectoryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuaranteeAmountUSD",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "GuaranteeLink",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Liquidity",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "GuaranteeAmountUSD",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "GuaranteeLink",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "Liquidity",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "GuaranteeAmountUSD",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "GuaranteeLink",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "Liquidity",
                table: "DirectoryEntries");
        }
    }
}
