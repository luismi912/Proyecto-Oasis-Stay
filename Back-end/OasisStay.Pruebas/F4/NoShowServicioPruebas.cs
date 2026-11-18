using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using OasisStay.Api;

namespace OasisStay.Pruebas.F4;

/// <summary>Arranque de la rutina de No-Show en segundo plano [DE#4 CP-R05][D-44].</summary>
public class NoShowServicioPruebas
{
    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "x";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Cfg(string? autoActivo = null)
        => new ConfigurationBuilder().AddInMemoryCollection(autoActivo == null
            ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["NoShow:AutoActivo"] = autoActivo }).Build();

    [Fact]
    public void NoShowServicio_ConSemillaDemo_NoSeEjecutaSolo()
        => Assert.False(NoShowServicio.DebeEjecutarse(Cfg(), new Entorno { EnvironmentName = "Development" }, new[] { "--semilla-demo", "--escenario", "US08" }));

    [Fact]
    public void NoShowServicio_PorDefectoYEnProduccion_SigueActivo()
    {
        Assert.True(NoShowServicio.DebeEjecutarse(Cfg(), new Entorno { EnvironmentName = "Development" }, Array.Empty<string>()));
        Assert.True(NoShowServicio.DebeEjecutarse(Cfg(), new Entorno { EnvironmentName = "Production" }, Array.Empty<string>()));
        // --semilla-demo solo vale en Development [D-39]
        Assert.True(NoShowServicio.DebeEjecutarse(Cfg(), new Entorno { EnvironmentName = "Production" }, new[] { "--semilla-demo" }));
    }

    [Fact]
    public void NoShowServicio_AutoActivoFalse_NoSeEjecuta()
        => Assert.False(NoShowServicio.DebeEjecutarse(Cfg("false"), new Entorno { EnvironmentName = "Production" }, Array.Empty<string>()));

    [Fact]
    public void NoShowServicio_EnPruebas_NoSeEjecuta()
        => Assert.False(NoShowServicio.DebeEjecutarse(Cfg(), new Entorno { EnvironmentName = "Testing" }, Array.Empty<string>()));
}
