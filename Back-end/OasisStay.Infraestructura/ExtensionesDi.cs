using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Aplicacion;
using OasisStay.Infraestructura.Identidad;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Infraestructura.Seguridad;

namespace OasisStay.Infraestructura;

public static class ExtensionesDi
{
    public static IServiceCollection AddInfraestructura(this IServiceCollection s, IConfiguration config)
    {
        s.AddSingleton<ICifradorDatos>(sp =>
        {
            var claveAes = sp.GetRequiredService<IConfiguration>()["Seguridad:ClaveAes"];
            if (string.IsNullOrWhiteSpace(claveAes))
                throw new InvalidOperationException("Falta Seguridad:ClaveAes (user-secrets o variable de entorno) [D-03].");
            return new CifradorAes256(Convert.FromBase64String(claveAes));
        });

        s.AddDbContext<OasisStayDbContext>((sp, o) => o
            .UseSqlServer(sp.GetRequiredService<IConfiguration>().GetConnectionString("OasisStay"))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        s.AddIdentityCore<UsuarioOasis>(o => { o.User.RequireUniqueEmail = true; })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<OasisStayDbContext>();

        s.AddSingleton<IReloj, RelojColombia>();
        s.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();
        s.AddScoped<INotificador, NotificadorSimulado>();
        s.AddScoped<IRepositorioHabitaciones, RepositorioHabitaciones>();
        s.AddScoped<IRepositorioReservas, RepositorioReservas>();
        s.AddScoped<IRepositorioHuespedes, RepositorioHuespedes>();
        s.AddScoped<IRepositorioConsumos, RepositorioConsumos>();
        s.AddScoped<IFacturaRepository, FacturaRepository>();
        s.AddScoped<IRepositorioPagos, RepositorioPagos>();
        s.AddScoped<IRepositorioHistorial, RepositorioHistorial>();
        s.AddScoped<IGestionReservas, GestionReservas>();
        // F1 [CTR §5][D-16][D-39]
        s.AddScoped<IRepositorioTiposHabitacion, RepositorioTiposHabitacion>();
        s.AddScoped<IGestionEstadoHabitacion, GestionEstadoHabitacion>();
        s.AddScoped<IConsultaDisponibilidad, GestionReservas>();
        s.AddScoped<ISemillaDemo, SemillaDemoF1>();
        s.AddScoped<EjecutorSemillaDemo>();
        return s;
    }
}
