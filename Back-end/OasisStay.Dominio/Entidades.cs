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

    public void RegistrarDatos() => throw new NotImplementedException("F2");
    public void ActualizarDatos() => throw new NotImplementedException("F2");
}

/// <summary>Reserva [DC][DE#4]. Cuerpos en F2/F3/F4.</summary>
public class Reserva
{
    public int IdReserva { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;
    public int IdCliente { get; set; }
    public Cliente? Cliente { get; set; }
    public int NumeroHabitacion { get; set; }
    public Habitacion? Habitacion { get; set; }
    public List<Servicio> Servicios { get; set; } = new();
    public List<Pago> Pagos { get; set; } = new();

    public decimal CalcularTotal() => throw new NotImplementedException("F3");
    public void CancelarReserva() => throw new NotImplementedException("F2");
    public void HacerCheckIn() => throw new NotImplementedException("F4");
    public void HacerCheckOut() => throw new NotImplementedException("F4");
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

    private IEstadoHabitacion Actual => estadoActual
        ?? throw new InvalidOperationException("El estado de la habitación no ha sido reconstruido (F1).");

    public ResultadoOperacion Reservar() => Actual.Reservar(this);
    public ResultadoOperacion HacerCheckIn() => Actual.HacerCheckIn(this);
    public ResultadoOperacion HacerCheckOut() => Actual.HacerCheckOut(this);
    public ResultadoOperacion MarcarDisponible() => Actual.MarcarDisponible(this);

    /// <summary>Asigna el estado persistido; las guardas de transición son de F1.</summary>
    public void CambiarEstado(EstadoHabitacion nuevo) => Estado = nuevo;
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

/// <summary>Solicitud de reserva [GoF Chain][CTR §4]; la cadena y TieneReservaSolapada son de F2.</summary>
public class SolicitudReserva
{
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Huespedes { get; set; }
    public Habitacion? Habitacion { get; set; }
}
