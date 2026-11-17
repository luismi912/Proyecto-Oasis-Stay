namespace OasisStay.Dominio;

/// <summary>Mensajes acordados del State [GoF State][D-32][CTR §3].</summary>
public static class EstadosHabitacion
{
    public const string EnLimpieza = "La habitación está en limpieza";
    public const string OcupadaRequiereCheckOut = "La habitación está ocupada: requiere check-out previo";
    public const string MotivoMantenimiento = "Debe ingresar el motivo del mantenimiento (mínimo 10 caracteres)";
    public const int LongitudMinimaMotivo = 10;

    public static string NoPermitida(string estado) => $"La operación no está permitida en estado {estado}";

    /// <summary>Motivo válido: al menos 10 caracteres [D-24][JIRA US-02 RN2].</summary>
    public static bool MotivoValido(string? motivo)
        => !string.IsNullOrWhiteSpace(motivo) && motivo.Trim().Length >= LongitudMinimaMotivo;
}

/// <summary>Disponible [DE#1][GoF State].</summary>
public class EstadoDisponible : IEstadoHabitacion
{
    public string Nombre => "Disponible";

    public ResultadoOperacion Reservar(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.RESERVADA);
        return ResultadoOperacion.Exitosa();
    }

    public ResultadoOperacion HacerCheckIn(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion HacerCheckOut(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion MarcarDisponible(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    /// <summary>Disponible a Mantenimiento con motivo de al menos 10 caracteres [DE#1 CP-H07][D-24][D-28].</summary>
    public ResultadoOperacion MarcarMantenimiento(Habitacion h, string? motivo)
    {
        if (!EstadosHabitacion.MotivoValido(motivo))
            return ResultadoOperacion.Rechazada(EstadosHabitacion.MotivoMantenimiento);
        h.MotivoMantenimiento = motivo!.Trim();
        h.CambiarEstado(EstadoHabitacion.MANTENIMIENTO);
        return ResultadoOperacion.Exitosa();
    }
}

/// <summary>Reservada [DE#1][GoF State].</summary>
public class EstadoReservada : IEstadoHabitacion
{
    public string Nombre => "Reservada";
    public ResultadoOperacion Reservar(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    public ResultadoOperacion HacerCheckIn(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.OCUPADO);
        return ResultadoOperacion.Exitosa();
    }

    public ResultadoOperacion HacerCheckOut(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    /// <summary>Cancelación, No-show o reasignación [D-21].</summary>
    public ResultadoOperacion MarcarDisponible(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.DISPONIBLE);
        return ResultadoOperacion.Exitosa();
    }
}

/// <summary>Ocupada [DE#1][GoF State].</summary>
public class EstadoOcupada : IEstadoHabitacion
{
    public string Nombre => "Ocupada";
    public ResultadoOperacion Reservar(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion HacerCheckIn(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    public ResultadoOperacion HacerCheckOut(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.EN_LIMPIEZA);
        return ResultadoOperacion.Exitosa();
    }

    /// <summary>[JIRA US-02 RN3][DE#1 §7 Esc. 3][D-32]</summary>
    public ResultadoOperacion MarcarDisponible(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.OcupadaRequiereCheckOut);
}

/// <summary>En limpieza [DE#1][GoF State].</summary>
public class EstadoEnLimpieza : IEstadoHabitacion
{
    public string Nombre => "EnLimpieza";

    /// <summary>[GoF State][CTR §3]</summary>
    public ResultadoOperacion Reservar(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.EnLimpieza);
    public ResultadoOperacion HacerCheckIn(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion HacerCheckOut(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    public ResultadoOperacion MarcarDisponible(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.DISPONIBLE);
        return ResultadoOperacion.Exitosa();
    }
}

/// <summary>Mantenimiento ("Fuera de Servicio") [DE#1][D-18][GoF State].</summary>
public class EstadoMantenimiento : IEstadoHabitacion
{
    public string Nombre => "Mantenimiento";
    public ResultadoOperacion Reservar(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion HacerCheckIn(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));
    public ResultadoOperacion HacerCheckOut(Habitacion h) => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(Nombre));

    public ResultadoOperacion MarcarDisponible(Habitacion h)
    {
        h.CambiarEstado(EstadoHabitacion.DISPONIBLE);
        return ResultadoOperacion.Exitosa();
    }
}
