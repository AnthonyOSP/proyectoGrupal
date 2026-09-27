using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class AddUbicacionGeografica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitud",
                table: "Incidencias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitud",
                table: "Incidencias",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitud",
                table: "Incidencias");

            migrationBuilder.DropColumn(
                name: "Longitud",
                table: "Incidencias");
        }
    }
}
