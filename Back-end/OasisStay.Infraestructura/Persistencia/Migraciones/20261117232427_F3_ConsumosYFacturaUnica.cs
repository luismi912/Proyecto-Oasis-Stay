using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OasisStay.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F3_ConsumosYFacturaUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facturas_IdReserva",
                table: "Facturas");

            migrationBuilder.CreateTable(
                name: "ConsumosReserva",
                columns: table => new
                {
                    IdConsumo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdReserva = table.Column<int>(type: "int", nullable: false),
                    IdServicio = table.Column<int>(type: "int", nullable: true),
                    Concepto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumosReserva", x => x.IdConsumo);
                    table.ForeignKey(
                        name: "FK_ConsumosReserva_Reservas_IdReserva",
                        column: x => x.IdReserva,
                        principalTable: "Reservas",
                        principalColumn: "IdReserva",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumosReserva_Servicios_IdServicio",
                        column: x => x.IdServicio,
                        principalTable: "Servicios",
                        principalColumn: "IdServicio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_IdReserva",
                table: "Facturas",
                column: "IdReserva",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosReserva_IdReserva",
                table: "ConsumosReserva",
                column: "IdReserva");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosReserva_IdServicio",
                table: "ConsumosReserva",
                column: "IdServicio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumosReserva");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_IdReserva",
                table: "Facturas");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_IdReserva",
                table: "Facturas",
                column: "IdReserva");
        }
    }
}
