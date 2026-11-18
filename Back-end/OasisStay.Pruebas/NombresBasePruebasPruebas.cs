namespace OasisStay.Pruebas;

public class NombresBasePruebasPruebas
{
    [Fact]
    public void Pruebas_BasesUnicasPorEjecucion()
    {
        var a = NombresBasePruebas.Nombre("F2", NombresBasePruebas.NuevoSufijoDeEjecucion());
        var b = NombresBasePruebas.Nombre("F2", NombresBasePruebas.NuevoSufijoDeEjecucion());
        Assert.NotEqual(a, b);
        Assert.StartsWith("OasisStay_Pruebas", a);
        Assert.StartsWith("OasisStay_Pruebas", NombresBasePruebas.Nombre());
        Assert.Contains(NombresBasePruebas.Nombre("F3"), NombresBasePruebas.Cadena("F3"));
    }
}
