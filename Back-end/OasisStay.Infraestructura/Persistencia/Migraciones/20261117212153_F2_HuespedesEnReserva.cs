using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OasisStay.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F2_HuespedesEnReserva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Huespedes",
                table: "Reservas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Huespedes",
                table: "Reservas");
        }
    }
}
