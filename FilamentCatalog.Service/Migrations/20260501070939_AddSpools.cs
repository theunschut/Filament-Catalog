using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilamentCatalog.Migrations
{
    /// <inheritdoc />
    public partial class AddSpools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Spools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightGrams = table.Column<int>(type: "INTEGER", nullable: true),
                    PricePaid = table.Column<decimal>(type: "TEXT", nullable: true),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SpoolStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spools_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spools_OwnerId",
                table: "Spools",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Spools");
        }
    }
}
