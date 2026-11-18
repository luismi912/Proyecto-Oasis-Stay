using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Facturacion;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F3;

/// <summary>Servicios de F3 armados sobre un mismo contexto SQL Server real (transacciones reales) [D-06].</summary>
public sealed class PilaF3
{
    public required OasisStayDbContext Db { get; init; }
    public required ServicioFacturacion Facturacion { get; init; }
    public required GestionPagos Pagos { get; init; }
    public required CheckoutFacade Facade { get; init; }
    public required GestionEstadoHabitacion Estados { get; init; }
}

/// <summary>Base propia OasisStay_PruebasF3 (se borra al terminar), con los tipos y tarifas de los escenarios de Jira [D-29][D-39].</summary>
public sealed class BancoF3 : IAsyncLifetime
{
    public const string Cadena = "Server=localhost;Database=OasisStay_PruebasF3;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    private static int _habitacion = 400;
    private static int _documento = 9000000;

    public RelojFijo Reloj { get; } = new() { Ahora = new DateTime(2026, 12, 10, 10, 0, 0) };
    public static readonly DateTime Hoy = new(2026, 12, 10);

    public static OasisStayDbContext Contexto() =>
        new(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(Cadena).Options, new CifradorAes256(Banco.Clave));

    public async Task InitializeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        db.TiposHabitacion.AddRange(
            new TipoHabitacion { Nombre = "Estándar", CapacidadMax = 2, PrecioBase = 150000m },
            new TipoHabitacion { Nombre = "Superior", CapacidadMax = 2, PrecioBase = 200000m },
            new TipoHabitacion { Nombre = "Individual", CapacidadMax = 1, PrecioBase = 120000m },
            new TipoHabitacion { Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m },
            new TipoHabitacion { Nombre = "Suite Ejecutiva", CapacidadMax = 4, PrecioBase = 320000m });
        db.Servicios.Add(new Servicio { Nombre = "Minibar", Precio = 0m, Descripcion = "Consumo de minibar" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }

    public int IdTipo(string nombre)
    {
        using var db = Contexto();
        return db.TiposHabitacion.Single(t => t.Nombre == nombre).IdTipo;
    }

    public PilaF3 Nueva(IRepositorioHistorial? historial = null)
    {
        var db = Contexto();
        var reservas = new RepositorioReservas(db);
        var habitaciones = new RepositorioHabitaciones(db);
        var consumos = new RepositorioConsumos(db);
        var pagos = new RepositorioPagos(db);
        var motor = new MotorCuentasFacturacion();
        var procesador = new ProcesadorPagos(new IEstrategiaPago[] { new PagoEfectivo(), new PagoTarjeta(), new PagoTransferencia() });
        var gestionPagos = new GestionPagos(reservas, consumos, pagos, motor, procesador, Reloj);
        var politicas = new PoliticasOpciones();
        var facturacion = new ServicioFacturacion(reservas, habitaciones, new RepositorioTiposHabitacion(db), consumos, pagos,
            new FacturaRepository(db), motor, gestionPagos, new FacturaPdfGenerator(), Reloj, politicas);
        var facade = new CheckoutFacade(facturacion, new ServicioHabitacion(habitaciones),
            historial ?? new RepositorioHistorial(db), new UnidadDeTrabajoEf(db));
        return new PilaF3
        {
            Db = db, Facturacion = facturacion, Pagos = gestionPagos, Facade = facade,
            Estados = new GestionEstadoHabitacion(habitaciones, new RepositorioTiposHabitacion(db), new UnidadDeTrabajoEf(db)),
        };
    }

    /// <summary>Reserva EnCurso en habitación Ocupada, con su minibar, late check-out y abonos previos opcionales; devuelve (idReserva, habitación).</summary>
    public (int IdReserva, int Habitacion) CrearReservaEnCurso(string tipo, int noches, string huesped = "Ana Gómez",
        decimal minibar = 0m, bool late = false, decimal pagado = 0m, int? numeroHabitacion = null)
    {
        using var db = Contexto();
        var n = numeroHabitacion ?? Interlocked.Increment(ref _habitacion);
        var idTipo = db.TiposHabitacion.Single(t => t.Nombre == tipo).IdTipo;
        db.Habitaciones.Add(new Habitacion { NumeroHabitacion = n, Piso = 2, IdTipo = idTipo, Estado = EstadoHabitacion.OCUPADO });
        var cliente = new Cliente
        {
            TipoDocumento = TipoDocumento.CC, Documento = Interlocked.Increment(ref _documento).ToString(), Nombre = huesped,
            Telefono = "3101234567", Correo = "huesped@email.com",
        };
        var reserva = new Reserva
        {
            Cliente = cliente, NumeroHabitacion = n, Huespedes = 1, Estado = EstadoReserva.EnCurso,
            FechaInicio = Hoy.AddDays(-noches), FechaFin = Hoy,
        };
        db.Reservas.Add(reserva);
        db.SaveChanges();
        var minibarId = db.Servicios.Single(s => s.Nombre == "Minibar").IdServicio;
        if (minibar > 0)
            db.ConsumosReserva.Add(new ConsumoReserva { IdReserva = reserva.IdReserva, IdServicio = minibarId, Concepto = "Minibar", Cantidad = 1, ValorUnitario = minibar, Fecha = Hoy });
        if (late)
            db.ConsumosReserva.Add(new ConsumoReserva { IdReserva = reserva.IdReserva, Concepto = "Late check-out", Cantidad = 1, ValorUnitario = 40000m, Fecha = Hoy });
        if (pagado > 0)
            db.Pagos.Add(new Pago { IdReserva = reserva.IdReserva, FechaPago = Hoy, Monto = pagado, MetodoPago = MetodoPago.EFECTIVO, Vuelto = 0m });
        db.SaveChanges();
        return (reserva.IdReserva, n);
    }
}
