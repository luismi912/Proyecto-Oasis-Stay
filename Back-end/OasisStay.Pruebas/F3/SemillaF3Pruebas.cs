using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Semilla;

namespace OasisStay.Pruebas.F3;

/// <summary>Semilla demo por escenario de F3: --semilla-demo --escenario US05|US06|US10 [D-39][D-41].</summary>
[Collection("SqlServer")]
public class SemillaF3Pruebas : IClassFixture<BancoF3>
{
    private readonly BancoF3 _banco;
    public SemillaF3Pruebas(BancoF3 banco) => _banco = banco;

    private static EjecutorSemillaDemo Ejecutor()
        => new(new ISemillaDemo[] { new SemillaDemoF1(), new SemillaDemoF2(), new SemillaDemoF3() });

    private async Task Escenario(string escenario)
    {
        await using var db = BancoF3.Contexto();
        await Ejecutor().EjecutarAsync(db, escenario);
    }

    private int IdReservaDe(int habitacion)
    {
        using var db = BancoF3.Contexto();
        return db.Reservas.Single(r => r.NumeroHabitacion == habitacion).IdReserva;
    }

    [Fact] // [JIRA US-05 ESC1-ESC3][D-39]
    public async Task Semilla_PorEscenario_US05_3Noches150000_Y_1Noche200000ConMinibarYLate()
    {
        await Escenario("US05");
        await Escenario("US05"); // reinicia sin duplicar
        var p = _banco.Nueva();
        var esc1 = p.Facturacion.CalcularTotales(IdReservaDe(213));
        Assert.Equal((3, 150000m, 450000m, 85500m, 535500m), (esc1.Noches, esc1.Tarifa, esc1.Subtotal, esc1.Iva, esc1.Total));
        var esc2 = p.Facturacion.CalcularTotales(IdReservaDe(214));
        Assert.Equal((200000m, 270000m, 51300m, 321300m), (esc2.Tarifa, esc2.Subtotal, esc2.Iva, esc2.Total));
        // ESC3: la cotización de la misma tarifa pasa de 3 a 5 noches
        using var db = BancoF3.Contexto();
        var tipo = db.TiposHabitacion.Single(t => t.Nombre == "Estándar demo US-05").IdTipo;
        var cinco = p.Facturacion.Cotizar(new SolicitudCotizacion(null, tipo, BancoF3.Hoy, BancoF3.Hoy.AddDays(5), null, false));
        Assert.Equal(892500m, cinco.Total);
        Assert.Equal(2, db.Reservas.Count());
    }

    [Fact] // [JIRA US-06 ESC1-ESC3][D-39]
    public async Task Semilla_PorEscenario_US06_Rs1025Pagada350000_Rs1026Saldo80000_Checkout208AmaLaVe()
    {
        await Escenario("US06");
        var p = _banco.Nueva();
        var rs1025 = p.Facturacion.CalcularTotales(IdReservaDe(208));
        Assert.Equal(("Ana Gómez", 350000m, 350000m, 0m), (rs1025.Huesped, rs1025.Total, rs1025.Pagado, rs1025.Saldo));
        Assert.Equal(80000m, p.Facturacion.CalcularTotales(IdReservaDe(209)).Saldo);
        using (var db = BancoF3.Contexto())
            Assert.Equal(EstadoHabitacion.OCUPADO, db.Habitaciones.Single(h => h.NumeroHabitacion == 208).Estado);

        p.Facade.ProcesarSalida(IdReservaDe(208), "", new DatosPago());
        var tablero = _banco.Nueva().Estados.ObtenerHabitaciones(EstadoHabitacion.EN_LIMPIEZA);
        Assert.Contains(tablero, h => h.NumeroHabitacion == 208);

        await Escenario("US06"); // reinicia: factura y pagos borrados, la 208 vuelve a Ocupada
        using var v = BancoF3.Contexto();
        Assert.Empty(v.Facturas);
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == 208).Estado);
        Assert.Equal(0m, _banco.Nueva().Facturacion.CalcularTotales(IdReservaDe(208)).Saldo);
    }

    [Fact] // [JIRA US-10 ESC1-ESC3][D-39]
    public async Task Semilla_PorEscenario_US10_Deudas120000_250000_150000()
    {
        await Escenario("US10");
        var p = _banco.Nueva();
        Assert.Equal(120000m, p.Facturacion.CalcularTotales(IdReservaDe(210)).Saldo);
        Assert.Equal(120000m, p.Facturacion.CalcularTotales(IdReservaDe(215)).Saldo);
        Assert.Equal(250000m, p.Facturacion.CalcularTotales(IdReservaDe(211)).Saldo);
        Assert.Equal(150000m, p.Facturacion.CalcularTotales(IdReservaDe(212)).Saldo);

        var efectivo = p.Pagos.RegistrarPago(IdReservaDe(210), "EFECTIVO", 120000m, new DatosPago { MontoRecibido = 150000m });
        Assert.Equal(30000m, efectivo.Vuelto);
        var id = IdReservaDe(215);
        p.Pagos.RegistrarPago(id, "TARJETA_CREDITO", 50000m, new DatosPago { NumeroAutorizacion = "4892" });
        Assert.Equal(0m, p.Pagos.RegistrarPago(id, "TRANSFERENCIA", 70000m, new DatosPago { ReferenciaBancaria = "TRF-9921" }).SaldoRestante);
    }

    [Fact] // [D-41]: el reinicio borra facturas, pagos y consumos antes que las reservas (FK Restrict)
    public async Task Semilla_PorEscenario_US01aUS04_ReiniciaSobreBaseConDatosDeF3()
    {
        await Escenario("US06");
        _banco.Nueva().Facade.ProcesarSalida(IdReservaDe(208), "", new DatosPago()); // deja factura, pagos, consumos e historial
        using (var db = BancoF3.Contexto())
        {
            Assert.NotEmpty(db.Facturas);
            Assert.NotEmpty(db.Pagos);
            Assert.NotEmpty(db.ConsumosReserva);
        }

        foreach (var us in new[] { "US01", "US02", "US03", "US04" })
        {
            await Escenario(us);
            using var db = BancoF3.Contexto();
            Assert.Empty(db.Facturas);
            Assert.Empty(db.Pagos);
            Assert.Empty(db.ConsumosReserva);
            Assert.Empty(db.DetallesFactura);
        }
        using var v = BancoF3.Contexto();
        Assert.Equal(105, v.Reservas.Single().NumeroHabitacion); // quedó el escenario US04
    }

    [Fact] // [D-39]: sin escenario, --semilla-demo carga todo y es idempotente
    public async Task Semilla_SinEscenario_CargaTodoSinDuplicar()
    {
        await using var db = BancoF3.Contexto();
        await Ejecutor().EjecutarAsync(db, "US06");
        await Ejecutor().EjecutarAsync(db, null);
        await Ejecutor().EjecutarAsync(db, null);
        Assert.Equal(1, db.Reservas.Count(r => r.NumeroHabitacion == 208));
        Assert.Equal(1, db.Reservas.Count(r => r.NumeroHabitacion == 215));
    }
}
