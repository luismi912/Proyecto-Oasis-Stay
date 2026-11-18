using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Repositorios;

namespace OasisStay.Pruebas.F3;

/// <summary>US-05 cotización con Decorator [JIRA US-05][CTR §4][DS4 calcularTotales].</summary>
[Collection("SqlServer")]
public class CotizacionPruebas : IClassFixture<BancoF3>
{
    private readonly BancoF3 _banco;
    public CotizacionPruebas(BancoF3 banco) => _banco = banco;

    private SolicitudCotizacion Solicitud(string tipo, int noches, IReadOnlyList<ConsumoCotizado>? consumos = null, bool late = false)
        => new(null, _banco.IdTipo(tipo), BancoF3.Hoy, BancoF3.Hoy.AddDays(noches), consumos, late);

    [Fact] // [JIRA US-05 ESC1]
    public void US05_ESC1_3Noches150000_Subtotal450000_Iva85500_Total535500()
    {
        var c = _banco.Nueva().Facturacion.Cotizar(Solicitud("Estándar", 3));
        Assert.Equal(3, c.Noches);
        Assert.Equal(150000m, c.Tarifa);
        Assert.Equal(450000m, c.Subtotal);
        Assert.Equal(85500m, c.Iva);
        Assert.Equal(535500m, c.Total);
    }

    [Fact] // [JIRA US-05 ESC2]: 1 noche 200000 + minibar 30000 + late 40000 = 270000, IVA 51300, total 321300
    public void US05_ESC2_ConsumosYLateCheckout_Total321300()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Superior", 1);
        var p = _banco.Nueva();
        p.Facturacion.AgregarConsumo(id, "Minibar", 1, 30000m);
        p.Facturacion.AgregarConsumo(id, "Late check-out", 1, 0m);

        var r = p.Facturacion.CalcularTotales(id);
        Assert.Equal(200000m, r.Tarifa);
        Assert.Equal(270000m, r.Subtotal);
        Assert.Equal(51300m, r.Iva);
        Assert.Equal(321300m, r.Total);
        Assert.True(r.SalidaTardia);
        Assert.Contains(r.Consumos, l => l.Concepto == "Late check-out" && l.SubTotal == 40000m);

        // la misma cuenta cotizada con consumos y salida tardía
        var c = p.Facturacion.Cotizar(Solicitud("Superior", 1, new[] { new ConsumoCotizado("Minibar", 1, 30000m) }, true));
        Assert.Equal((270000m, 51300m, 321300m), (c.Subtotal, c.Iva, c.Total));
    }

    [Fact] // [JIRA US-05 ESC3]: de 3 a 5 noches recalcula y responde rápido (< 200 ms, RNF-02)
    public void US05_ESC3_AmplíaDe3a5Noches_Recalcula()
    {
        var p = _banco.Nueva();
        var tres = p.Facturacion.Cotizar(Solicitud("Estándar", 3));
        Assert.Equal(535500m, tres.Total);
        var sw = Stopwatch.StartNew();
        var cinco = p.Facturacion.Cotizar(Solicitud("Estándar", 5));
        sw.Stop();
        Assert.Equal((750000m, 142500m, 892500m), (cinco.Subtotal, cinco.Iva, cinco.Total));
        Assert.True(sw.ElapsedMilliseconds < 200, $"Tardó {sw.ElapsedMilliseconds} ms");
    }

    [Fact] // [JIRA US-05 caso borde][D-22]
    public void US05_FechasInvalidasOCeroNoches_Rechaza()
    {
        var p = _banco.Nueva();
        var cero = new SolicitudCotizacion(null, _banco.IdTipo("Estándar"), BancoF3.Hoy, BancoF3.Hoy, null, false);
        var negativo = new SolicitudCotizacion(null, _banco.IdTipo("Estándar"), BancoF3.Hoy, BancoF3.Hoy.AddDays(-2), null, false);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada",
            Assert.Throws<ValidacionDominioException>(() => p.Facturacion.Cotizar(cero)).Message);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada",
            Assert.Throws<ValidacionDominioException>(() => p.Facturacion.Cotizar(negativo)).Message);
    }
}

/// <summary>US-10 pagos con Strategy y pagos mixtos [JIRA US-10][D-13][D-33].</summary>
[Collection("SqlServer")]
public class PagosPruebas : IClassFixture<BancoF3>
{
    private readonly BancoF3 _banco;
    public PagosPruebas(BancoF3 banco) => _banco = banco;

    [Fact] // [JIRA US-10 ESC1]: deuda 250000, TARJETA_CREDITO con código de aprobación -> saldo 0 y comprobante
    public void US10_ESC1_Deuda250000_TarjetaCredito_SaldoCero_ConComprobante()
    {
        // Suite 1 noche = 380800 con IVA; abono previo 130800 -> deuda 250000
        var (id, _) = _banco.CrearReservaEnCurso("Suite Ejecutiva", 1, pagado: 130800m);
        var p = _banco.Nueva();
        Assert.Equal(250000m, p.Facturacion.CalcularTotales(id).Saldo);

        var recibo = p.Pagos.RegistrarPago(id, "TARJETA_CREDITO", 250000m, new DatosPago { NumeroAutorizacion = "AP-778811" });

        Assert.True(recibo.IdPago > 0);
        Assert.Equal(250000m, recibo.Monto);
        Assert.Equal("TARJETA_CREDITO", recibo.Metodo);
        Assert.Equal(0m, recibo.SaldoRestante);
        Assert.Equal(0m, p.Facturacion.CalcularTotales(id).Saldo);
        using var v = BancoF3.Contexto();
        var pago = v.Pagos.Single(x => x.IdPago == recibo.IdPago);
        Assert.Equal("AP-778811", pago.NumeroAutorizacion);
        Assert.Equal("Crédito", pago.TipoTarjeta);
    }

    [Fact] // [JIRA US-10 ESC2][D-13]: 120000 = 50000 TC (aprob 4892) + 70000 transferencia (TRF-9921) cubre el 100 %
    public void US10_ESC2_Mixto120000_50000TC_Aprob4892_Mas70000Transf_TRF9921_Cubre100()
    {
        // Individual 1 noche = 142800 con IVA; abono previo 22800 -> deuda 120000
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var p = _banco.Nueva();
        Assert.Equal(120000m, p.Facturacion.CalcularTotales(id).Saldo);

        var uno = p.Pagos.RegistrarPago(id, "TARJETA_CREDITO", 50000m, new DatosPago { NumeroAutorizacion = "4892" });
        Assert.Equal(70000m, uno.SaldoRestante);
        var dos = p.Pagos.RegistrarPago(id, "TRANSFERENCIA", 70000m, new DatosPago { ReferenciaBancaria = "TRF-9921" });
        Assert.Equal(0m, dos.SaldoRestante);

        var r = p.Facturacion.CalcularTotales(id);
        Assert.Equal(0m, r.Saldo);
        Assert.Equal(r.Total, r.Pagado);
        using var v = BancoF3.Contexto();
        Assert.Equal("TRF-9921", v.Pagos.Single(x => x.IdPago == dos.IdPago).ReferenciaBancaria);
        Assert.Equal("4892", v.Pagos.Single(x => x.IdPago == uno.IdPago).NumeroAutorizacion);
    }

    [Fact] // [JIRA US-10 ESC3]: cobro de 150000 con solo 100000 o sin referencia -> alerta y no cierra el pago total
    public void US10_ESC3_Cobro150000_Solo100000_OSinReferencia_NoCierra()
    {
        // Doble Clásica 2 noches = 440300 con IVA; abono previo 290300 -> cobro de 150000
        var (id, _) = _banco.CrearReservaEnCurso("Doble Clásica", 2, pagado: 290300m);
        var p = _banco.Nueva();
        Assert.Equal(150000m, p.Facturacion.CalcularTotales(id).Saldo);

        var efectivo = Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "EFECTIVO", 150000m, new DatosPago { MontoRecibido = 100000m }));
        Assert.Equal("Monto insuficiente", efectivo.Message);
        var transf = Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "TRANSFERENCIA", 150000m, new DatosPago()));
        Assert.Equal("Falta la referencia bancaria", transf.Message);
        var tarjeta = Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "TARJETA_DEBITO", 150000m, new DatosPago()));
        Assert.Equal("Falta tipo de tarjeta o autorización", tarjeta.Message);

        Assert.Equal(150000m, p.Facturacion.CalcularTotales(id).Saldo);
        var cierre = Assert.Throws<ValidacionDominioException>(() =>
            p.Facade.ProcesarSalida(id, "EFECTIVO", new DatosPago { MontoRecibido = 100000m }));
        Assert.Equal("Monto insuficiente", cierre.Message);
        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single(r => r.IdReserva == id).Estado);
        Assert.Empty(v.Facturas.Where(f => f.IdReserva == id));
    }

    [Fact] // [JIRA US-10 caso 1 Efectivo][D-33]: recibe 150000 para una deuda de 120000 -> vuelto 30000, el pago guarda el vuelto
    public void US10_Efectivo_Recibido150000_DeudaIgual120000_RegistraVuelto30000()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var recibo = _banco.Nueva().Pagos.RegistrarPago(id, "EFECTIVO", 120000m, new DatosPago { MontoRecibido = 150000m });
        Assert.Equal(30000m, recibo.Vuelto);
        Assert.Equal(0m, recibo.SaldoRestante);
        using var v = BancoF3.Contexto();
        Assert.Equal(30000m, v.Pagos.Single(x => x.IdPago == recibo.IdPago).Vuelto);
    }

    [Fact] // [JIRA US-10 RN1]: el abono no puede superar lo adeudado ni ser cero
    public void US10_Abono_MayorAlSaldoOCero_Rechaza()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var p = _banco.Nueva();
        Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "TRANSFERENCIA", 120001m, new DatosPago { ReferenciaBancaria = "X" }));
        Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "TRANSFERENCIA", 0m, new DatosPago { ReferenciaBancaria = "X" }));
        Assert.Throws<ValidacionDominioException>(() =>
            p.Pagos.RegistrarPago(id, "CHEQUE", 1000m, new DatosPago()));
        Assert.Equal(120000m, p.Facturacion.CalcularTotales(id).Saldo);
    }
}

/// <summary>US-06 check-out con Facade [JIRA US-06][DS4][CTR §4 Facade].</summary>
[Collection("SqlServer")]
public class CheckoutPruebas : IClassFixture<BancoF3>
{
    private readonly BancoF3 _banco;
    public CheckoutPruebas(BancoF3 banco) => _banco = banco;

    /// <summary>Reserva "#RS-1025" de Ana Gómez en la habitación 208 con total liquidado 350000 y pagado 350000 [JIRA US-06 datos].</summary>
    private (int Id, int Hab) Rs1025()
    {
        // Doble Clásica 1 noche 185000 + minibar 109117,65 = subtotal 294117,65 -> total 350000,00
        var (id, hab) = _banco.CrearReservaEnCurso("Doble Clásica", 1, "Ana Gómez", minibar: 109117.65m, numeroHabitacion: 208);
        _banco.Nueva().Pagos.RegistrarPago(id, "TARJETA_CREDITO", 350000m, new DatosPago { NumeroAutorizacion = "AP-1025" });
        return (id, hab);
    }

    [Fact] // [JIRA US-06 ESC1][JIRA US-06 datos]
    public void US06_ESC1_Rs1025_AnaGomez_Hab208_Saldo0_FacturaFolioPdfYEnLimpieza()
    {
        var (id, hab) = Rs1025();
        var p = _banco.Nueva();
        var antes = p.Facturacion.CalcularTotales(id);
        Assert.Equal(350000m, antes.Total);
        Assert.Equal(350000m, antes.Pagado);
        Assert.Equal(0m, antes.Saldo);

        var resultado = p.Facade.ProcesarSalida(id, "", new DatosPago());

        var f = resultado.Factura;
        Assert.True(f.Folio > 0);
        Assert.Equal(350000m, f.ValorTotal);
        Assert.Equal(55882.35m, f.ImpuestoIVA);
        Assert.Equal(hab, f.IdHabitacion);
        Assert.Equal(294117.65m, f.Detalles.Sum(d => d.SubTotal));
        var pdf = p.Facturacion.ObtenerPdf(f.IdFactura);
        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
        Assert.Equal(EstadoReserva.Finalizada, v.Reservas.Single(r => r.IdReserva == id).Estado);
        Assert.All(v.Pagos.Where(x => x.IdReserva == id), x => Assert.Equal(f.IdFactura, x.IdFactura));
    }

    /// <summary>Reserva "#RS-1026": minibar 40000 + late check-out 40000, saldo pendiente de 80000 [JIRA US-06 datos].</summary>
    private (int Id, int Hab) Rs1026()
        => _banco.CrearReservaEnCurso("Doble Clásica", 1, "Pedro Salazar", minibar: 40000m, late: true, pagado: 235350m);

    [Fact] // [JIRA US-06 ESC2]
    public void US06_ESC2_Rs1026_Saldo80000_RechazaYRedirigePagos()
    {
        var (id, hab) = Rs1026();
        var p = _banco.Nueva();
        Assert.Equal(80000m, p.Facturacion.CalcularTotales(id).Saldo);

        var ex = Assert.Throws<ConflictoDominioException>(() => p.Facade.ProcesarSalida(id, "", new DatosPago()));

        Assert.Equal("La reserva tiene saldo pendiente de $80.000: registre el pago antes del check-out", ex.Message);
        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single(r => r.IdReserva == id).Estado);
        Assert.Empty(v.Facturas.Where(f => f.IdReserva == id));
    }

    [Fact] // [DE#1 CP-H04]
    public void CP_H04_OcupadaCheckOut_SaldoPendiente_Rechazado_SigueOcupada()
    {
        var (id, hab) = _banco.CrearReservaEnCurso("Individual", 1);
        Assert.Throws<ConflictoDominioException>(() => _banco.Nueva().Facade.ProcesarSalida(id, "", new DatosPago()));
        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
    }

    [Fact] // [DE#4 CP-R07]
    public void CP_R07_EnCursoCheckOut_SaldoPendiente_Rechazado_SigueEnCurso()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1);
        Assert.Throws<ConflictoDominioException>(() => _banco.Nueva().Facade.ProcesarSalida(id, "", new DatosPago()));
        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single(r => r.IdReserva == id).Estado);
        Assert.Empty(v.HistorialReservas.Where(h => h.IdReserva == id && h.AccionRealizada == "Check-out"));
    }

    [Fact] // [D-33]: ProcesarSalida cobra el saldo restante con el método indicado
    public void Checkout_CobraSaldoRestanteConElMetodoIndicado()
    {
        var (id, hab) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var r = _banco.Nueva().Facade.ProcesarSalida(id, "TRANSFERENCIA", new DatosPago { ReferenciaBancaria = "TRF-1" });
        Assert.Equal(142800m, r.Factura.ValorTotal);
        using var v = BancoF3.Contexto();
        Assert.Equal(142800m, v.Pagos.Where(x => x.IdReserva == id).Sum(x => x.Monto));
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
    }

    [Fact] // [DS4 alt errorPago(motivo)]: pago rechazado, sin cambiar estados ni emitir factura
    public void Checkout_PagoRechazado_NoCambiaEstadosNiEmiteFactura()
    {
        var (id, hab) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var ex = Assert.Throws<ValidacionDominioException>(() =>
            _banco.Nueva().Facade.ProcesarSalida(id, "TRANSFERENCIA", new DatosPago()));
        Assert.Equal("Falta la referencia bancaria", ex.Message);
        using var v = BancoF3.Contexto();
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single(r => r.IdReserva == id).Estado);
        Assert.Empty(v.Facturas.Where(f => f.IdReserva == id));
    }

    private sealed class HistorialQueFalla : IRepositorioHistorial
    {
        public void RegistrarEvento(HistorialReserva evento) => throw new InvalidOperationException("Fallo forzado");
        public void Archivar(int idReserva, Factura factura) => throw new InvalidOperationException("Fallo forzado al archivar");
        public IReadOnlyList<HistorialReserva> BuscarEventos(FiltrosHistorial filtros) => throw new NotImplementedException();
    }

    [Fact] // [E2 §3 Facade]
    public void Facade_FallaArchivar_RollbackTotal()
    {
        var (id, hab) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 22800m);
        var p = _banco.Nueva(new HistorialQueFalla());

        Assert.Throws<InvalidOperationException>(() => p.Facade.ProcesarSalida(id, "TRANSFERENCIA", new DatosPago { ReferenciaBancaria = "TRF-2" }));

        using var v = BancoF3.Contexto();
        Assert.Empty(v.Facturas.Where(f => f.IdReserva == id));
        Assert.Equal(22800m, v.Pagos.Where(x => x.IdReserva == id).Sum(x => x.Monto)); // el cobro del saldo también se revirtió
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == hab).Estado);
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single(r => r.IdReserva == id).Estado);
    }

    [Fact] // [JIRA US-06 RN3 y riesgo]
    public void Factura_FolioConsecutivoUnico_Inmutable_NoSeEmiteDosVeces()
    {
        var (a, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 142800m);
        var (b, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 142800m);
        var p = _banco.Nueva();

        var fa = p.Facade.ProcesarSalida(a, "", new DatosPago()).Factura;
        var fb = p.Facade.ProcesarSalida(b, "", new DatosPago()).Factura;
        Assert.Equal(fa.Folio + 1, fb.Folio);

        // doble clic: no hay segunda factura
        var otra = _banco.Nueva();
        Assert.Equal("La reserva ya tiene una factura emitida",
            Assert.Throws<ConflictoDominioException>(() => otra.Facade.ProcesarSalida(a, "", new DatosPago())).Message);
        // un duplicado forzado contra el repositorio también se rechaza (índice único por reserva)
        var repo = new FacturaRepository(otra.Db);
        Assert.Throws<ConflictoDominioException>(() => repo.Guardar(new Factura { IdReserva = a, IdHabitacion = 1, FechaEmision = BancoF3.Hoy, ValorTotal = 1m }));
        // inmutable: una factura ya emitida no se vuelve a guardar
        Assert.Throws<ConflictoDominioException>(() => repo.Guardar(repo.BuscarPorId(fa.IdFactura)!));
        using var v = BancoF3.Contexto();
        Assert.Single(v.Facturas.Where(f => f.IdReserva == a));
    }

    [Fact] // [JIRA US-06 NFR][D-04]
    public void Pdf_Factura_SeGeneraEnMenosDe2s()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 142800m);
        var p = _banco.Nueva();
        var f = p.Facade.ProcesarSalida(id, "", new DatosPago()).Factura;
        p.Facturacion.ObtenerPdf(f.IdFactura); // primera llamada: carga de fuentes
        var sw = Stopwatch.StartNew();
        var pdf = p.Facturacion.ObtenerPdf(f.IdFactura);
        sw.Stop();
        Assert.True(pdf.Length > 500);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Tardó {sw.ElapsedMilliseconds} ms");
    }

    [Fact] // [JIRA US-06 RN2][D-23]
    public void Historial_Checkout_RegistraEventoYReservaFinalizada()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 142800m);
        var f = _banco.Nueva().Facade.ProcesarSalida(id, "", new DatosPago()).Factura;
        using var v = BancoF3.Contexto();
        var ev = Assert.Single(v.HistorialReservas.Where(h => h.IdReserva == id && h.AccionRealizada == "Check-out"));
        Assert.Equal("EnCurso", ev.ValorAnterior);
        Assert.Contains("Finalizada", ev.ValorNuevo);
        Assert.Contains(f.Folio.ToString(), ev.ValorNuevo);
        Assert.Equal(EstadoReserva.Finalizada, v.Reservas.Single(r => r.IdReserva == id).Estado);
    }

    [Fact] // [JIRA US-05 RN3]: la reserva EnCurso acepta consumos; las demás, no (409)
    public void Consumos_SoloEnReservaEnCurso_YValidados()
    {
        var (id, _) = _banco.CrearReservaEnCurso("Individual", 1, pagado: 142800m);
        var p = _banco.Nueva();
        p.Facturacion.AgregarConsumo(id, "Minibar", 2, 15000m);
        Assert.Equal(30000m, p.Facturacion.CalcularTotales(id).Consumos.Single(c => c.Concepto == "Minibar").SubTotal);
        Assert.Throws<ValidacionDominioException>(() => p.Facturacion.AgregarConsumo(id, "Minibar", 0, 15000m));
        Assert.Throws<ValidacionDominioException>(() => p.Facturacion.AgregarConsumo(id, "Servicio fantasma", 1, 100m));
        p.Facturacion.AgregarConsumo(id, "Late check-out", 1, 0m);
        Assert.Throws<ConflictoDominioException>(() => p.Facturacion.AgregarConsumo(id, "Late check-out", 1, 0m));
        p.Facade.ProcesarSalida(id, "TRANSFERENCIA", new DatosPago { ReferenciaBancaria = "TRF-9" });
        Assert.Throws<ConflictoDominioException>(() => _banco.Nueva().Facturacion.AgregarConsumo(id, "Minibar", 1, 100m));
        Assert.Throws<NoEncontradoException>(() => _banco.Nueva().Facturacion.CalcularTotales(987654));
    }

    [Fact] // [JIRA US-06 contexto]: "Check-outs pendientes" solo lista reservas EnCurso con su estado de pago
    public void Pendientes_ListaReservasEnCursoConEstadoDePago()
    {
        var (pagada, _) = _banco.CrearReservaEnCurso("Individual", 1, "Tomás Restrepo", pagado: 142800m);
        var (debe, _) = _banco.CrearReservaEnCurso("Individual", 1, "Laura Castillo");
        var lista = _banco.Nueva().Facturacion.ConsultarPendientes();
        Assert.Equal("Pagado", lista.Single(x => x.IdReserva == pagada).EstadoPago);
        var fila = lista.Single(x => x.IdReserva == debe);
        Assert.Equal("Pendiente", fila.EstadoPago);
        Assert.Equal(142800m, fila.Total);
        Assert.Equal("Laura Castillo", fila.Huesped);
    }
}

/// <summary>US-06 ESC3: el ama de llaves ve la habitación de inmediato [JIRA US-06 ESC3].</summary>
[Collection("SqlServer")]
public class CheckoutAlertaLimpiezaPruebas : IClassFixture<BancoF3>
{
    private readonly BancoF3 _banco;
    public CheckoutAlertaLimpiezaPruebas(BancoF3 banco) => _banco = banco;

    [Fact]
    public void US06_ESC3_Hab208_AmaDeLlavesLaVeEnLimpiezaDeInmediato()
    {
        var (id, hab) = _banco.CrearReservaEnCurso("Doble Clásica", 1, "Ana Gómez", minibar: 109117.65m, numeroHabitacion: 208);
        var p = _banco.Nueva();
        p.Pagos.RegistrarPago(id, "TARJETA_CREDITO", 350000m, new DatosPago { NumeroAutorizacion = "AP-1025" });
        Assert.Empty(p.Estados.ObtenerHabitaciones(EstadoHabitacion.EN_LIMPIEZA));

        p.Facade.ProcesarSalida(id, "", new DatosPago());

        // tablero del ama de llaves: consulta nueva, sin recargar nada (GestionEstadoHabitacion.ObtenerHabitaciones)
        var tablero = _banco.Nueva().Estados.ObtenerHabitaciones(EstadoHabitacion.EN_LIMPIEZA);
        Assert.Equal(208, Assert.Single(tablero).NumeroHabitacion);
    }
}
