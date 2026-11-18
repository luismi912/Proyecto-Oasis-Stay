using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Calcula, no genera PDF [CTR §5 MotorCuentasFacturacion][E2 §6 SRP][GoF Decorator].</summary>
public class MotorCuentasFacturacion
{
    /// <summary>Totales, pagado y saldo de una reserva a partir de sus consumos y abonos guardados [DS4 calcularTotales].</summary>
    public ResumenCobro Calcular(Reserva reserva, IReadOnlyList<ConsumoReserva> consumos, IReadOnlyList<Pago> pagos,
        bool salidaTardiaSugerida, int? idFactura)
    {
        var noches = (reserva.FechaFin.Date - reserva.FechaInicio.Date).Days;
        var tarifa = reserva.Habitacion?.Tipo?.CalcularPrecioNoche() ?? 0m;
        var subtotal = ArmadorCuenta.Armar(noches, tarifa, consumos, false).CalcularCosto();
        var total = ArmadorCuenta.Armar(noches, tarifa, consumos, true).CalcularCosto();
        var pagado = pagos.Sum(p => p.Monto);
        return new ResumenCobro(
            reserva.IdReserva, reserva.Cliente?.Nombre ?? "", reserva.NumeroHabitacion, reserva.Habitacion?.Tipo?.Nombre ?? "",
            reserva.FechaInicio, reserva.FechaFin, reserva.Estado.ToString(), noches, tarifa,
            consumos.Select(c => new LineaConsumo(c.Concepto, c.Cantidad, c.ValorUnitario, c.SubTotal)).ToList(),
            consumos.Any(c => c.EsLateCheckout), salidaTardiaSugerida,
            subtotal, ReglasFacturacion.Redondear(total - subtotal), total, pagado, ReglasFacturacion.Redondear(total - pagado),
            pagos.Select(p => new LineaPago(p.IdPago, p.FechaPago, p.MetodoPago.ToString(), p.Monto, p.Vuelto)).ToList(),
            idFactura);
    }

    /// <summary>Desglose de una cotización sin reserva [JIRA US-05 ESC1].</summary>
    public CotizacionResultado Cotizar(int noches, decimal tarifa, IEnumerable<ConsumoReserva> consumos)
    {
        var lista = consumos.ToList();
        var subtotal = ArmadorCuenta.Armar(noches, tarifa, lista, false).CalcularCosto();
        var total = ArmadorCuenta.Armar(noches, tarifa, lista, true).CalcularCosto();
        return new CotizacionResultado(noches, tarifa, subtotal, ReglasFacturacion.Redondear(total - subtotal), total);
    }
}

/// <summary>Abonos con su estrategia de pago; cada abono es un <see cref="Pago"/> con su recibo [CTR §5 GestionPagos][D-13][D-33][JIRA US-10].</summary>
public class GestionPagos : IGestionPagos
{
    private readonly IRepositorioReservas _reservas;
    private readonly IRepositorioConsumos _consumos;
    private readonly IRepositorioPagos _pagos;
    private readonly MotorCuentasFacturacion _motor;
    private readonly ProcesadorPagos _procesador;
    private readonly IReloj _reloj;

    public GestionPagos(IRepositorioReservas reservas, IRepositorioConsumos consumos, IRepositorioPagos pagos,
        MotorCuentasFacturacion motor, ProcesadorPagos procesador, IReloj reloj)
    {
        _reservas = reservas;
        _consumos = consumos;
        _pagos = pagos;
        _motor = motor;
        _procesador = procesador;
        _reloj = reloj;
    }

    /// <summary>TARJETA_CREDITO y TARJETA_DEBITO usan la estrategia "Tarjeta" [CTR §4 Strategy].</summary>
    private static string Estrategia(MetodoPago m) => m switch
    {
        MetodoPago.EFECTIVO => "Efectivo",
        MetodoPago.TRANSFERENCIA => "Transferencia",
        _ => "Tarjeta",
    };

    public ReciboPago RegistrarPago(int idReserva, string metodoPago, decimal monto, DatosPago datos)
    {
        var reserva = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        if (reserva.Estado != EstadoReserva.EnCurso) throw new ConflictoDominioException(MensajesFacturacion.ReservaNoEnCurso);
        if (!Enum.TryParse<MetodoPago>(metodoPago, true, out var metodo) || !Enum.IsDefined(metodo))
            throw new ValidacionDominioException(MensajesFacturacion.MetodoInvalido);
        monto = ReglasFacturacion.Redondear(monto);
        if (monto <= 0) throw new ValidacionDominioException(MensajesFacturacion.MontoInvalido);

        var resumen = _motor.Calcular(reserva, _consumos.ConsultarPorReserva(idReserva), _pagos.ConsultarPorReserva(idReserva), false, null);
        if (monto > resumen.Saldo) throw new ValidacionDominioException(MensajesFacturacion.MontoExcedeSaldo(resumen.Saldo));

        datos ??= new DatosPago();
        if (Estrategia(metodo) == "Tarjeta" && string.IsNullOrWhiteSpace(datos.TipoTarjeta))
            datos.TipoTarjeta = metodo == MetodoPago.TARJETA_CREDITO ? "Crédito" : "Débito"; // el método ya define el tipo [JIRA US-10 historia]

        var resultado = _procesador.Cobrar(Estrategia(metodo), monto, datos);
        if (!resultado.EsAprobado) throw new ValidacionDominioException(resultado.Motivo!);

        var esEfectivo = metodo == MetodoPago.EFECTIVO;
        var pago = new Pago
        {
            IdReserva = idReserva,
            FechaPago = _reloj.Ahora,
            Monto = monto,
            MetodoPago = metodo,
            TipoTarjeta = Estrategia(metodo) == "Tarjeta" ? datos.TipoTarjeta : null,
            NumeroAutorizacion = Estrategia(metodo) == "Tarjeta" ? datos.NumeroAutorizacion : null,
            ReferenciaBancaria = metodo == MetodoPago.TRANSFERENCIA ? datos.ReferenciaBancaria : null,
            Vuelto = esEfectivo ? resultado.Vuelto : null,
        };
        _pagos.Guardar(pago);
        return new ReciboPago(pago.IdPago, pago.FechaPago, monto, metodo.ToString(), pago.Vuelto, ReglasFacturacion.Redondear(resumen.Saldo - monto));
    }
}

/// <summary>Cotización, totales, consumos, cierre de cuenta y PDF [CTR §5 ServicioFacturacion][DS4][E1 RF-05][E1 RF-06].</summary>
public class ServicioFacturacion : IServicioFacturacion
{
    private readonly IRepositorioReservas _reservas;
    private readonly IRepositorioHabitaciones _habitaciones;
    private readonly IRepositorioTiposHabitacion _tipos;
    private readonly IRepositorioConsumos _consumos;
    private readonly IRepositorioPagos _pagos;
    private readonly IFacturaRepository _facturas;
    private readonly MotorCuentasFacturacion _motor;
    private readonly IGestionPagos _gestionPagos;
    private readonly IGeneradorFacturaPdf _pdf;
    private readonly IReloj _reloj;
    private readonly PoliticasOpciones _politicas;

    public ServicioFacturacion(IRepositorioReservas reservas, IRepositorioHabitaciones habitaciones, IRepositorioTiposHabitacion tipos,
        IRepositorioConsumos consumos, IRepositorioPagos pagos, IFacturaRepository facturas, MotorCuentasFacturacion motor,
        IGestionPagos gestionPagos, IGeneradorFacturaPdf pdf, IReloj reloj, PoliticasOpciones politicas)
    {
        _reservas = reservas;
        _habitaciones = habitaciones;
        _tipos = tipos;
        _consumos = consumos;
        _pagos = pagos;
        _facturas = facturas;
        _motor = motor;
        _gestionPagos = gestionPagos;
        _pdf = pdf;
        _reloj = reloj;
        _politicas = politicas;
    }

    private Reserva Cargar(int idReserva)
        => _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");

    public ResumenCobro CalcularTotales(int idReserva)
    {
        var reserva = Cargar(idReserva);
        // Salida tardía = hora real posterior a la máxima de check-out [D-12][DE#1 registrarHoraCheckOut]
        var tardia = _reloj.Ahora.Date >= reserva.FechaFin.Date && _reloj.Ahora.TimeOfDay > _politicas.HoraMaximaCheckout;
        return _motor.Calcular(reserva, _consumos.ConsultarPorReserva(idReserva), _pagos.ConsultarPorReserva(idReserva),
            tardia, _facturas.BuscarPorReserva(idReserva)?.IdFactura);
    }

    /// <summary>Cotiza noches × tarifa del tipo, consumos, recargo y IVA [JIRA US-05 ESC1-ESC3][D-22].</summary>
    public CotizacionResultado Cotizar(SolicitudCotizacion solicitud)
    {
        var noches = (solicitud.FechaFin.Date - solicitud.FechaInicio.Date).Days;
        if (noches <= 0) throw new ValidacionDominioException(MensajesReserva.FechasInvalidas); // sin 0 noches ni Day Use [D-22]

        decimal tarifa;
        if (solicitud.NumeroHabitacion is int numero)
        {
            var h = _habitaciones.BuscarPorNumero(numero) ?? throw new NoEncontradoException("La habitación no existe.");
            tarifa = h.Tipo?.CalcularPrecioNoche() ?? 0m;
        }
        else if (solicitud.IdTipo is int idTipo)
        {
            var t = _tipos.BuscarPorId(idTipo) ?? throw new NoEncontradoException("El tipo de habitación no existe.");
            tarifa = t.CalcularPrecioNoche();
        }
        else throw new ValidacionDominioException("Indique la habitación o el tipo de habitación.");

        var consumos = new List<ConsumoReserva>();
        foreach (var c in solicitud.Consumos ?? Array.Empty<ConsumoCotizado>())
        {
            if (string.IsNullOrWhiteSpace(c.Concepto) || c.Cantidad <= 0 || c.Monto < 0)
                throw new ValidacionDominioException(MensajesFacturacion.ConsumoInvalido);
            consumos.Add(new ConsumoReserva { Concepto = c.Concepto.Trim(), Cantidad = c.Cantidad, ValorUnitario = c.Monto });
        }
        if (solicitud.SalidaTardia && !consumos.Any(c => c.EsLateCheckout))
            consumos.Add(new ConsumoReserva { Concepto = ReglasFacturacion.ConceptoLateCheckout, Cantidad = 1, ValorUnitario = ReglasFacturacion.RecargoLateCheckout });
        return _motor.Cotizar(noches, tarifa, consumos);
    }

    /// <summary>Reservas EnCurso con su estado de pago, para "Check-outs pendientes" [E2 §6].</summary>
    public IReadOnlyList<PendienteCobro> ConsultarPendientes()
        => _reservas.Listar(EstadoReserva.EnCurso, null)
            .Select(r => CalcularTotales(r.IdReserva))
            .Select(t => new PendienteCobro(t.IdReserva, t.Huesped, t.NumeroHabitacion, t.FechaFin,
                t.Saldo > 0 ? "Pendiente" : "Pagado", t.Total, t.Saldo))
            .ToList();

    /// <summary>Añade un consumo y el total se recalcula [actividad-4 "Añadir consumos extra"][D-29]; "Late check-out" aplica el recargo fijo [E1 RN-04].</summary>
    public ConsumoReserva AgregarConsumo(int idReserva, string concepto, int cantidad, decimal valorUnitario)
    {
        var reserva = Cargar(idReserva);
        if (reserva.Estado != EstadoReserva.EnCurso) throw new ConflictoDominioException(MensajesFacturacion.ReservaNoEnCurso);
        if (string.IsNullOrWhiteSpace(concepto)) throw new ValidacionDominioException(MensajesFacturacion.ConsumoInvalido);
        concepto = concepto.Trim();

        ConsumoReserva consumo;
        if (string.Equals(concepto, ReglasFacturacion.ConceptoLateCheckout, StringComparison.OrdinalIgnoreCase))
        {
            if (_consumos.ConsultarPorReserva(idReserva).Any(c => c.EsLateCheckout))
                throw new ConflictoDominioException(MensajesFacturacion.SalidaTardiaRegistrada);
            consumo = new ConsumoReserva
            {
                IdReserva = idReserva, Concepto = ReglasFacturacion.ConceptoLateCheckout, Cantidad = 1,
                ValorUnitario = ReglasFacturacion.RecargoLateCheckout,
            };
        }
        else
        {
            if (cantidad <= 0 || valorUnitario <= 0) throw new ValidacionDominioException(MensajesFacturacion.ConsumoInvalido);
            var servicio = _consumos.BuscarServicio(concepto) ?? throw new ValidacionDominioException(MensajesFacturacion.ServicioNoExiste);
            consumo = new ConsumoReserva
            {
                IdReserva = idReserva, IdServicio = servicio.IdServicio, Concepto = servicio.Nombre,
                Cantidad = cantidad, ValorUnitario = ReglasFacturacion.Redondear(valorUnitario),
            };
        }
        consumo.Fecha = _reloj.Ahora;
        _consumos.Agregar(consumo);
        return consumo;
    }

    /// <summary>
    /// Verifica saldo $0 (cobrando el saldo restante si se indica método) y emite la factura con folio consecutivo único
    /// [DS4 cerrarCuenta][JIRA US-06 RN1/RN3][D-33].
    /// </summary>
    public Factura CerrarCuenta(int idReserva, string? metodoPago, DatosPago datos)
    {
        var reserva = Cargar(idReserva);
        if (_facturas.BuscarPorReserva(idReserva) != null) throw new ConflictoDominioException(MensajesFacturacion.YaFacturada);
        if (reserva.Estado != EstadoReserva.EnCurso) throw new ConflictoDominioException(MensajesFacturacion.ReservaNoEnCurso);

        var resumen = CalcularTotales(idReserva);
        if (resumen.Saldo > 0)
        {
            if (string.IsNullOrWhiteSpace(metodoPago))
                throw new ConflictoDominioException(MensajesFacturacion.SaldoPendiente(resumen.Saldo)); // redirige a pagos [JIRA US-06 ESC2]
            _gestionPagos.RegistrarPago(idReserva, metodoPago, resumen.Saldo, datos ?? new DatosPago());
        }

        var factura = new Factura
        {
            FechaEmision = _reloj.Ahora,
            ImpuestoIVA = resumen.Iva,
            ValorTotal = resumen.Total,
            IdReserva = idReserva,
            IdHabitacion = reserva.NumeroHabitacion,
        };
        factura.Detalles.Add(new DetalleFactura
        {
            Concepto = ReglasFacturacion.ConceptoEstadia, Cantidad = resumen.Noches, ValorUnitario = resumen.Tarifa,
            SubTotal = ReglasFacturacion.Redondear(resumen.Noches * resumen.Tarifa),
        });
        foreach (var c in resumen.Consumos)
            factura.Detalles.Add(new DetalleFactura { Concepto = c.Concepto, Cantidad = c.Cantidad, ValorUnitario = c.ValorUnitario, SubTotal = c.SubTotal });

        _facturas.Guardar(factura);
        foreach (var p in _pagos.ConsultarPorReserva(idReserva))
        {
            p.IdFactura = factura.IdFactura; // el abono queda ligado a la factura [D-33]
            _pagos.Guardar(p);
        }
        return factura;
    }

    /// <summary>PDF de una factura ya cerrada [D-04][E1 RF-06].</summary>
    public byte[] ObtenerPdf(int idFactura)
    {
        var factura = _facturas.BuscarPorId(idFactura) ?? throw new NoEncontradoException("La factura no existe.");
        var huesped = _reservas.BuscarPorId(factura.IdReserva)?.Cliente?.Nombre ?? "";
        return _pdf.Generar(factura, huesped, factura.IdHabitacion);
    }
}

/// <summary>Cambia la habitación a EnLimpieza usando State [CTR §4 Facade][DS4 marcarEnLimpieza][DE#1].</summary>
public class ServicioHabitacion : IServicioHabitacion
{
    private readonly IRepositorioHabitaciones _habitaciones;
    public ServicioHabitacion(IRepositorioHabitaciones habitaciones) => _habitaciones = habitaciones;

    public void MarcarEnLimpieza(int numeroHabitacion)
    {
        var h = _habitaciones.BuscarPorNumero(numeroHabitacion) ?? throw new NoEncontradoException("La habitación no existe.");
        var r = h.HacerCheckOut(); // estadoActual.HacerCheckOut(hab) → EstadoEnLimpieza
        if (!r.EsExitosa) throw new ConflictoDominioException(r.Motivo!);
        _habitaciones.Guardar(h);
    }
}

/// <summary>
/// Fachada del check-out en una transacción: cerrar cuenta → habitación a EnLimpieza → archivar historial; si un paso falla se
/// revierte todo [GoF Facade][CTR §4][DS4][E2 §3 Facade][D-10][JIRA US-06 RN2].
/// </summary>
public class CheckoutFacade : ICheckoutFacade
{
    private readonly IServicioFacturacion _facturacion;
    private readonly IServicioHabitacion _habitaciones;
    private readonly IRepositorioHistorial _historial;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;

    public CheckoutFacade(IServicioFacturacion facturacion, IServicioHabitacion habitaciones, IRepositorioHistorial historial, IUnidadDeTrabajo unidadDeTrabajo)
    {
        _facturacion = facturacion;
        _habitaciones = habitaciones;
        _historial = historial;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public ResultadoCheckout ProcesarSalida(int idReserva, string metodoPago, DatosPago datos)
    {
        using var tx = _unidadDeTrabajo.Iniciar();
        var factura = _facturacion.CerrarCuenta(idReserva, metodoPago, datos);
        _habitaciones.MarcarEnLimpieza(factura.IdHabitacion);
        _historial.Archivar(idReserva, factura);
        tx.Confirmar();
        return ResultadoCheckout.Exitoso(factura);
    }
}
