using System.Text.RegularExpressions;

namespace OasisStay.Dominio;

/// <summary>Mensajes acordados de la reserva [CTR §4][D-19][D-32].</summary>
public static class MensajesReserva
{
    public const string FechasInvalidas = "La fecha de salida debe ser posterior a la fecha de llegada";
    public const string CapacidadExcedida = "Excede la capacidad de la habitación (RN-05)";
    public const string HabitacionNoDisponible = "La habitación no está disponible para reservar";
    public const string Solapamiento = "Las fechas se solapan con otra reserva (RN-01)";
    public const string Upgrade = "La habitación nueva es de categoría superior (Upgrade). Confirme para continuar.";
    public const string DocumentoNoVerificado = "Debe verificar el documento del huésped";
    public const string CheckInAntesDeLlegada = "El check-in no puede hacerse antes de la fecha de llegada";
    public const string ReservaNoConfirmada = "La reserva no está confirmada";
}

/// <summary>Resultado de un validador de la cadena [GoF Chain][CTR §4].</summary>
public sealed class ResultadoValidacion
{
    public bool EsValido { get; }
    public string? Mensaje { get; }
    private ResultadoValidacion(bool ok, string? mensaje) { EsValido = ok; Mensaje = mensaje; }
    public static ResultadoValidacion Ok() => new(true, null);
    public static ResultadoValidacion Error(string mensaje) => new(false, mensaje);
}

/// <summary>Eslabón de la cadena de validación; corta en el primer error [GoF Chain][CTR §4].</summary>
public abstract class ValidadorReserva
{
    private ValidadorReserva? _siguiente;

    /// <summary>Enlaza el siguiente validador y lo devuelve para encadenar [GoF Chain].</summary>
    public ValidadorReserva Siguiente(ValidadorReserva siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public virtual ResultadoValidacion Validar(SolicitudReserva solicitud)
        => _siguiente?.Validar(solicitud) ?? ResultadoValidacion.Ok();
}

/// <summary>Salida estrictamente posterior a la llegada; sin 0 noches ni Day Use [D-19][D-22][JIRA US-01 Esc3].</summary>
public class ValidadorFechas : ValidadorReserva
{
    public override ResultadoValidacion Validar(SolicitudReserva solicitud)
        => solicitud.CheckOut <= solicitud.CheckIn
            ? ResultadoValidacion.Error(MensajesReserva.FechasInvalidas)
            : base.Validar(solicitud);
}

/// <summary>[E1 RN-05][JIRA US-04 RN1]</summary>
public class ValidadorCapacidad : ValidadorReserva
{
    public override ResultadoValidacion Validar(SolicitudReserva solicitud)
        => solicitud.Habitacion != null && solicitud.Huespedes > (solicitud.Habitacion.Tipo?.CapacidadMax ?? 0)
            ? ResultadoValidacion.Error(MensajesReserva.CapacidadExcedida)
            : base.Validar(solicitud);
}

/// <summary>Solo se reserva una habitación Disponible [CTR §3][JIRA US-04 RN1].</summary>
public class ValidadorEstadoHabitacion : ValidadorReserva
{
    public override ResultadoValidacion Validar(SolicitudReserva solicitud)
        => solicitud.Habitacion == null || !solicitud.Habitacion.PuedeReservarse()
            ? ResultadoValidacion.Error(MensajesReserva.HabitacionNoDisponible)
            : base.Validar(solicitud);
}

/// <summary>[E1 RN-01][JIRA US-04 RN2]</summary>
public class ValidadorSolapamiento : ValidadorReserva
{
    public override ResultadoValidacion Validar(SolicitudReserva solicitud)
        => solicitud.TieneReservaSolapada()
            ? ResultadoValidacion.Error(MensajesReserva.Solapamiento)
            : base.Validar(solicitud);
}

/// <summary>Formato de teléfono y correo del huésped [JIRA US-03 RN2][JIRA US-03 ESC3][D-32].</summary>
public static class ReglasHuesped
{
    private static readonly Regex Telefono = new(@"^[0-9 +\-]+$", RegexOptions.Compiled);
    private static readonly Regex Correo = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    public static bool TelefonoValido(string t) => Telefono.IsMatch(t.Trim()) && t.Any(char.IsDigit);
    public static bool CorreoValido(string c) => Correo.IsMatch(c.Trim());
}

/// <summary>Campos inválidos o regla de validación incumplida (HTTP 400) [dotnet-api].</summary>
public class ValidacionDominioException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errores { get; }
    public ValidacionDominioException(string mensaje, IDictionary<string, string[]>? errores = null) : base(mensaje)
        => Errores = new Dictionary<string, string[]>(errores ?? new Dictionary<string, string[]>());
}

/// <summary>Conflicto de estado, solapamiento o concurrencia (HTTP 409) [dotnet-api].</summary>
public class ConflictoDominioException : Exception
{
    public ConflictoDominioException(string mensaje) : base(mensaje) { }
}

/// <summary>El recurso no existe (HTTP 404) [dotnet-api].</summary>
public class NoEncontradoException : Exception
{
    public NoEncontradoException(string mensaje) : base(mensaje) { }
}
