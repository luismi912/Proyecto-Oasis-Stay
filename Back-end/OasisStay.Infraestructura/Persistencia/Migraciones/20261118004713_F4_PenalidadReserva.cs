using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OasisStay.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F4_PenalidadReserva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoExoneracion",
                table: "Reservas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Penalidad",
                table: "Reservas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "PenalidadExonerada",
                table: "Reservas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoExoneracion",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "Penalidad",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "PenalidadExonerada",
                table: "Reservas");
        }
    }
}
