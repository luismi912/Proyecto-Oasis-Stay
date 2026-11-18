namespace OasisStay.Dominio;

/// <summary>Cuenta de la estadía [GoF Decorator][CTR §4].</summary>
public interface ICuenta { decimal CalcularCosto(); }

/// <summary>noches × tarifa [CTR §4][JIRA US-05 RN1].</summary>
public class EstadiaBase : ICuenta
{
    private readonly int _noches;
    private readonly decimal _tarifa;
    public EstadiaBase(int noches, decimal tarifa) { _noches = noches; _tarifa = tarifa; }
    public decimal CalcularCosto() => ReglasFacturacion.Redondear(_noches * _tarifa);
}

/// <summary>Capa base de los cargos que envuelven una cuenta [GoF Decorator].</summary>
public abstract class CuentaDecorator : ICuenta
{
    protected readonly ICuenta Cuenta;
    protected CuentaDecorator(ICuenta cuenta) => Cuenta = cuenta;
    public abstract decimal CalcularCosto();
}

/// <summary>Suma el consumo de minibar [CTR §4][JIRA US-05 ESC2][D-29].</summary>
public class CargoMinibar : CuentaDecorator
{
    private readonly decimal _monto;
    public CargoMinibar(ICuenta cuenta, decimal monto) : base(cuenta) => _monto = monto;
    public override decimal CalcularCosto() => ReglasFacturacion.Redondear(Cuenta.CalcularCosto() + _monto);
}

/// <summary>Recargo fijo de 40000 COP [E1 RN-04][D-12].</summary>
public class CargoLateCheckout : CuentaDecorator
{
    public CargoLateCheckout(ICuenta cuenta) : base(cuenta) { }
    public override decimal CalcularCosto() => ReglasFacturacion.Redondear(Cuenta.CalcularCosto() + ReglasFacturacion.RecargoLateCheckout);
}

/// <summary>×1.19 sobre el subtotal acumulado, siempre la capa externa [E1 RN-03][JIRA US-05 RN2].</summary>
public class ImpuestoIVA : CuentaDecorator
{
    public ImpuestoIVA(ICuenta cuenta) : base(cuenta) { }
    public override decimal CalcularCosto() => ReglasFacturacion.Redondear(Cuenta.CalcularCosto() * ReglasFacturacion.FactorIva);
}

/// <summary>
/// Arma la cadena en un solo lugar, en el orden base → minibar → late check-out → IVA; no se persiste, se reconstruye
/// desde los consumos guardados [CTR §4 Decorator][E2 §6 SRP].
/// </summary>
public static class ArmadorCuenta
{
    public static ICuenta Armar(int noches, decimal tarifa, IEnumerable<ConsumoReserva> consumos, bool conIva)
    {
        var lista = consumos.ToList();
        ICuenta cuenta = new EstadiaBase(noches, tarifa);
        foreach (var c in lista.Where(c => !c.EsLateCheckout)) cuenta = new CargoMinibar(cuenta, c.SubTotal);
        if (lista.Any(c => c.EsLateCheckout)) cuenta = new CargoLateCheckout(cuenta);
        return conIva ? new ImpuestoIVA(cuenta) : cuenta;
    }
}
