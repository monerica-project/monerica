using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SponsoredListingOfferPerPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "UX_Offer_Type_Days_Cat_NoSubcat",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "UX_Offer_Type_Days_Cat_Subcat",
                table: "SponsoredListingOffers");

            // Retire "category price stored per subcategory": a category sponsor price used to be
            // copied onto every subcategory of the category. Turn each set of copies into ONE price
            // list for the category page itself, then remove the copies. Paid history lives on the
            // invoices (amount, type, category, subcategory) and is not touched.
            migrationBuilder.Sql(@"
INSERT INTO ""SponsoredListingOffers""
    (""IsEnabled"", ""Description"", ""Days"", ""PriceCurrency"", ""Price"", ""CategoryId"", ""SubcategoryId"",
     ""SponsorshipType"", ""CreateDate"", ""UpdateDate"", ""CreatedByUserId"", ""UpdatedByUserId"")
SELECT bool_or(o.""IsEnabled""), max(o.""Description""), o.""Days"", min(o.""PriceCurrency""), max(o.""Price""),
       s.""CategoryId"", NULL, 3, now(), NULL, max(o.""CreatedByUserId""), NULL
FROM ""SponsoredListingOffers"" o
JOIN ""Subcategories"" s ON s.""SubCategoryId"" = o.""SubcategoryId""
WHERE o.""SponsorshipType"" = 3 AND o.""SubcategoryId"" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM ""SponsoredListingOffers"" x
      WHERE x.""SponsorshipType"" = 3 AND x.""CategoryId"" = s.""CategoryId""
        AND x.""SubcategoryId"" IS NULL AND x.""Days"" = o.""Days"")
GROUP BY s.""CategoryId"", o.""Days"";

DELETE FROM ""SponsoredListingOffers"" WHERE ""SponsorshipType"" = 3 AND ""SubcategoryId"" IS NOT NULL;
DELETE FROM ""SponsoredListingOffers""
WHERE ""SponsorshipType"" NOT IN (1, 2, 3)
   OR (""SponsorshipType"" = 1 AND (""CategoryId"" IS NOT NULL OR ""SubcategoryId"" IS NOT NULL));
UPDATE ""SponsoredListingOffers"" SET ""CategoryId"" = NULL
WHERE ""SponsorshipType"" = 2 AND ""CategoryId"" IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Category",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId" },
                unique: true,
                filter: "\"CategoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Standard",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "\"CategoryId\" IS NULL AND \"SubcategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Subcategory",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "SubcategoryId" },
                unique: true,
                filter: "\"SubcategoryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Offer_Placement",
                table: "SponsoredListingOffers",
                sql: "(\"SponsorshipType\" = 1 AND \"CategoryId\" IS NULL AND \"SubcategoryId\" IS NULL) OR (\"SponsorshipType\" = 3 AND \"SubcategoryId\" IS NULL) OR (\"SponsorshipType\" = 2 AND \"CategoryId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the per-subcategory copies of category prices are not recreated.
            migrationBuilder.DropIndex(
                name: "UX_Offer_Category",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "UX_Offer_Standard",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "UX_Offer_Subcategory",
                table: "SponsoredListingOffers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Offer_Placement",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "\"SubcategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_NoSubcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId" },
                unique: true,
                filter: "\"SubcategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_Subcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId", "SubcategoryId" },
                unique: true,
                filter: "\"SubcategoryId\" IS NOT NULL");
        }
    }
}
