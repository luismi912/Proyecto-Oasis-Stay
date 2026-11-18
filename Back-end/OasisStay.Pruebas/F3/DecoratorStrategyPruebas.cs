using OasisStay.Dominio;

namespace OasisStay.Pruebas.F3;

/// <summary>Decorator de la cuenta [GoF Decorator][CTR §4][JIRA US-05].</summary>
public class DecoratorPruebas
{
    [Fact] // [GoF Decorator][JIRA US-05 ESC1]
    public void Decorator_EstadiaBase_3Noches150000_450000()
        => Assert.Equal(450000m, new EstadiaBase(3, 150000m).CalcularCosto());

    [Fact] // [GoF Decorator][E1 RN-03]: el IVA se aplica sobre el subtotal acumulado, incluidos consumos y recargo
    public void Decorator_IvaSiempreCapaExterna()
    {
        ICuenta cuenta = new EstadiaBase(1, 200000m);
        cuenta = new CargoMinibar(cuenta, 30000m);
        cuenta = new CargoLateCheckout(cuenta);
        Assert.Equal(270000m, cuenta.CalcularCosto());
        Assert.Equal(321300m, new ImpuestoIVA(cuenta).CalcularCosto());

        var consumos = new[]
        {
            new ConsumoReserva { Concepto = ReglasFacturacion.ConceptoMinibar, Cantidad = 1, ValorUnitario = 30000m },
            new ConsumoReserva { Concepto = ReglasFacturacion.ConceptoLateCheckout, Cantidad = 1, ValorUnitario = 40000m },
        };
        var armada = ArmadorCuenta.Armar(1, 200000m, consumos, true);
        Assert.IsType<ImpuestoIVA>(armada);
        Assert.Equal(321300m, armada.CalcularCosto());
        Assert.Equal(270000m, ArmadorCuenta.Armar(1, 200000m, consumos, false).CalcularCosto());
    }

    [Fact] // [JIRA US-05 RN3][D-24]
    public void Decorator_RedondeoDosDecimales_SinNegativos()
    {
        Assert.Equal(100.01m, new EstadiaBase(1, 100.005m).CalcularCosto());
        Assert.Equal(119.0m, new ImpuestoIVA(new EstadiaBase(1, 100m)).CalcularCosto());
        Assert.Equal(2.38m, new ImpuestoIVA(new EstadiaBase(1, 2m)).CalcularCosto());
        Assert.Equal(0m, new CargoMinibar(new EstadiaBase(1, 100m), -500m).CalcularCosto());
        Assert.Equal(0m, new ImpuestoIVA(new CargoMinibar(new EstadiaBase(1, 100m), -500m)).CalcularCosto());
    }
}

/// <summary>Estrategias de pago [GoF Strategy][CTR §4][JIRA US-10].</summary>
public class StrategyPruebas
{
    [Fact] // [JIRA US-10 caso 1 Efectivo]: deuda 120000, recibido 150000 -> Aprobado, cambio 30000
    public void Strategy_Efectivo_120000Recibido150000_Aprobado_Vuelto30000()
    {
        var r = new PagoEfectivo().Procesar(120000m, new DatosPago { MontoRecibido = 150000m });
        Assert.True(r.EsAprobado);
        Assert.Equal(30000m, r.Vuelto);
    }

    [Fact] // [JIRA US-10 ESC3][GoF Strategy]
    public void Strategy_Efectivo_MontoInsuficiente()
    {
        var r = new PagoEfectivo().Procesar(150000m, new DatosPago { MontoRecibido = 100000m });
        Assert.False(r.EsAprobado);
        Assert.Equal("Monto insuficiente", r.Motivo);
    }

    [Fact] // [GoF Strategy][JIRA US-10 RN2]
    public void Strategy_Tarjeta_SinTipoOAutorizacion_Rechazado()
    {
        var s = new PagoTarjeta();
        Assert.Equal("Falta tipo de tarjeta o autorización", s.Procesar(1000m, new DatosPago()).Motivo);
        Assert.Equal("Falta tipo de tarjeta o autorización", s.Procesar(1000m, new DatosPago { TipoTarjeta = "Crédito" }).Motivo);
        Assert.Equal("Falta tipo de tarjeta o autorización", s.Procesar(1000m, new DatosPago { NumeroAutorizacion = "4892" }).Motivo);
        Assert.True(s.Procesar(1000m, new DatosPago { TipoTarjeta = "Crédito", NumeroAutorizacion = "4892" }).EsAprobado);
    }

    [Fact] // [GoF Strategy][JIRA US-10 RN2]: TARJETA_DEBITO usa la estrategia "Tarjeta"
    public void Strategy_Tarjeta_TarjetaDebitoSinAutorizacion()
    {
        var r = new PagoTarjeta().Procesar(50000m, new DatosPago { TipoTarjeta = "Débito", NumeroAutorizacion = "  " });
        Assert.False(r.EsAprobado);
        Assert.Equal("Falta tipo de tarjeta o autorización", r.Motivo);
    }

    [Fact] // [GoF Strategy][JIRA US-10 ESC3]
    public void Strategy_Transferencia_SinReferencia_Rechazado()
    {
        var s = new PagoTransferencia();
        var r = s.Procesar(70000m, new DatosPago());
        Assert.False(r.EsAprobado);
        Assert.Equal("Falta la referencia bancaria", r.Motivo);
        Assert.True(s.Procesar(70000m, new DatosPago { ReferenciaBancaria = "TRF-9921" }).EsAprobado);
    }

    [Fact] // [GoF Strategy]
    public void ProcesadorPagos_Cobrar_ElijeEstrategiaPorMetodo()
    {
        var p = new ProcesadorPagos(new IEstrategiaPago[] { new PagoEfectivo(), new PagoTarjeta(), new PagoTransferencia() });
        Assert.True(p.Cobrar("Efectivo", 100m, new DatosPago { MontoRecibido = 100m }).EsAprobado);
        Assert.Equal("Falta tipo de tarjeta o autorización", p.Cobrar("Tarjeta", 100m, new DatosPago()).Motivo);
        Assert.Equal("Falta la referencia bancaria", p.Cobrar("Transferencia", 100m, new DatosPago()).Motivo);
        Assert.ThrowsAny<InvalidOperationException>(() => p.Cobrar("Cheque", 100m, new DatosPago()));
    }
}
