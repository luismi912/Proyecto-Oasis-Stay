extern alias Front;
using System.Reflection;
using OasisStay.Aplicacion;

namespace OasisStay.Pruebas.F6;

/// <summary>Conexión de las pantallas con los reportes y unificación de AuditoriaHistorial [JIRA US-11 UX/RN1][DS3][D-34][D-46].</summary>
public class PantallasReportesPruebas
{
    private static string Pantalla(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Front-end", "Pages", "Ventanas", nombre))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Front-end", "Pages", "Ventanas", nombre));
    }

    [Fact] // [DS3 AuditoriaHistorial]: una sola clase con ObtenerRegistros y GenerarReporte
    public void US11_AuditoriaHistorial_UnaSolaClase_ConsultaYGeneraReporte()
    {
        var clases = typeof(AuditoriaHistorial).Assembly.GetTypes().Where(t => t.Name == "AuditoriaHistorial").ToList();
        Assert.Single(clases);
        Assert.NotNull(typeof(AuditoriaHistorial).GetMethod("ObtenerRegistros"));
        Assert.NotNull(typeof(AuditoriaHistorial).GetMethod("GenerarReporte"));
        Assert.NotNull(typeof(IAuditoriaHistorial).GetMethod("GenerarReporte"));
    }

    [Fact] // [JIRA US-11 UX][actividad-3]
    public void US11_Historial_BotonExportar_AbreModalConKpisFormatoYFechas()
    {
        var html = Pantalla("Historial.cshtml");
        Assert.Matches("id=\"exportar-reporte\"(?:(?!>).)*data-modal-open=\"reporte-modal\"", html.Replace("\n", " "));
        Assert.DoesNotContain("disabled aria-disabled=\"true\"\n    title=\"Disponible con la exportación", html);
        Assert.Contains("id=\"reporte-modal\"", html);
        foreach (var t in new[] { "Ingreso Total", "Ticket Promedio", "Ocupación %", "Excel", "PDF", "id=\"reporte-desde\"", "id=\"reporte-hasta\"", "id=\"reporte-descargar\"" })
            Assert.Contains(t, html);
    }

    [Fact] // [DS3 alt][JIRA US-11 ESC3]
    public void US11_Historial_Modelo_TieneManejadoresDeResumenYExportar()
    {
        var modelo = typeof(Front::Front_end.Pages.IndexModel).Assembly.GetType("Front_end.Pages.HistorialModel")!;
        Assert.NotNull(modelo.GetMethod("OnGetResumenAsync"));
        Assert.NotNull(modelo.GetMethod("OnGetExportarAsync"));
        var api = typeof(Front::Front_end.Pages.IndexModel).Assembly.GetType("Front_end.Servicios.Api.ReportesApi")!;
        Assert.NotNull(api.GetMethod("ResumenAsync"));
        Assert.NotNull(api.GetMethod("ExportarAsync"));
    }

    [Fact] // [JIRA US-11 RN1][D-34]
    public void US11_Facturacion_BotonExportarReporte_SoloAdministrador()
    {
        var html = Pantalla("Facturacion.cshtml").Replace("\r", "");
        var i = html.IndexOf("Exportar reporte", StringComparison.Ordinal);
        Assert.True(i > 0);
        var previo = html[Math.Max(0, i - 200)..i];
        Assert.Contains("User.IsInRole(\"Administrador\")", previo);
    }
}
