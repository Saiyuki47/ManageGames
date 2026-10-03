using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <inheritdoc />
    public partial class CoverThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutomatic",
                table: "GameCovers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "Thumbnail",
                table: "GameCovers",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutomatic",
                table: "GameCovers");

            migrationBuilder.DropColumn(
                name: "Thumbnail",
                table: "GameCovers");
        }
    }
}
