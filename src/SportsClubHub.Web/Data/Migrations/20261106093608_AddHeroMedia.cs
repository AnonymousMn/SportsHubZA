using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsClubHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS also accommodates databases from the earlier additive startup
            // helper, while still recording this schema change as an EF migration.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "SiteMedia" (
                    "SiteMediaId" INTEGER NOT NULL CONSTRAINT "PK_SiteMedia" PRIMARY KEY AUTOINCREMENT,
                    "ContentType" TEXT NOT NULL,
                    "Data" BLOB NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                """);
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "SiteSettings" (
                    "SiteSettingsId" INTEGER NOT NULL CONSTRAINT "PK_SiteSettings" PRIMARY KEY,
                    "HeroMediaKind" TEXT NOT NULL,
                    "HeroImageUrl" TEXT NULL,
                    "HeroVideoUrl" TEXT NULL,
                    "HeroOverlayOpacity" INTEGER NOT NULL,
                    "HeroFocus" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteMedia");

            migrationBuilder.DropTable(
                name: "SiteSettings");
        }
    }
}
