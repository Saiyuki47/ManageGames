using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <inheritdoc />
    public partial class NullableConsoleAndCompanyFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblConsoles_tblCompanies_Company",
                table: "tblConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_tblGames_tblConsoles_Console",
                table: "tblGames");

            migrationBuilder.AlterColumn<int>(
                name: "Console",
                table: "tblGames",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Company",
                table: "tblConsoles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_tblConsoles_tblCompanies_Company",
                table: "tblConsoles",
                column: "Company",
                principalTable: "tblCompanies",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tblGames_tblConsoles_Console",
                table: "tblGames",
                column: "Console",
                principalTable: "tblConsoles",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblConsoles_tblCompanies_Company",
                table: "tblConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_tblGames_tblConsoles_Console",
                table: "tblGames");

            migrationBuilder.AlterColumn<int>(
                name: "Console",
                table: "tblGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Company",
                table: "tblConsoles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tblConsoles_tblCompanies_Company",
                table: "tblConsoles",
                column: "Company",
                principalTable: "tblCompanies",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tblGames_tblConsoles_Console",
                table: "tblGames",
                column: "Console",
                principalTable: "tblConsoles",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
