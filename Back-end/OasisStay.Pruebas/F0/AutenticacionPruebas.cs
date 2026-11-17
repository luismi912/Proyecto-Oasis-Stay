using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OasisStay.Api.Contratos;

namespace OasisStay.Pruebas.F0;

public class AutenticacionPruebas : IClassFixture<FabricaApi>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApi _fabrica;
    public AutenticacionPruebas(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<LoginRespuesta> Login(string usuario, string clave)
    {
        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
    }

    [Fact] // [D-02][RNF-06]
    public async Task F0_Login_Administrador_Ok()
    {
        var r = await Login("admin@oasisstay.co", FabricaApi.ClaveAdmin);
        Assert.Equal("Administrador", r.Rol);
        Assert.False(string.IsNullOrWhiteSpace(r.Token));
        Assert.True(r.ExpiraEn > DateTime.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(r.Nombre));
    }

    [Fact]
    public async Task F0_Login_Recepcionista_Ok()
    {
        var r = await Login("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
        Assert.Equal("Recepcionista", r.Rol);
        Assert.False(string.IsNullOrWhiteSpace(r.Token));
    }

    [Fact]
    public async Task F0_Login_AmaDeLlaves_Ok()
    {
        var r = await Login("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal("AmaDeLlaves", r.Rol);
        Assert.False(string.IsNullOrWhiteSpace(r.Token));
    }

    [Fact] // [Front][D-02]
    public async Task F0_Login_CredencialesInvalidas_401()
    {
        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginSolicitud { Usuario = "admin@oasisstay.co", Contrasena = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal("Las credenciales no son válidas.", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact] // [RNF-06]
    public async Task F0_EndpointProtegido_SinToken_401()
    {
        var r = await _fabrica.CreateClient().GetAsync("/api/auth/yo");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] // [RNF-06]
    public async Task F0_EndpointProtegido_RolIncorrecto_403()
    {
        var t = await Login("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
        var c = _fabrica.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/prueba/solo-administrador")).StatusCode);

        var a = await Login("admin@oasisstay.co", FabricaApi.ClaveAdmin);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/prueba/solo-administrador")).StatusCode);
    }

    [Fact] // [D-02] GET /api/auth/yo
    public async Task F0_Yo_DevuelveNombreYRol()
    {
        var t = await Login("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        var c = _fabrica.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        var yo = await c.GetFromJsonAsync<YoRespuesta>("/api/auth/yo", Web);
        Assert.Equal("AmaDeLlaves", yo!.Rol);
        Assert.Equal(t.Nombre, yo.Nombre);
    }
}
