using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalSubcategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryEntryAdditionalSubcategories",
                columns: table => new
                {
                    DirectoryEntryAdditionalSubcategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkA = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link2A = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3A = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryAdditionalSubcategories", x => x.DirectoryEntryAdditionalSubcategoryId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryAdditionalSubcategories_DirectoryEntries_Dire~",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryAdditionalSubcategories_Subcategories_SubCate~",
                        column: x => x.SubCategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "SubCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryAdditionalSubcategories_DirectoryEntryId_SubC~",
                table: "DirectoryEntryAdditionalSubcategories",
                columns: new[] { "DirectoryEntryId", "SubCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryAdditionalSubcategories_SubCategoryId",
                table: "DirectoryEntryAdditionalSubcategories",
                column: "SubCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryEntryAdditionalSubcategories");
        }
    }
}
