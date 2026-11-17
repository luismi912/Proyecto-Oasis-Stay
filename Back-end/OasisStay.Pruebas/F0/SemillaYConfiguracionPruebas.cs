using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Identidad;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;

namespace OasisStay.Pruebas.F0;

public class SemillaYConfiguracionPruebas : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public SemillaYConfiguracionPruebas(FabricaApi fabrica) => _fabrica = fabrica;

    [Fact] // [D-29]
    public async Task F0_Semilla_CreaTiposHabitacionesYUsuarios()
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        var tipos = await db.TiposHabitacion.OrderBy(t => t.PrecioBase).ToListAsync();
        Assert.Equal(3, tipos.Count);
        Assert.Equal(("Individual", 1, 120000m), (tipos[0].Nombre, tipos[0].CapacidadMax, tipos[0].PrecioBase));
        Assert.Equal(("Doble Clásica", 2, 185000m), (tipos[1].Nombre, tipos[1].CapacidadMax, tipos[1].PrecioBase));
        Assert.Equal(("Suite Ejecutiva", 4, 320000m), (tipos[2].Nombre, tipos[2].CapacidadMax, tipos[2].PrecioBase));

        var habitaciones = await db.Habitaciones.ToListAsync();
        Assert.Equal(new[] { 101, 102, 103, 104, 201, 202, 203, 204, 301, 302, 303, 304 },
            habitaciones.Select(h => h.NumeroHabitacion).OrderBy(n => n).ToArray());
        Assert.All(habitaciones, h => Assert.Equal(h.NumeroHabitacion / 100, h.Piso));
        Assert.Contains(await db.Servicios.ToListAsync(), s => s.Nombre == "Minibar");

        var um = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioOasis>>();
        foreach (var (correo, rol) in new[] { ("admin@oasisstay.co", "Administrador"), ("recepcion@oasisstay.co", "Recepcionista"), ("amadellaves@oasisstay.co", "AmaDeLlaves") })
        {
            var u = await um.FindByEmailAsync(correo);
            Assert.NotNull(u);
            Assert.True(await um.IsInRoleAsync(u!, rol));
        }
    }

    [Fact] // [D-01][D-12][JIRA US-08 RN1]
    public void F0_Configuracion_Politicas()
    {
        var p = _fabrica.Services.GetRequiredService<IOptions<PoliticasOpciones>>().Value;
        Assert.Equal(24, p.PlazoCancelacionHoras);
        Assert.Equal(new TimeSpan(12, 0, 0), p.HoraMaximaCheckout);
        Assert.Equal(new TimeSpan(15, 0, 0), p.HoraCheckIn);
    }

    private class SemillaDePrueba : ISemillaDemo
    {
        public int Llamadas;
        public async Task SembrarAsync(OasisStayDbContext db)
        {
            Llamadas++;
            if (!await db.Servicios.AnyAsync(s => s.Nombre == "ServicioDemo"))
            {
                db.Servicios.Add(new Servicio { Nombre = "ServicioDemo", Precio = 1000m, Descripcion = "demo" });
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact] // [D-39]
    public async Task F0_SemillaDemo_Idempotente()
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        var semilla = new SemillaDePrueba();
        var ejecutor = new EjecutorSemillaDemo(new ISemillaDemo[] { semilla });
        await ejecutor.EjecutarAsync(db);
        await ejecutor.EjecutarAsync(db);
        Assert.Equal(2, semilla.Llamadas);
        Assert.Equal(1, await db.Servicios.CountAsync(s => s.Nombre == "ServicioDemo"));
    }
}
