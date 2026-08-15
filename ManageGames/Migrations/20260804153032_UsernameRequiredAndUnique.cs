using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <inheritdoc />
    public partial class UsernameRequiredAndUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Backfill the normalized value for any pre-existing rows (mirrors Normalize():
            // lower-cased + spaces removed) so the unique index below does not collide on "".
            migrationBuilder.Sql("UPDATE tblUser SET NormalizedUsername = replace(lower(Username), ' ', '');");

            migrationBuilder.CreateIndex(
                name: "IX_tblUser_NormalizedUsername",
                table: "tblUser",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tblUser_NormalizedUsername",
                table: "tblUser");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "tblUser");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "tblUser",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
