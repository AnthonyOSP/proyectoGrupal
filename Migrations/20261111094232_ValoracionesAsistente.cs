using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class ValoracionesAsistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValoracionesAsistente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RespuestaId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Pregunta = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Respuesta = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Fuentes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Util = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoracionesAsistente", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValoracionesAsistente_RespuestaId",
                table: "ValoracionesAsistente",
                column: "RespuestaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValoracionesAsistente");
        }
    }
}
