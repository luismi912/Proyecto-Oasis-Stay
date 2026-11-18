using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OasisStay.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F4a_IndiceHistorialFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HistorialReservas_FechaRegistro",
                table: "HistorialReservas",
                column: "FechaRegistro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistorialReservas_FechaRegistro",
                table: "HistorialReservas");
        }
    }
}
