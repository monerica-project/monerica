using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorUserId",
                table: "DirectoryEntryReviews",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "DirectoryEntryReviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUserId",
                table: "DirectoryEntryReviewComments",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "DirectoryEntryReviewComments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisabledUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AuthorUser_Entry",
                table: "DirectoryEntryReviews",
                columns: new[] { "AuthorUserId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_AuthorUser",
                table: "DirectoryEntryReviewComments",
                column: "AuthorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntryReviewComments_AspNetUsers_AuthorUserId",
                table: "DirectoryEntryReviewComments",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntryReviews_AspNetUsers_AuthorUserId",
                table: "DirectoryEntryReviews",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Admin role. Until now there was no public sign-up and every signed-in account was an
            // admin, so EVERY account that exists when this migration runs gets the role. Accounts
            // created later (member sign-ups) do not. Both statements are idempotent.
            migrationBuilder.Sql(
                "INSERT INTO \"AspNetRoles\" (\"Id\", \"Name\", \"NormalizedName\", \"ConcurrencyStamp\") " +
                "VALUES ('" + DirectoryManager.Data.Constants.AuthRoles.AdminRoleId + "', '" + DirectoryManager.Data.Constants.AuthRoles.Admin + "', '" +
                DirectoryManager.Data.Constants.AuthRoles.AdminNormalized + "', '" + DirectoryManager.Data.Constants.AuthRoles.AdminRoleId + "') " +
                "ON CONFLICT DO NOTHING;");

            // AspNetUserRoles has a TPH Discriminator column; Identity's own inserts use this value.
            migrationBuilder.Sql(
                "INSERT INTO \"AspNetUserRoles\" (\"UserId\", \"RoleId\", \"Discriminator\") " +
                "SELECT u.\"Id\", r.\"Id\", 'IdentityUserRole<string>' " +
                "FROM \"AspNetUsers\" u CROSS JOIN \"AspNetRoles\" r " +
                "WHERE r.\"NormalizedName\" = '" + DirectoryManager.Data.Constants.AuthRoles.AdminNormalized + "' " +
                "ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Admin role and its assignments are left in place (the older code ignores roles).
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntryReviewComments_AspNetUsers_AuthorUserId",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntryReviews_AspNetUsers_AuthorUserId",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_AuthorUser_Entry",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropIndex(
                name: "IX_ReviewComments_AuthorUser",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "DeletedUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisabledUtc",
                table: "AspNetUsers");
        }
    }
}
