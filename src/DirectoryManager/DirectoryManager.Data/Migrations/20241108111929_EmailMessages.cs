using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    EmailMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmailSubject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EmailBodyText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailBodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.EmailMessageId);
                });

            migrationBuilder.CreateTable(
                name: "SentEmailRecords",
                columns: table => new
                {
                    SentEmailRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    EmailMessageId = table.Column<int>(type: "int", nullable: false),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentEmailRecords", x => x.SentEmailRecordId);
                    table.ForeignKey(
                        name: "FK_SentEmailRecords_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "EmailMessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SentEmailRecords_EmailSubscriptions_EmailSubscriptionId",
                        column: x => x.EmailSubscriptionId,
                        principalTable: "EmailSubscriptions",
                        principalColumn: "EmailSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntriesAudit_SubCategoryId",
                table: "DirectoryEntriesAudit",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_EmailKey",
                table: "EmailMessages",
                column: "EmailKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailMessageId",
                table: "SentEmailRecords",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId",
                table: "SentEmailRecords",
                column: "EmailSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntriesAudit_SubCategories_SubCategoryId",
                table: "DirectoryEntriesAudit",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "SubCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntriesAudit_SubCategories_SubCategoryId",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropTable(
                name: "SentEmailRecords");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntriesAudit_SubCategoryId",
                table: "DirectoryEntriesAudit");
        }
    }
}
