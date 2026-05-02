using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilamentCatalog.Migrations
{
    /// <inheritdoc />
    public partial class AddBambuProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BambuProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    ColorName = table.Column<string>(type: "TEXT", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", nullable: false),
                    ColorSwatchUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BambuProducts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BambuProducts_LastSyncedAt",
                table: "BambuProducts",
                column: "LastSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BambuProducts_Material",
                table: "BambuProducts",
                column: "Material");

            migrationBuilder.CreateIndex(
                name: "IX_BambuProducts_Name_Material",
                table: "BambuProducts",
                columns: new[] { "Name", "Material" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BambuProducts");
        }
    }
}
