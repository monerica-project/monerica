using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoIdeaUniqueTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One row per topic among the selectable video ideas, ignoring case and outer spaces.
            // Dedicated videos are named after their sponsor, so they are exempt.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_VideoIdeas_Topic_Unique"
                ON "VideoIdeas" (lower(btrim("Title")))
                WHERE "IsDedicated" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_VideoIdeas_Topic_Unique";""");
        }
    }
}
