using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Operaciones de registro, reasignación y check-in de F2 [CTR §5][D-09][D-21][D-31].</summary>
public interface IRegistroReservas
{
    Cliente? BuscarHuesped(TipoDocumento tipo, string documento);
    (Cliente Cliente, bool Creado) GuardarHuesped(Cliente datos);
    IReadOnlyList<Reserva> ConsultarReservas(EstadoReserva? estado, string? texto);
    Reserva Reasignar(int idReserva, int numeroHabitacionNueva, bool confirmarUpgrade, string usuario);
    Reserva HacerCheckIn(int idReserva, string? documento, string usuario);
}

public partial class GestionReservas : IRegistroReservas
{
    private IRepositorioHuespedes _huespedes = null!;
    private IRepositorioHistorial _historial = null!;
    private INotificador _notificador = null!;
    private IReloj _reloj = null!;
    private MotorDisponibilidadAsignacion _motor = null!;

    public GestionReservas(IRepositorioHabitaciones habitaciones, IRepositorioReservas reservas,
        IRepositorioHuespedes huespedes, IRepositorioHistorial historial, INotificador notificador,
        IReloj reloj, MotorDisponibilidadAsignacion motor) : this(habitaciones, reservas)
    {
        _huespedes = huespedes;
        _historial = historial;
        _notificador = notificador;
        _reloj = reloj;
        _motor = motor;
    }

    /// <summary>La cadena se arma aquí y el front solo envía la solicitud [D-09][GoF Chain].</summary>
    private static ValidadorReserva ArmarCadena()
    {
        var fechas = new ValidadorFechas();
        fechas.Siguiente(new ValidadorCapacidad())
              .Siguiente(new ValidadorEstadoHabitacion())
              .Siguiente(new ValidadorSolapamiento());
        return fechas;
    }

    private static void LanzarSiRechaza(ResultadoValidacion r)
    {
        if (r.EsValido) return;
        if (r.Mensaje == MensajesReserva.Solapamiento) throw new ConflictoDominioException(r.Mensaje!);
        throw new ValidacionDominioException(r.Mensaje!);
    }

    private void Historial(int idReserva, string usuario, string accion, string? anterior, string? nuevo)
        => _historial.RegistrarEvento(new HistorialReserva
        {
            FechaRegistro = _reloj.Ahora,
            AccionRealizada = accion,
            IdReserva = idReserva,
            Usuario = string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario,
            ValorAnterior = anterior,
            ValorNuevo = nuevo,
        });

    /// <summary>Alta o actualización del huésped por tipo y número de documento [JIRA US-03 RN3].</summary>
    private Cliente GuardarCliente(Cliente datos, out bool creado)
    {
        var existente = _huespedes.BuscarPorDocumento(datos.TipoDocumento, datos.Documento.Trim());
        creado = existente == null;
        if (existente == null)
        {
            datos.Documento = datos.Documento.Trim();
            datos.RegistrarDatos();
            _huespedes.Guardar(datos);
            return datos;
        }
        existente.Nombre = datos.Nombre;
        existente.Telefono = datos.Telefono;
        existente.Correo = datos.Correo;
        if (datos.Edad.HasValue) existente.Edad = datos.Edad;
        existente.ActualizarDatos();
        _huespedes.Guardar(existente);
        return existente;
    }

    public Cliente? BuscarHuesped(TipoDocumento tipo, string documento)
        => _huespedes.BuscarPorDocumento(tipo, documento.Trim());

    public (Cliente Cliente, bool Creado) GuardarHuesped(Cliente datos)
    {
        datos.RegistrarDatos();
        var c = GuardarCliente(datos, out var creado);
        return (c, creado);
    }

    public IReadOnlyList<Reserva> ConsultarReservas(EstadoReserva? estado, string? texto)
        => _reservas.Listar(estado, texto);

    /// <summary>Valida en cadena, guarda el huésped, crea la reserva Confirmada y reserva la habitación en una transacción [DS1][DE#4][D-09].</summary>
    public Reserva RegistrarReserva(SolicitudReserva solicitud)
    {
        var datos = solicitud.Cliente ?? throw new ValidacionDominioException("Faltan los datos del huésped.");
        var numero = solicitud.Habitacion?.NumeroHabitacion
            ?? throw new ValidacionDominioException("Seleccione una habitación.");
        datos.RegistrarDatos(); // campos obligatorios y formato [JIRA US-03 RN1/RN2]

        return _motor.EjecutarBloqueado(new[] { numero }, () =>
        {
            var habitacion = _habitaciones.BuscarPorNumero(numero)
                ?? throw new NoEncontradoException("La habitación no existe.");
            solicitud.Habitacion = habitacion;
            solicitud.ReservasExistentes = _reservas.BuscarPorHabitacionYFechas(habitacion, solicitud.CheckIn, solicitud.CheckOut);
            LanzarSiRechaza(ArmarCadena().Validar(solicitud));

            var cliente = GuardarCliente(datos, out _);
            var reserva = new Reserva
            {
                FechaInicio = solicitud.CheckIn,
                FechaFin = solicitud.CheckOut,
                Huespedes = solicitud.Huespedes,
                Estado = EstadoReserva.Confirmada,
                Cliente = cliente,
                IdCliente = cliente.IdCliente,
                Habitacion = habitacion,
                NumeroHabitacion = habitacion.NumeroHabitacion,
            };
            var r = habitacion.Reservar();
            if (!r.EsExitosa) throw new ConflictoDominioException(r.Motivo!);
            _reservas.Guardar(reserva);
            _habitaciones.Guardar(habitacion);

            Historial(reserva.IdReserva, solicitud.Usuario, "Reserva creada", null,
                $"Confirmada · Hab. {numero} · {reserva.FechaInicio:dd/MM/yyyy}–{reserva.FechaFin:dd/MM/yyyy}");
            _notificador.Notificar(cliente.Correo, "Reserva confirmada",
                $"Su reserva #{reserva.IdReserva} en la habitación {numero} del {reserva.FechaInicio:dd/MM/yyyy} al {reserva.FechaFin:dd/MM/yyyy} está confirmada.");
            return reserva;
        });
    }

    /// <summary>Reasignación antes del check-in: anterior → Disponible y nueva → Reservada en una transacción; Upgrade con confirmación [D-21][JIRA US-04 ESC3/RN3].</summary>
    public Reserva Reasignar(int idReserva, int numeroHabitacionNueva, bool confirmarUpgrade, string usuario)
    {
        var previa = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        return _motor.EjecutarBloqueado(new[] { previa.NumeroHabitacion, numeroHabitacionNueva }, () =>
        {
            var reserva = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
            if (reserva.Estado != EstadoReserva.Confirmada) throw new ConflictoDominioException(MensajesReserva.ReservaNoConfirmada);
            var anterior = _habitaciones.BuscarPorNumero(reserva.NumeroHabitacion)
                ?? throw new NoEncontradoException("La habitación actual no existe.");
            var nueva = _habitaciones.BuscarPorNumero(numeroHabitacionNueva)
                ?? throw new NoEncontradoException("La habitación nueva no existe.");

            var solicitud = new SolicitudReserva
            {
                CheckIn = reserva.FechaInicio,
                CheckOut = reserva.FechaFin,
                Huespedes = reserva.Huespedes,
                Habitacion = nueva,
                ReservasExistentes = _reservas.BuscarPorHabitacionYFechas(nueva, reserva.FechaInicio, reserva.FechaFin)
                    .Where(x => x.IdReserva != reserva.IdReserva).ToList(),
            };
            LanzarSiRechaza(ArmarCadena().Validar(solicitud));

            var esUpgrade = (nueva.Tipo?.PrecioBase ?? 0) > (anterior.Tipo?.PrecioBase ?? 0);
            if (esUpgrade && !confirmarUpgrade) throw new ConflictoDominioException(MensajesReserva.Upgrade);

            var libera = anterior.MarcarDisponible();
            if (!libera.EsExitosa) throw new ConflictoDominioException(libera.Motivo!);
            var reserva2 = nueva.Reservar();
            if (!reserva2.EsExitosa) throw new ConflictoDominioException(reserva2.Motivo!);

            reserva.Habitacion = nueva;
            reserva.NumeroHabitacion = nueva.NumeroHabitacion;
            _habitaciones.Guardar(anterior);
            _habitaciones.Guardar(nueva);
            _reservas.Guardar(reserva);
            Historial(reserva.IdReserva, usuario, "Habitación reasignada", $"Hab. {anterior.NumeroHabitacion}", $"Hab. {nueva.NumeroHabitacion}");
            return reserva;
        });
    }

    /// <summary>Check-in: Reserva Confirmada → EnCurso y Habitación Reservada → Ocupada [DE#4][DE#1][D-31].</summary>
    public Reserva HacerCheckIn(int idReserva, string? documento, string usuario)
    {
        var previa = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        return _motor.EjecutarBloqueado(new[] { previa.NumeroHabitacion }, () =>
        {
            var reserva = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
            var habitacion = _habitaciones.BuscarPorNumero(reserva.NumeroHabitacion)
                ?? throw new NoEncontradoException("La habitación no existe.");

            var guarda = reserva.HacerCheckIn(documento, _reloj.Ahora);
            if (!guarda.EsExitosa) throw new ConflictoDominioException(guarda.Motivo!);
            var r = habitacion.HacerCheckIn();
            if (!r.EsExitosa)
            {
                reserva.Estado = EstadoReserva.Confirmada;
                throw new ConflictoDominioException(r.Motivo!);
            }
            _habitaciones.Guardar(habitacion);
            _reservas.Guardar(reserva);
            Historial(reserva.IdReserva, usuario, "Check-in", EstadoReserva.Confirmada.ToString(), EstadoReserva.EnCurso.ToString());
            return reserva;
        });
    }
}
