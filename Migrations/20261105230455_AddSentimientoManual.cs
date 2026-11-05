using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class AddSentimientoManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SentimientoManual",
                table: "MensajesContacto",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentimientoManual",
                table: "MensajesContacto");
        }
    }
}
