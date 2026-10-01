using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositGuaranteesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuaranteesJson",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DirectoryEntryGuarantees",
                columns: table => new
                {
                    DirectoryEntryGuaranteeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AmountUsd = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryGuarantees", x => x.DirectoryEntryGuaranteeId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryGuarantees_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryGuarantees_DirectoryEntryId_SortOrder",
                table: "DirectoryEntryGuarantees",
                columns: new[] { "DirectoryEntryId", "SortOrder" },
                unique: true);

            // Migrate the single scalar guarantee (from the prior migration) into the new table as
            // each listing's first guarantee, BEFORE dropping the scalar columns. Only rows with
            // both a link and a positive amount carry over.
            migrationBuilder.Sql(@"
                INSERT INTO ""DirectoryEntryGuarantees"" (""DirectoryEntryId"", ""SortOrder"", ""Link"", ""AmountUsd"", ""CreateDate"", ""CreatedByUserId"")
                SELECT ""DirectoryEntryId"", 1, ""GuaranteeLink"", COALESCE(""GuaranteeAmountUSD"", 0), NOW() AT TIME ZONE 'UTC', ''
                FROM ""DirectoryEntries""
                WHERE ""GuaranteeLink"" IS NOT NULL AND ""GuaranteeLink"" <> '' AND COALESCE(""GuaranteeAmountUSD"", 0) > 0;");

            migrationBuilder.DropColumn(name: "GuaranteeAmountUSD", table: "Submissions");
            migrationBuilder.DropColumn(name: "GuaranteeLink", table: "Submissions");
            migrationBuilder.DropColumn(name: "GuaranteeAmountUSD", table: "DirectoryEntriesAudit");
            migrationBuilder.DropColumn(name: "GuaranteeLink", table: "DirectoryEntriesAudit");
            migrationBuilder.DropColumn(name: "GuaranteeAmountUSD", table: "DirectoryEntries");
            migrationBuilder.DropColumn(name: "GuaranteeLink", table: "DirectoryEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryGuarantees");

            migrationBuilder.DropColumn(
                name: "GuaranteesJson",
                table: "Submissions");

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
        }
    }
}
