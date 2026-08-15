using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <inheritdoc />
    public partial class EnglishNamingAndTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Wunschliste",
                table: "tblGames",
                newName: "IsOnWishList");

            migrationBuilder.RenameColumn(
                name: "Anzahl_von_Game",
                table: "tblGames",
                newName: "Copies");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "tblGames",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tblGames",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "tblConsoles",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tblConsoles",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "tblUser");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tblUser");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "tblGames");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tblGames");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "tblConsoles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tblConsoles");

            migrationBuilder.RenameColumn(
                name: "IsOnWishList",
                table: "tblGames",
                newName: "Wunschliste");

            migrationBuilder.RenameColumn(
                name: "Copies",
                table: "tblGames",
                newName: "Anzahl_von_Game");
        }
    }
}
