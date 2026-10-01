using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageGames.Migrations
{
    /// <summary>
    /// Starting data: the well-known game consoles, home consoles and handhelds, each linked to its maker. Runs
    /// once per database, like every migration, so admins can edit or delete the entries afterwards without
    /// them coming back. Entries that already exist under the same name are reused instead of duplicated.
    /// </summary>
    public partial class SeedConsolesAndCompanies : Migration
    {
        // In release order (first release anywhere).
        private static readonly (string Console, string Company)[] Consoles =
        [
            ("Magnavox Odyssey", "Magnavox"),
            ("Fairchild Channel F", "Fairchild"),
            ("Atari 2600", "Atari"),
            ("Magnavox Odyssey 2", "Magnavox"),
            ("Intellivision", "Mattel"),
            ("Game & Watch", "Nintendo"),
            ("Atari 5200", "Atari"),
            ("ColecoVision", "Coleco"),
            ("Vectrex", "General Consumer Electronics"),
            ("Nintendo Entertainment System", "Nintendo"),
            ("Sega SG-1000", "Sega"),
            ("Sega Master System", "Sega"),
            ("Atari 7800", "Atari"),
            ("PC Engine", "NEC"),
            ("Sega Mega Drive", "Sega"),
            ("Game Boy", "Nintendo"),
            ("Atari Lynx", "Atari"),
            ("Super Nintendo Entertainment System", "Nintendo"),
            ("Neo Geo AES", "SNK"),
            ("Sega Game Gear", "Sega"),
            ("Sega Mega-CD", "Sega"),
            ("Philips CD-i", "Philips"),
            ("3DO Interactive Multiplayer", "Panasonic"),
            ("Atari Jaguar", "Atari"),
            ("Amiga CD32", "Commodore"),
            ("Sega 32X", "Sega"),
            ("Sega Saturn", "Sega"),
            ("PlayStation", "Sony"),
            ("Neo Geo CD", "SNK"),
            ("PC-FX", "NEC"),
            ("Virtual Boy", "Nintendo"),
            ("Nintendo 64", "Nintendo"),
            ("Game.com", "Tiger Electronics"),
            ("Game Boy Color", "Nintendo"),
            ("Neo Geo Pocket", "SNK"),
            ("Sega Dreamcast", "Sega"),
            ("WonderSwan", "Bandai"),
            ("Neo Geo Pocket Color", "SNK"),
            ("PlayStation 2", "Sony"),
            ("WonderSwan Color", "Bandai"),
            ("Game Boy Advance", "Nintendo"),
            ("Nintendo GameCube", "Nintendo"),
            ("Xbox", "Microsoft"),
            ("N-Gage", "Nokia"),
            ("Nintendo DS", "Nintendo"),
            ("PlayStation Portable", "Sony"),
            ("Xbox 360", "Microsoft"),
            ("Wii", "Nintendo"),
            ("PlayStation 3", "Sony"),
            ("Nintendo 3DS", "Nintendo"),
            ("PlayStation Vita", "Sony"),
            ("Wii U", "Nintendo"),
            ("Ouya", "Ouya"),
            ("PlayStation 4", "Sony"),
            ("Xbox One", "Microsoft"),
            ("Nintendo Switch", "Nintendo"),
            ("Evercade", "Blaze Entertainment"),
            ("PlayStation 5", "Sony"),
            ("Xbox Series X|S", "Microsoft"),
            ("Analogue Pocket", "Analogue"),
            ("Playdate", "Panic"),
            ("Steam Deck", "Valve"),
            ("Nintendo Switch 2", "Nintendo"),
        ];

        // The makers, in the order their first console appears above.
        private static readonly string[] Companies = Consoles.Select(entry => entry.Company).Distinct().ToArray();

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "Companies" ("Name")
                SELECT v.name
                FROM (VALUES {string.Join(", ", Companies.Select((name, i) => $"({i}, {Literal(name)})"))}) AS v(position, name)
                WHERE NOT EXISTS (SELECT 1 FROM "Companies" c WHERE c."Name" = v.name)
                ORDER BY v.position;
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "Consoles" ("Name", "CompanyId")
                SELECT v.name, (SELECT min(c."Id") FROM "Companies" c WHERE c."Name" = v.company)
                FROM (VALUES {string.Join(", ", Consoles.Select((entry, i) => $"({i}, {Literal(entry.Console)}, {Literal(entry.Company)})"))})
                    AS v(position, name, company)
                WHERE NOT EXISTS (SELECT 1 FROM "Consoles" k WHERE k."Name" = v.name)
                ORDER BY v.position;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Games on these consoles are kept, without a console (ON DELETE SET NULL). A maker stays as long
            // as other consoles still refer to it.
            migrationBuilder.Sql($"""
                DELETE FROM "Consoles"
                WHERE "Name" IN ({string.Join(", ", Consoles.Select(entry => Literal(entry.Console)))})
                  AND "CompanyId" IN (SELECT "Id" FROM "Companies" WHERE "Name" IN ({string.Join(", ", Companies.Select(Literal))}));
                """);

            migrationBuilder.Sql($"""
                DELETE FROM "Companies" c
                WHERE c."Name" IN ({string.Join(", ", Companies.Select(Literal))})
                  AND NOT EXISTS (SELECT 1 FROM "Consoles" k WHERE k."CompanyId" = c."Id");
                """);
        }

        // The values are the constants above; quotes are doubled anyway, as SQL string literals require.
        private static string Literal(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
