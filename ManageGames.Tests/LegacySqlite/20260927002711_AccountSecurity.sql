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

