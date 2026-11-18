using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F6;

/// <summary>El evento del check-out registra el usuario autenticado y, sin usuario, "sistema" [JIRA US-07 ESC2/RN3][D-47][D-23].</summary>
public class CheckoutUsuarioPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public CheckoutUsuarioPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<(HttpClient Cliente, string Nombre)> Entrar(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return (c, t.Nombre);
    }

    [Fact] // [D-47][JIRA US-07 ESC2]: check-out como Recepcionista -> el filtro por ese usuario incluye el evento
    public async Task US07_Checkout_RegistraUsuarioAutenticado()
    {
        var (rec, nombre) = await Entrar("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
        var (adm, _) = await Entrar("admin@oasisstay.co", FabricaApi.ClaveAdmin);
        var hoy = DateTime.Today;
        var crear = await rec.PostAsJsonAsync("/api/reservas", new ReservaSolicitud
        {
            TipoDocumento = "CC", Documento = "1029384756", Nombre = "Carlos Mendoza", Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
            FechaInicio = hoy, FechaFin = hoy.AddDays(1), Huespedes = 1, NumeroHabitacion = 304,
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Content.ReadFromJsonAsync<ReservaCreadaRespuesta>(Web))!.IdReserva;
        Assert.Equal(HttpStatusCode.OK, (await rec.PostAsJsonAsync($"/api/reservas/{id}/check-in", new { documento = "1029384756" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rec.PostAsJsonAsync($"/api/reservas/{id}/checkout",
            new { metodoPago = "EFECTIVO", datosPago = new { montoRecibido = 1000000m } })).StatusCode);

        var json = JsonDocument.Parse(await adm.GetStringAsync($"/api/historial?usuario={Uri.EscapeDataString(nombre)}&idReserva={id}")).RootElement;
        var eventos = json.EnumerateArray().ToList();
        var checkout = Assert.Single(eventos, e => e.GetProperty("accion").GetString() == "Check-out");
        Assert.Equal(nombre, checkout.GetProperty("usuario").GetString());
    }

    [Fact] // [D-47]: sin usuario autenticado (pruebas, servicios en segundo plano) sigue "sistema"
    public async Task US07_Checkout_SinUsuario_RegistraSistema()
    {
        var banco = new Banco();
        banco.Sembrar((101, "Doble Clásica", EstadoHabitacion.OCUPADO));
        await using var db = banco.Contexto();
        var cliente = new Cliente { TipoDocumento = TipoDocumento.CC, Documento = "1", Nombre = "X", Telefono = "3", Correo = "a@b.co" };
        db.Reservas.Add(new Reserva { Cliente = cliente, NumeroHabitacion = 101, FechaInicio = DateTime.Today, FechaFin = DateTime.Today.AddDays(1), Huespedes = 1, Estado = EstadoReserva.EnCurso });
        await db.SaveChangesAsync();
        var reserva = await db.Reservas.SingleAsync();

        new RepositorioHistorial(db).Archivar(reserva.IdReserva, new Factura { Folio = 7, FechaEmision = DateTime.Today });

        Assert.Equal("sistema", (await db.HistorialReservas.SingleAsync(h => h.AccionRealizada == "Check-out")).Usuario);
    }
}
