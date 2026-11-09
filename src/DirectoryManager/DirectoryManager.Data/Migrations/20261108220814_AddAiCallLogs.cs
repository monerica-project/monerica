using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCallLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiCallLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Feature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubmissionId = table.Column<int>(type: "integer", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    StopReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Verdict = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    SystemPromptChars = table.Column<int>(type: "integer", nullable: false),
                    RequestText = table.Column<string>(type: "text", nullable: true),
                    ResponseText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCallLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiCallLogs_CreatedUtc",
                table: "AiCallLogs",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiCallLogs_SubmissionId",
                table: "AiCallLogs",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCallLogs");
        }
    }
}
