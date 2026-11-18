using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Aplicacion;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F6;

/// <summary>API de reportes con rol Administrador y errores acordados [JIRA US-11][DS3][D-34][D-46]; datos del escenario US11 [D-41].</summary>
public class ApiReportesPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public ApiReportesPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private async Task<HttpClient> Admin()
    {
        await Escenario();
        return await Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin);
    }

    private async Task Escenario()
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db, "US11");
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private const string Oct = "desde=2026-10-01&hasta=2026-10-15";

    [Fact] // [JIRA US-11 ESC1]
    public async Task US11_ESC1_Api_ExportaExcel_01al15Oct2026()
    {
        var c = await Admin();
        var r = await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=xlsx");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", r.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".xlsx", r.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        using var libro = new XLWorkbook(new MemoryStream(await r.Content.ReadAsByteArrayAsync()));
        Assert.Equal(46, libro.Worksheet("Facturas").LastRowUsed()!.RowNumber());
    }

    [Fact] // [JIRA US-11 ESC2]
    public async Task US11_ESC2_Api_ExportaPdf()
    {
        var c = await Admin();
        var r = await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=pdf");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/pdf", r.Content.Headers.ContentType!.MediaType);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact] // [JIRA US-11 UX][JIRA US-11 RN2]
    public async Task US11_Api_Resumen_KpisYDesgloseCuadran()
    {
        var c = await Admin();
        var j = await Json(await c.GetAsync($"/api/reportes/resumen?{Oct}"));

        Assert.Equal(45, j.GetProperty("facturas").GetInt32());
        Assert.Equal(12_500_000m, j.GetProperty("ingresoTotal").GetDecimal());
        Assert.Equal(12_500_000m, j.GetProperty("ingresoBruto").GetDecimal());
        Assert.True(j.GetProperty("ingresoNeto").GetDecimal() < 12_500_000m);
        Assert.Equal(277777.78m, j.GetProperty("ticketPromedio").GetDecimal());
        Assert.True(j.GetProperty("ocupacionPorcentaje").GetDecimal() > 0);
        Assert.Equal(0m, j.GetProperty("cancelaciones").GetDecimal());
        Assert.Equal(12_500_000m, j.GetProperty("porMetodoPago").EnumerateArray().Sum(m => m.GetProperty("monto").GetDecimal()));
    }

    [Fact] // [JIRA US-11 ESC3]
    public async Task US11_ESC3_Api_SinMovimientos_Ano2027_ResumenEnCeroYExportar409()
    {
        var c = await Admin();
        var resumen = await c.GetAsync("/api/reportes/resumen?desde=2027-01-01&hasta=2027-01-15");
        Assert.Equal(HttpStatusCode.OK, resumen.StatusCode);
        Assert.Equal(0, (await Json(resumen)).GetProperty("facturas").GetInt32());

        var r = await c.GetAsync("/api/reportes/exportar?desde=2027-01-01&hasta=2027-01-15&formato=xlsx");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(MensajesReportes.SinMovimientos, (await Json(r)).GetProperty("detail").GetString());
    }

    [Fact] // [JIRA US-11 ESC3][JIRA US-11 RN3]
    public async Task US11_ESC3_Api_Rango400Dias_400()
    {
        var c = await Admin();
        foreach (var ruta in new[] { "resumen?desde=2026-01-01&hasta=2027-02-04", "exportar?desde=2026-01-01&hasta=2027-02-04&formato=pdf" })
        {
            var r = await c.GetAsync($"/api/reportes/{ruta}");
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Equal(MensajesReportes.RangoExcedido, (await Json(r)).GetProperty("detail").GetString());
        }
    }

    [Fact] // [DS3 alt]
    public async Task US11_Api_FormatoInvalido_400()
    {
        var c = await Admin();
        var r = await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=csv");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(MensajesReportes.FormatoInvalido, (await Json(r)).GetProperty("detail").GetString());
    }

    [Fact] // [JIRA US-11 alcance]
    public async Task US11_Api_FaltanFechas_400()
    {
        var c = await Admin();
        var r = await c.GetAsync("/api/reportes/resumen");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(MensajesReportes.FaltanFechas, (await Json(r)).GetProperty("detail").GetString());
    }

    [Fact] // [JIRA US-11 RN1][D-34]
    public async Task US11_Roles_AmaDeLlaves_403()
    {
        await Escenario();
        var c = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/reportes/resumen?{Oct}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=xlsx")).StatusCode);
    }

    [Fact] // [JIRA US-11 RN1][D-34]
    public async Task US11_Roles_Recepcionista_403()
    {
        await Escenario();
        var c = await Cliente("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/reportes/resumen?{Oct}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=pdf")).StatusCode);
    }

    [Fact] // [JIRA US-11 NFR][RNF-06]
    public async Task US11_SinToken_401()
    {
        var r = await _fabrica.CreateClient().GetAsync($"/api/reportes/resumen?{Oct}");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] // [DS3][D-46]
    public async Task US11_Api_ExportarRegistraAuditoria()
    {
        var c = await Admin();
        using (var scope = _fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
            db.AuditoriasReporte.RemoveRange(db.AuditoriasReporte);
            await db.SaveChangesAsync();
        }
        await c.GetAsync($"/api/reportes/exportar?{Oct}&formato=pdf");
        using var scope2 = _fabrica.Services.CreateScope();
        var fila = scope2.ServiceProvider.GetRequiredService<OasisStayDbContext>().AuditoriasReporte.Single();
        Assert.Equal(("pdf", new DateTime(2026, 10, 1), new DateTime(2026, 10, 15)), (fila.Formato, fila.Desde, fila.Hasta));
        Assert.False(string.IsNullOrWhiteSpace(fila.Usuario)); // nombre del Administrador autenticado
    }
}
