using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateCommissionEarnedId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AffiliateCommissionId",
                table: "AffiliateCommissionsEarned",
                newName: "AffiliateCommissionEarnedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AffiliateCommissionEarnedId",
                table: "AffiliateCommissionsEarned",
                newName: "AffiliateCommissionId");
        }
    }
}
