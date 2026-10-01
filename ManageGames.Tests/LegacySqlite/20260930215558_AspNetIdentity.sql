CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "tblCompanies" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_tblCompanies" PRIMARY KEY AUTOINCREMENT,
    "CompanyName" TEXT NOT NULL
);

CREATE TABLE "tblUser" (
    "UserID" TEXT NOT NULL CONSTRAINT "PK_tblUser" PRIMARY KEY,
    "ProfilePicturesID" TEXT NOT NULL,
    "Username" TEXT NULL,
    "Password" TEXT NULL,
    "IsAdmin" INTEGER NOT NULL,
    "CookieID" TEXT NULL
);

CREATE TABLE "tblConsoles" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_tblConsoles" PRIMARY KEY AUTOINCREMENT,
    "ConsoleName" TEXT NOT NULL,
    "Company" INTEGER NOT NULL,
    CONSTRAINT "FK_tblConsoles_tblCompanies_Company" FOREIGN KEY ("Company") REFERENCES "tblCompanies" ("ID") ON DELETE CASCADE
);

CREATE TABLE "tblGames" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_tblGames" PRIMARY KEY AUTOINCREMENT,
    "Console" INTEGER NOT NULL,
    "GameName" TEXT NOT NULL,
    "Anzahl_von_Game" INTEGER NOT NULL,
    "Wunschliste" INTEGER NOT NULL,
    "UserID" TEXT NOT NULL,
    CONSTRAINT "FK_tblGames_tblConsoles_Console" FOREIGN KEY ("Console") REFERENCES "tblConsoles" ("ID") ON DELETE CASCADE,
    CONSTRAINT "FK_tblGames_tblUser_UserID" FOREIGN KEY ("UserID") REFERENCES "tblUser" ("UserID") ON DELETE CASCADE
);

CREATE INDEX "IX_tblConsoles_Company" ON "tblConsoles" ("Company");

CREATE INDEX "IX_tblGames_Console" ON "tblGames" ("Console");

CREATE INDEX "IX_tblGames_UserID" ON "tblGames" ("UserID");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260804151406_InitialCreate', '10.0.12');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "tblUser" ADD "NormalizedUsername" TEXT NOT NULL DEFAULT '';

UPDATE tblUser SET NormalizedUsername = replace(lower(Username), ' ', '');

CREATE UNIQUE INDEX "IX_tblUser_NormalizedUsername" ON "tblUser" ("NormalizedUsername");

CREATE TABLE "ef_temp_tblUser" (
    "UserID" TEXT NOT NULL CONSTRAINT "PK_tblUser" PRIMARY KEY,
    "CookieID" TEXT NULL,
    "IsAdmin" INTEGER NOT NULL,
    "NormalizedUsername" TEXT NOT NULL,
    "Password" TEXT NULL,
    "ProfilePicturesID" TEXT NOT NULL,
    "Username" TEXT NOT NULL
);

INSERT INTO "ef_temp_tblUser" ("UserID", "CookieID", "IsAdmin", "NormalizedUsername", "Password", "ProfilePicturesID", "Username")
SELECT "UserID", "CookieID", "IsAdmin", "NormalizedUsername", "Password", "ProfilePicturesID", IFNULL("Username", '')
FROM "tblUser";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "tblUser";

ALTER TABLE "ef_temp_tblUser" RENAME TO "tblUser";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_tblUser_NormalizedUsername" ON "tblUser" ("NormalizedUsername");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260804153032_UsernameRequiredAndUnique', '10.0.12');

BEGIN TRANSACTION;
ALTER TABLE "tblGames" RENAME COLUMN "Wunschliste" TO "IsOnWishList";

ALTER TABLE "tblGames" RENAME COLUMN "Anzahl_von_Game" TO "Copies";

ALTER TABLE "tblUser" ADD "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblUser" ADD "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblGames" ADD "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblGames" ADD "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblConsoles" ADD "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblConsoles" ADD "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260804154147_EnglishNamingAndTimestamps', '10.0.12');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "ef_temp_tblUser" (
    "UserID" TEXT NOT NULL CONSTRAINT "PK_tblUser" PRIMARY KEY,
    "CookieID" TEXT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "IsAdmin" INTEGER NOT NULL,
    "NormalizedUsername" TEXT NOT NULL,
    "Password" TEXT NOT NULL,
    "ProfilePicturesID" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "Username" TEXT NOT NULL
);

INSERT INTO "ef_temp_tblUser" ("UserID", "CookieID", "CreatedAt", "IsAdmin", "NormalizedUsername", "Password", "ProfilePicturesID", "UpdatedAt", "Username")
SELECT "UserID", "CookieID", "CreatedAt", "IsAdmin", "NormalizedUsername", IFNULL("Password", ''), "ProfilePicturesID", "UpdatedAt", "Username"
FROM "tblUser";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "tblUser";

ALTER TABLE "ef_temp_tblUser" RENAME TO "tblUser";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_tblUser_NormalizedUsername" ON "tblUser" ("NormalizedUsername");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260804160242_PasswordRequired', '10.0.12');

BEGIN TRANSACTION;
CREATE TABLE "ef_temp_tblConsoles" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_tblConsoles" PRIMARY KEY AUTOINCREMENT,
    "Company" INTEGER NULL,
    "ConsoleName" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "FK_tblConsoles_tblCompanies_Company" FOREIGN KEY ("Company") REFERENCES "tblCompanies" ("ID") ON DELETE SET NULL
);

INSERT INTO "ef_temp_tblConsoles" ("ID", "Company", "ConsoleName", "CreatedAt", "UpdatedAt")
SELECT "ID", "Company", "ConsoleName", "CreatedAt", "UpdatedAt"
FROM "tblConsoles";

CREATE TABLE "ef_temp_tblGames" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_tblGames" PRIMARY KEY AUTOINCREMENT,
    "Console" INTEGER NULL,
    "Copies" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "GameName" TEXT NOT NULL,
    "IsOnWishList" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UserID" TEXT NOT NULL,
    CONSTRAINT "FK_tblGames_tblConsoles_Console" FOREIGN KEY ("Console") REFERENCES "tblConsoles" ("ID") ON DELETE SET NULL,
    CONSTRAINT "FK_tblGames_tblUser_UserID" FOREIGN KEY ("UserID") REFERENCES "tblUser" ("UserID") ON DELETE CASCADE
);

INSERT INTO "ef_temp_tblGames" ("ID", "Console", "Copies", "CreatedAt", "GameName", "IsOnWishList", "UpdatedAt", "UserID")
SELECT "ID", "Console", "Copies", "CreatedAt", "GameName", "IsOnWishList", "UpdatedAt", "UserID"
FROM "tblGames";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "tblConsoles";

ALTER TABLE "ef_temp_tblConsoles" RENAME TO "tblConsoles";

DROP TABLE "tblGames";

ALTER TABLE "ef_temp_tblGames" RENAME TO "tblGames";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_tblConsoles_Company" ON "tblConsoles" ("Company");

CREATE INDEX "IX_tblGames_Console" ON "tblGames" ("Console");

CREATE INDEX "IX_tblGames_UserID" ON "tblGames" ("UserID");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260804170841_NullableConsoleAndCompanyFks', '10.0.12');

BEGIN TRANSACTION;
ALTER TABLE "tblCompanies" ADD "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

ALTER TABLE "tblCompanies" ADD "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260815222929_CompanyTimestamps', '10.0.12');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE tblUser DROP COLUMN CookieID;

ALTER TABLE tblUser DROP COLUMN ProfilePicturesID;

ALTER TABLE "tblUser" RENAME COLUMN "Password" TO "PasswordHash";

ALTER TABLE "tblUser" ADD "MustChangePassword" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "tblUser" ADD "SecurityStamp" TEXT NOT NULL DEFAULT '';

UPDATE tblUser SET SecurityStamp = hex(randomblob(16));

UPDATE tblUser SET MustChangePassword = 1 WHERE NormalizedUsername = 'user';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260927002711_AccountSecurity', '10.0.12');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);

CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "MustChangePassword" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);

CREATE TABLE "Companies" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Companies" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserSessions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_UserSessions" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    CONSTRAINT "FK_UserSessions_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Consoles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Consoles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "CompanyId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "FK_Consoles_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL
);

CREATE TABLE "Games" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Games" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Copies" INTEGER NOT NULL,
    "IsOnWishList" INTEGER NOT NULL,
    "ConsoleId" INTEGER NULL,
    "UserId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "FK_Games_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Games_Consoles_ConsoleId" FOREIGN KEY ("ConsoleId") REFERENCES "Consoles" ("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");

CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");

CREATE INDEX "IX_Consoles_CompanyId" ON "Consoles" ("CompanyId");

CREATE INDEX "IX_Games_ConsoleId" ON "Games" ("ConsoleId");

CREATE INDEX "IX_Games_UserId" ON "Games" ("UserId");

CREATE INDEX "IX_UserSessions_UserId" ON "UserSessions" ("UserId");

INSERT INTO "Companies" ("Id", "Name", "CreatedAt", "UpdatedAt")
SELECT "ID", "CompanyName", "CreatedAt", "UpdatedAt" FROM "tblCompanies";

INSERT INTO "Consoles" ("Id", "Name", "CompanyId", "CreatedAt", "UpdatedAt")
SELECT "ID", "ConsoleName",
       CASE WHEN "Company" IN (SELECT "ID" FROM "tblCompanies") THEN "Company" END,
       "CreatedAt", "UpdatedAt"
FROM "tblConsoles";

INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "EmailConfirmed", "PasswordHash",
    "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
    "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
SELECT upper("UserID"), "Username", "NormalizedUsername", 0, "PasswordHash",
    "SecurityStamp", lower(hex(randomblob(16))), 0, 0, 1,
    0, "MustChangePassword", "CreatedAt", "UpdatedAt"
FROM "tblUser";

INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
VALUES ('6E9D3F0B-8C2A-4B7E-9F1D-2A5C7E4B8D10', 'Admin', 'admin', lower(hex(randomblob(16))));

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT upper("UserID"), '6E9D3F0B-8C2A-4B7E-9F1D-2A5C7E4B8D10' FROM "tblUser" WHERE "IsAdmin" = 1;

INSERT INTO "Games" ("Id", "Name", "Copies", "IsOnWishList", "ConsoleId", "UserId", "CreatedAt", "UpdatedAt")
SELECT "ID", "GameName", "Copies", "IsOnWishList",
       CASE WHEN "Console" IN (SELECT "ID" FROM "tblConsoles") THEN "Console" END,
       upper("UserID"), "CreatedAt", "UpdatedAt"
FROM "tblGames"
WHERE upper("UserID") IN (SELECT "Id" FROM "AspNetUsers");

DROP TABLE "tblGames";

DROP TABLE "tblConsoles";

DROP TABLE "tblUser";

DROP TABLE "tblCompanies";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260930215558_AspNetIdentity', '10.0.12');

COMMIT;

