using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LlmTornado.Internal.Press.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedPostTextToLinkedIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SummaryJson",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedPostText",
                table: "ArticlePublishStatus",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryJson",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "GeneratedPostText",
                table: "ArticlePublishStatus");
        }
    }
}
