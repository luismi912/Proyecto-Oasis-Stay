using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Datos del escenario US-08 [D-39][D-41], idempotentes y con fechas RELATIVAS a hoy en UTC-5 (IReloj) para reproducirse cualquier día:
/// 221 = ESC1 gratis (llega hoy + 3 días, más de 48 h; noche $220.000), 222 = ESC2 tardía (llega hoy, menos de 24 h; noche $180.000),
/// 223 = ESC3 exoneración (llega hoy; $220.000), 224 = No-Show manual (llegó ayer; $220.000), 225 = EnCurso (cancelar da 409).
/// Los casos literales de Jira (check-in 25/11/2026 15:00; A: 20/11 10:00, B: 24/11 20:00 a 19 h, C: exonerada) los cubren las pruebas
/// unitarias con IReloj fijo (US08_Datos_CasoA/B/C), no la demo.
/// </summary>
public class SemillaDemoF4 : ISemillaDemo
{
    private record Fila(int Habitacion, string Tipo, DateTime Inicio, EstadoReserva Estado, string Huesped, string Documento);

    private const string Tipo220 = "Tarifa demo US-08 $220.000";
    private const string Tipo180 = "Tarifa demo US-08 $180.000";

    private readonly IReloj _reloj;
    public SemillaDemoF4(IReloj reloj) => _reloj = reloj;

    public IReadOnlyCollection<string> Escenarios => new[] { "US08" };

    public Task SembrarEscenarioAsync(OasisStayDbContext db, string escenario) => Sembrar(db);

    public Task SembrarAsync(OasisStayDbContext db) => Sembrar(db);

    private async Task Sembrar(OasisStayDbContext db)
    {
        foreach (var (nombre, precio) in new[] { (Tipo220, 220000m), (Tipo180, 180000m) })
            if (!await db.TiposHabitacion.AnyAsync(t => t.Nombre == nombre))
                db.TiposHabitacion.Add(new TipoHabitacion { Nombre = nombre, CapacidadMax = 2, PrecioBase = precio, Descripcion = "Tarifa de ejemplo de Jira US-08" });
        await db.SaveChangesAsync();

        var hoy = _reloj.Ahora.Date;
        var filas = new[]
        {
            new Fila(221, Tipo220, hoy.AddDays(3), EstadoReserva.Confirmada, "ESC1 Gratis", "1008221"),
            new Fila(222, Tipo180, hoy, EstadoReserva.Confirmada, "ESC2 Tardía 180", "1008222"),
            new Fila(223, Tipo220, hoy, EstadoReserva.Confirmada, "ESC3 Exonerar", "1008223"),
            new Fila(224, Tipo220, hoy.AddDays(-1), EstadoReserva.Confirmada, "No-Show Ayer", "1008224"),
            new Fila(225, Tipo220, hoy.AddDays(-1), EstadoReserva.EnCurso, "En curso", "1008225"),
        };
        foreach (var f in filas)
        {
            if (await db.Reservas.AnyAsync(r => r.NumeroHabitacion == f.Habitacion)) continue;
            var tipo = await db.TiposHabitacion.FirstAsync(t => t.Nombre == f.Tipo);
            var estadoHab = f.Estado == EstadoReserva.EnCurso ? EstadoHabitacion.OCUPADO : EstadoHabitacion.RESERVADA;
            var hab = await db.Habitaciones.FindAsync(f.Habitacion);
            if (hab == null) db.Habitaciones.Add(new Habitacion { NumeroHabitacion = f.Habitacion, Piso = f.Habitacion / 100, IdTipo = tipo.IdTipo, Estado = estadoHab });
            else { hab.IdTipo = tipo.IdTipo; hab.Estado = estadoHab; }

            db.Reservas.Add(new Reserva
            {
                Cliente = new Cliente
                {
                    TipoDocumento = TipoDocumento.CC, Documento = f.Documento, Nombre = f.Huesped,
                    Telefono = "3100000000", Correo = $"{f.Documento}@demo.oasisstay.co",
                },
                NumeroHabitacion = f.Habitacion, Huespedes = 1, Estado = f.Estado,
                FechaInicio = f.Inicio.Date, FechaFin = f.Inicio.Date.AddDays(2),
            });
            await db.SaveChangesAsync();
        }
    }
}
