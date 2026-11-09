using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionAiReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalSnapshotJson",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedChangesJson",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedCountryCode",
                table: "Submissions",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedDescription",
                table: "Submissions",
                type: "character varying(175)",
                maxLength: 175,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedKycPolicy",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedLiquidity",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedLocation",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedName",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedNote",
                table: "Submissions",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedOtherSubcategories",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedRateType",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedSubCategoryId",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedTags",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewAttempts",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewBannedCategory",
                table: "Submissions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewConfidence",
                table: "Submissions",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDossier",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDuplicateOf",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewError",
                table: "Submissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewIssuesJson",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewLog",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewModel",
                table: "Submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewMoneroEvidence",
                table: "Submissions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewPrefilterReason",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewState",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewSummary",
                table: "Submissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewTokensIn",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewTokensOut",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewVerdict",
                table: "Submissions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewVerdictJson",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedUtc",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalSnapshotJson",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedChangesJson",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedCountryCode",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedDescription",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedKycPolicy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedLiquidity",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedLocation",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedName",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedNote",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedOtherSubcategories",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedRateType",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedSubCategoryId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedTags",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewAttempts",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewBannedCategory",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewConfidence",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewDossier",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewDuplicateOf",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewError",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewIssuesJson",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewLog",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewModel",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewMoneroEvidence",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewPrefilterReason",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewState",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewSummary",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewTokensIn",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewTokensOut",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewVerdict",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewVerdictJson",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ReviewedUtc",
                table: "Submissions");
        }
    }
}
