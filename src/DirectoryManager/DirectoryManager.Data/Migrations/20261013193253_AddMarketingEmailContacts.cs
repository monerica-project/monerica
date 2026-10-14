using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingEmailContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketingEmailContacts",
                columns: table => new
                {
                    MarketingEmailContactId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OptedOutUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OptOutSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingEmailContacts", x => x.MarketingEmailContactId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingEmailContacts_Email",
                table: "MarketingEmailContacts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingEmailContacts_Token",
                table: "MarketingEmailContacts",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingEmailContacts");
        }
    }
}
