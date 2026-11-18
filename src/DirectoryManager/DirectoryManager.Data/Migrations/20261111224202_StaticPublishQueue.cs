using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaticPublishQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaticPublishLog",
                columns: table => new
                {
                    StaticPublishLogEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaticPublishLog", x => x.StaticPublishLogEntryId);
                });

            migrationBuilder.CreateTable(
                name: "StaticPublishQueue",
                columns: table => new
                {
                    StaticPublishQueueItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaticPublishQueue", x => x.StaticPublishQueueItemId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaticPublishLog_Path",
                table: "StaticPublishLog",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_StaticPublishLog_PublishedUtc",
                table: "StaticPublishLog",
                column: "PublishedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StaticPublishQueue_Path",
                table: "StaticPublishQueue",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaticPublishQueue_RequestedUtc",
                table: "StaticPublishQueue",
                column: "RequestedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaticPublishLog");

            migrationBuilder.DropTable(
                name: "StaticPublishQueue");
        }
    }
}
