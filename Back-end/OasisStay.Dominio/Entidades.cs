namespace OasisStay.Dominio;

/// <summary>Huésped [DC][D-08][D-20][JIRA US-03 RN1-RN3]. Las validaciones son de F2.</summary>
public class Cliente
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = "";
    public TipoDocumento TipoDocumento { get; set; }
    public string Documento { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public int? Edad { get; set; }

    /// <summary>Valida los datos digitados antes de guardarlos [JIRA US-03 RN1][JIRA US-03 RN2].</summary>
    public void RegistrarDatos() => ValidarDatos();

    /// <summary>Mismas reglas al actualizar los datos de un cliente existente [JIRA US-03 RN3].</summary>
    public void ActualizarDatos() => ValidarDatos();

    private void ValidarDatos()
    {
        var errores = new Dictionary<string, string[]>();
        if (!Enum.IsDefined(TipoDocumento)) errores["tipoDocumento"] = new[] { "El tipo de documento es obligatorio (CC, CE o Pasaporte)" };
        if (string.IsNullOrWhiteSpace(Documento)) errores["documento"] = new[] { "El número de documento es obligatorio" };
        if (string.IsNullOrWhiteSpace(Nombre)) errores["nombre"] = new[] { "Los nombres completos son obligatorios" };
        if (string.IsNullOrWhiteSpace(Telefono)) errores["telefono"] = new[] { "El teléfono es obligatorio" };
        else if (!ReglasHuesped.TelefonoValido(Telefono)) errores["telefono"] = new[] { "El teléfono solo admite dígitos, espacios, + y -" };
        if (string.IsNullOrWhiteSpace(Correo)) errores["correo"] = new[] { "El correo electrónico es obligatorio" };
        else if (!ReglasHuesped.CorreoValido(Correo)) errores["correo"] = new[] { "El correo electrónico no tiene un formato válido" };
        if (errores.Count > 0) throw new ValidacionDominioException("Hay campos con formato inválido.", errores);
    }
}

/// <summary>Reserva [DC][DE#4]. F2 implementa el check-in; F3 el total y el check-out.</summary>
public class Reserva
{
    public int IdReserva { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    /// <summary>Número de ocupantes [JIRA US-03 historia][JIRA US-04 RN1]; permite validar RN-05 al reasignar.</summary>
    public int Huespedes { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;
    public int IdCliente { get; set; }
    public Cliente? Cliente { get; set; }
    public int NumeroHabitacion { get; set; }
    public Habitacion? Habitacion { get; set; }
    public List<Servicio> Servicios { get; set; } = new();
    public List<Pago> Pagos { get; set; } = new();
    /// <summary>Consumos guardados; con ellos se reconstruye la cadena del Decorator [CTR §4 Decorator].</summary>
    public List<ConsumoReserva> Consumos { get; set; } = new();

    /// <summary>Total con IVA de la estadía y sus consumos [DC][E1 RF-05][JIRA US-05 RN2]; requiere <see cref="Habitacion"/> con su tipo.</summary>
    public decimal CalcularTotal()
        => ArmadorCuenta.Armar((FechaFin.Date - FechaInicio.Date).Days, Habitacion?.Tipo?.CalcularPrecioNoche() ?? 0m, Consumos, true).CalcularCosto();
    /// <summary>Penalidad registrada por cancelación tardía o No-Show: 100 % de la primera noche, sin IVA [JIRA US-08 RN2][D-44].</summary>
    public decimal Penalidad { get; set; }
    /// <summary>La penalidad fue exonerada por un Administrador [JIRA US-08 RN3][D-44].</summary>
    public bool PenalidadExonerada { get; set; }
    /// <summary>Justificación obligatoria de la exoneración [JIRA US-08 RN3][D-44].</summary>
    public string? MotivoExoneracion { get; set; }

    /// <summary>Confirmada → Cancelada [DE#4 CP-R03/CP-R04]; EnCurso, Finalizada, Cancelada y NoShow no cambian [DE#4 §5][CP-R08][D-44].</summary>
    public void CancelarReserva()
    {
        ValidarAbierta();
        Estado = EstadoReserva.Cancelada;
    }

    /// <summary>Confirmada sin check-in al fin del día de llegada → NoShow [DE#4 CP-R05][D-44].</summary>
    public void MarcarNoShow()
    {
        ValidarAbierta();
        Estado = EstadoReserva.NoShow;
    }

    /// <summary>Lanza el rechazo acordado si la reserva no está Confirmada [DE#4 §5][D-44].</summary>
    public void ValidarCancelable() => ValidarAbierta();

    private void ValidarAbierta()
    {
        if (Estado == EstadoReserva.Confirmada) return;
        throw new ConflictoDominioException(Estado == EstadoReserva.EnCurso ? MensajesReserva.CancelarEnCurso : MensajesReserva.ReservaCerrada);
    }

    /// <summary>Último instante de cancelación gratuita: hora de check-in del día de llegada menos el plazo [JIRA US-08 RN1][D-01][D-24].</summary>
    public DateTime LimiteCancelacionGratuita(TimeSpan horaCheckIn, int plazoHoras)
        => FechaInicio.Date + horaCheckIn - TimeSpan.FromHours(plazoHoras);

    /// <summary>Gratis hasta el límite (inclusive); después, 100 % de la primera noche sin IVA [JIRA US-08 RN1/RN2][D-44].</summary>
    public decimal CalcularPenalidad(DateTime ahora, TimeSpan horaCheckIn, int plazoHoras)
        => ahora <= LimiteCancelacionGratuita(horaCheckIn, plazoHoras) ? 0m : PrimeraNoche();

    /// <summary>Valor de la primera noche = precio base de la habitación de la reserva [JIRA US-08 RN2].</summary>
    public decimal PrimeraNoche() => Math.Round(Habitacion?.Tipo?.CalcularPrecioNoche() ?? 0m, 2);
    /// <summary>Confirmada → EnCurso [DE#4]; las guardas de documento y fecha están en la sobrecarga [D-31].</summary>
    public void HacerCheckIn()
    {
        if (Estado != EstadoReserva.Confirmada) throw new ConflictoDominioException(MensajesReserva.ReservaNoConfirmada);
        Estado = EstadoReserva.EnCurso;
    }

    /// <summary>Check-in con guardas "FechaInicio alcanzada y documento verificado" [DE#4][DE#1][D-31].</summary>
    public ResultadoOperacion HacerCheckIn(string? documentoVerificado, DateTime ahora)
    {
        if (Estado != EstadoReserva.Confirmada) return ResultadoOperacion.Rechazada(MensajesReserva.ReservaNoConfirmada);
        if (string.IsNullOrWhiteSpace(documentoVerificado) || Cliente == null
            || !string.Equals(documentoVerificado.Trim(), Cliente.Documento.Trim(), StringComparison.OrdinalIgnoreCase))
            return ResultadoOperacion.Rechazada(MensajesReserva.DocumentoNoVerificado);
        if (ahora.Date < FechaInicio.Date) return ResultadoOperacion.Rechazada(MensajesReserva.CheckInAntesDeLlegada);
        HacerCheckIn();
        return ResultadoOperacion.Exitosa();
    }

    /// <summary>EnCurso → Finalizada al archivar el check-out [DE#4][JIRA US-06 RN2]; el saldo $0 se verifica antes en el cierre de cuenta.</summary>
    public void HacerCheckOut()
    {
        if (Estado != EstadoReserva.EnCurso) throw new ConflictoDominioException(MensajesFacturacion.ReservaNoEnCurso);
        Estado = EstadoReserva.Finalizada;
    }
}

/// <summary>Tipo de habitación [DC][D-29].</summary>
public class TipoHabitacion
{
    public int IdTipo { get; set; }
    public string Nombre { get; set; } = "";
    public int CapacidadMax { get; set; }
    public decimal PrecioBase { get; set; }
    public string Descripcion { get; set; } = "";

    public decimal CalcularPrecioNoche() => PrecioBase;
}

/// <summary>Habitación [DC][GoF State][D-28]. Transiciones y reconstrucción del estado: F1.</summary>
public class Habitacion
{
    public int NumeroHabitacion { get; set; }
    public int Piso { get; set; }
    public string? MotivoMantenimiento { get; set; }
    public EstadoHabitacion Estado { get; set; } = EstadoHabitacion.DISPONIBLE;
    public int IdTipo { get; set; }
    public TipoHabitacion? Tipo { get; set; }

    /// <summary>Estado de comportamiento, no persistido [GoF State].</summary>
    public IEstadoHabitacion? estadoActual { get; set; }

    /// <summary>Si aún no se reconstruyó, se arma desde el texto persistido [E2 §3 State].</summary>
    private IEstadoHabitacion Actual => estadoActual ??= EstadoDesdeTexto(Estado.ToString());

    public ResultadoOperacion Reservar() => Actual.Reservar(this);
    public ResultadoOperacion HacerCheckIn() => Actual.HacerCheckIn(this);
    public ResultadoOperacion HacerCheckOut() => Actual.HacerCheckOut(this);
    public ResultadoOperacion MarcarDisponible() => Actual.MarcarDisponible(this);

    /// <summary>Solo desde Disponible y con motivo de al menos 10 caracteres [DE#1 CP-H07][D-24][JIRA US-02 RN2].</summary>
    public ResultadoOperacion MarcarMantenimiento(string? motivo)
        => Actual is EstadoDisponible d
            ? d.MarcarMantenimiento(this, motivo)
            : ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Actual.Nombre));

    /// <summary>Asigna el estado persistido y reconstruye el objeto de estado [GoF State].</summary>
    public void CambiarEstado(EstadoHabitacion nuevo)
    {
        Estado = nuevo;
        estadoActual = EstadoDesdeTexto(nuevo.ToString());
    }

    /// <summary>Reservar solo es posible desde Disponible [DE#1 §4 Guardas].</summary>
    public bool PuedeReservarse() => Actual is EstadoDisponible;

    /// <summary>Reconstruye <see cref="estadoActual"/> desde el texto persistido [E2 §3 State].</summary>
    public void ReconstruirEstado() => estadoActual = EstadoDesdeTexto(Estado.ToString());

    /// <summary>Texto persistido a objeto de estado [E2 §3 State].</summary>
    public static IEstadoHabitacion EstadoDesdeTexto(string texto) => texto switch
    {
        nameof(EstadoHabitacion.DISPONIBLE) => new EstadoDisponible(),
        nameof(EstadoHabitacion.RESERVADA) => new EstadoReservada(),
        nameof(EstadoHabitacion.OCUPADO) => new EstadoOcupada(),
        nameof(EstadoHabitacion.EN_LIMPIEZA) => new EstadoEnLimpieza(),
        nameof(EstadoHabitacion.MANTENIMIENTO) => new EstadoMantenimiento(),
        _ => throw new ArgumentException($"Estado de habitación desconocido: {texto}", nameof(texto)),
    };
}

/// <summary>Servicio de consumo [DC][D-29].</summary>
public class Servicio
{
    public int IdServicio { get; set; }
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public string Descripcion { get; set; } = "";
    public List<Reserva> Reservas { get; set; } = new();
}

/// <summary>Factura [DC][E1 RF-06][GoF Facade]; Folio único consecutivo.</summary>
public class Factura
{
    public int IdFactura { get; set; }
    public int Folio { get; set; }
    public DateTime FechaEmision { get; set; }
    public decimal ImpuestoIVA { get; set; }
    public decimal ValorTotal { get; set; }
    public int IdReserva { get; set; }
    public int IdHabitacion { get; set; }
    public List<DetalleFactura> Detalles { get; set; } = new();
}

/// <summary>Línea de factura [DC].</summary>
public class DetalleFactura
{
    public int IdDetalle { get; set; }
    public int IdFactura { get; set; }
    public string Concepto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal SubTotal { get; set; }
}

/// <summary>Pago [DC][D-33]; asociado a Reserva y Factura.</summary>
public class Pago
{
    public int IdPago { get; set; }
    public DateTime FechaPago { get; set; }
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? TipoTarjeta { get; set; }
    public string? NumeroAutorizacion { get; set; }
    public string? ReferenciaBancaria { get; set; }
    public decimal? Vuelto { get; set; }
    public int IdReserva { get; set; }
    public int? IdFactura { get; set; }
}

/// <summary>Bitácora de solo inserción [DC][E1 RF-07][D-23].</summary>
public class HistorialReserva
{
    public int IdHistorial { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string AccionRealizada { get; set; } = "";
    public int IdReserva { get; set; }
    public string Usuario { get; set; } = "";
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
}

/// <summary>Notificación simulada guardada en BD [D-04].</summary>
public class Notificacion
{
    public int IdNotificacion { get; set; }
    public DateTime Fecha { get; set; }
    public string Destinatario { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Mensaje { get; set; } = "";
}

/// <summary>Solicitud de reserva [GoF Chain][CTR §4].</summary>
public class SolicitudReserva
{
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Huespedes { get; set; }
    public Habitacion? Habitacion { get; set; }
    /// <summary>Huésped titular, guardado o actualizado al registrar [JIRA US-03 RN3][D-09].</summary>
    public Cliente? Cliente { get; set; }
    /// <summary>Usuario que registra, para la bitácora [JIRA US-07 RN3][D-23].</summary>
    public string Usuario { get; set; } = "";
    /// <summary>Reservas de la habitación en el rango, cargadas por GestionReservas [CTR §5].</summary>
    public IReadOnlyList<Reserva> ReservasExistentes { get; set; } = Array.Empty<Reserva>();

    /// <summary>RN-01: hay una reserva activa (Confirmada o EnCurso) que se cruza; salida y llegada el mismo día no se cruzan [E1 RN-01].</summary>
    public bool TieneReservaSolapada() => ReservasExistentes.Any(r =>
        (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso)
        && r.FechaInicio < CheckOut && r.FechaFin > CheckIn);
}
