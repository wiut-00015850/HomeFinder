using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeFinder.Migrations
{
    [Migration("20260227100000_AddApartmentViewLog")]
    public partial class AddApartmentViewLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApartmentViewLog",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    apartment_id = table.Column<int>(type: "int", nullable: false),
                    viewed_at = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentViewLog", x => x.id);
                    table.ForeignKey(
                        name: "FK_ApartmentViewLog_Apartment_apartment_id",
                        column: x => x.apartment_id,
                        principalTable: "Apartment",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentViewLog_apartment_id",
                schema: "dbo",
                table: "ApartmentViewLog",
                column: "apartment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentViewLog",
                schema: "dbo");
        }
    }
}
