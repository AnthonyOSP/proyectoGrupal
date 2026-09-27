using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioToIncidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Incidencias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_UsuarioId",
                table: "Incidencias",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidencias_AspNetUsers_UsuarioId",
                table: "Incidencias",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidencias_AspNetUsers_UsuarioId",
                table: "Incidencias");

            migrationBuilder.DropIndex(
                name: "IX_Incidencias_UsuarioId",
                table: "Incidencias");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Incidencias");
        }
    }
}
