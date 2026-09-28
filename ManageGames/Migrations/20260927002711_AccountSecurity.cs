using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <inheritdoc />
    public partial class AccountSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The bespoke session cookie was replaced by ASP.NET Core cookie authentication, and
            // profile pictures were never implemented. Neither column is a key or indexed, so SQLite
            // can drop them natively; EF's DropColumn would rebuild tblUser instead, after the
            // backfills below.
            migrationBuilder.Sql("ALTER TABLE tblUser DROP COLUMN CookieID;");
            migrationBuilder.Sql("ALTER TABLE tblUser DROP COLUMN ProfilePicturesID;");

            // Same data, clearer name: the column holds a salted hash, not a password.
            migrationBuilder.RenameColumn(
                name: "Password",
                table: "tblUser",
                newName: "PasswordHash");

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "tblUser",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Every existing user gets their own random security stamp.
            migrationBuilder.Sql("UPDATE tblUser SET SecurityStamp = hex(randomblob(16));");

            // Earlier versions seeded the admin 'user' with a password that was published in the docs.
            // Make it choose its own password at the next login.
            migrationBuilder.Sql("UPDATE tblUser SET MustChangePassword = 1 WHERE NormalizedUsername = 'user';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "tblUser");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "tblUser");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "tblUser",
                newName: "Password");

            migrationBuilder.AddColumn<Guid>(
                name: "ProfilePicturesID",
                table: "tblUser",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "CookieID",
                table: "tblUser",
                type: "TEXT",
                nullable: true);
        }
    }
}
