using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class AddSentimientoMensajeContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "ProbabilidadPositiva",
                table: "MensajesContacto",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sentimiento",
                table: "MensajesContacto",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProbabilidadPositiva",
                table: "MensajesContacto");

            migrationBuilder.DropColumn(
                name: "Sentimiento",
                table: "MensajesContacto");
        }
    }
}
