using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoGrupal.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorialEstadosIncidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialEstadosIncidencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncidenciaId = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaCambio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosIncidencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosIncidencia_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosIncidencia_Incidencias_IncidenciaId",
                        column: x => x.IncidenciaId,
                        principalTable: "Incidencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosIncidencia_IncidenciaId",
                table: "HistorialEstadosIncidencia",
                column: "IncidenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosIncidencia_UsuarioId",
                table: "HistorialEstadosIncidencia",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialEstadosIncidencia");
        }
    }
}
