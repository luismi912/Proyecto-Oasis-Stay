using System.Globalization;

namespace OasisStay.Dominio;

/// <summary>Valores fijos de facturación [CTR §1][RN-03][RN-04].</summary>
public static class ReglasFacturacion
{
    /// <summary>Recargo fijo de late check-out en COP [E1 RN-04][D-12].</summary>
    public const decimal RecargoLateCheckout = 40000m;
    /// <summary>IVA 19 % [E1 RN-03]: el IVA multiplica por 1.19.</summary>
    public const decimal FactorIva = 1.19m;
    public const string ConceptoEstadia = "Estadía";
    /// <summary>Servicio semilla de consumo [D-29].</summary>
    public const string ConceptoMinibar = "Minibar";
    public const string ConceptoLateCheckout = "Late check-out";

    /// <summary>Dos decimales y nunca negativo [JIRA US-05 RN3][D-24].</summary>
    public static decimal Redondear(decimal valor) => Math.Round(Math.Max(0m, valor), 2, MidpointRounding.AwayFromZero);
}

/// <summary>Mensajes acordados de facturación y pagos [CTR §4][D-32][D-33].</summary>
public static class MensajesFacturacion
{
    public const string MontoInsuficiente = "Monto insuficiente";
    public const string FaltaTarjeta = "Falta tipo de tarjeta o autorización";
    public const string FaltaReferencia = "Falta la referencia bancaria";
    public const string ReservaNoEnCurso = "La reserva no está en curso";
    public const string YaFacturada = "La reserva ya tiene una factura emitida";
    public const string MetodoInvalido = "El método de pago no es válido";
    public const string MontoInvalido = "El monto debe ser mayor que cero";
    public const string ConsumoInvalido = "Indique el servicio, la cantidad y el monto del consumo";
    public const string ServicioNoExiste = "El servicio no existe en el catálogo de consumos";
    public const string SalidaTardiaRegistrada = "La salida tardía ya está registrada";

    public static string Dinero(decimal monto)
        => "$" + monto.ToString(monto == decimal.Truncate(monto) ? "N0" : "N2", CultureInfo.GetCultureInfo("es-CO"));

    /// <summary>Texto de saldo pendiente [D-32][JIRA US-06 ESC2].</summary>
    public static string SaldoPendiente(decimal monto)
        => $"La reserva tiene saldo pendiente de {Dinero(monto)}: registre el pago antes del check-out";

    public static string MontoExcedeSaldo(decimal saldo)
        => $"El monto excede el saldo pendiente de {Dinero(saldo)}";
}

/// <summary>
/// Consumo guardado de una reserva (minibar, recargo late check-out); la cadena del Decorator se reconstruye desde estos
/// registros [CTR §4 Decorator][actividad-4 "Añadir consumos extra"][D-29][D-43].
/// </summary>
public class ConsumoReserva
{
    public int IdConsumo { get; set; }
    public int IdReserva { get; set; }
    /// <summary>Servicio del catálogo; nulo en el recargo de late check-out [D-29].</summary>
    public int? IdServicio { get; set; }
    public string Concepto { get; set; } = "";
    public int Cantidad { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public DateTime Fecha { get; set; }

    public decimal SubTotal => ReglasFacturacion.Redondear(Cantidad * ValorUnitario);
    public bool EsLateCheckout => Concepto == ReglasFacturacion.ConceptoLateCheckout;
}
