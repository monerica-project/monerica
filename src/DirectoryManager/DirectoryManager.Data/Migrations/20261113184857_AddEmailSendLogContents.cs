using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSendLogContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailSendLogContentId",
                table: "EmailSendLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailSendLogContents",
                columns: table => new
                {
                    EmailSendLogContentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReplyTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    BodyHtml = table.Column<string>(type: "text", nullable: true),
                    BodyTextTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    BodyHtmlTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    LastUsedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSendLogContents", x => x.EmailSendLogContentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_EmailSendLogContentId",
                table: "EmailSendLogs",
                column: "EmailSendLogContentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogContents_ContentHash",
                table: "EmailSendLogContents",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogContents_LastUsedUtc",
                table: "EmailSendLogContents",
                column: "LastUsedUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailSendLogs_EmailSendLogContents_EmailSendLogContentId",
                table: "EmailSendLogs",
                column: "EmailSendLogContentId",
                principalTable: "EmailSendLogContents",
                principalColumn: "EmailSendLogContentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailSendLogs_EmailSendLogContents_EmailSendLogContentId",
                table: "EmailSendLogs");

            migrationBuilder.DropTable(
                name: "EmailSendLogContents");

            migrationBuilder.DropIndex(
                name: "IX_EmailSendLogs_EmailSendLogContentId",
                table: "EmailSendLogs");

            migrationBuilder.DropColumn(
                name: "EmailSendLogContentId",
                table: "EmailSendLogs");
        }
    }
}
