using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Identidad;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>Datos semilla [D-29]: tipos, 12 habitaciones, servicio Minibar, roles y un usuario por rol.</summary>
public static class SemillaInicial
{
    public static async Task EjecutarAsync(IServiceProvider servicios)
    {
        var db = servicios.GetRequiredService<OasisStayDbContext>();
        var config = servicios.GetRequiredService<IConfiguration>();
        var roles = servicios.GetRequiredService<RoleManager<IdentityRole>>();
        var usuarios = servicios.GetRequiredService<UserManager<UsuarioOasis>>();

        foreach (var rol in Roles.Todos)
            if (!await roles.RoleExistsAsync(rol)) await roles.CreateAsync(new IdentityRole(rol));

        if (!await db.TiposHabitacion.AnyAsync())
        {
            db.TiposHabitacion.AddRange(
                new TipoHabitacion { Nombre = "Individual", CapacidadMax = 1, PrecioBase = 120000m, Descripcion = "Habitación individual" },
                new TipoHabitacion { Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m, Descripcion = "Habitación doble clásica" },
                new TipoHabitacion { Nombre = "Suite Ejecutiva", CapacidadMax = 4, PrecioBase = 320000m, Descripcion = "Suite ejecutiva" });
            await db.SaveChangesAsync();
        }

        if (!await db.Habitaciones.AnyAsync())
        {
            var t = await db.TiposHabitacion.ToDictionaryAsync(x => x.Nombre, x => x.IdTipo);
            const string S = "Suite Ejecutiva", D = "Doble Clásica", I = "Individual";
            var filas = new (int n, string tipo, EstadoHabitacion e)[]
            {
                (101, S, EstadoHabitacion.OCUPADO), (102, D, EstadoHabitacion.DISPONIBLE), (103, I, EstadoHabitacion.RESERVADA), (104, D, EstadoHabitacion.EN_LIMPIEZA),
                (201, S, EstadoHabitacion.DISPONIBLE), (202, D, EstadoHabitacion.OCUPADO), (203, I, EstadoHabitacion.DISPONIBLE), (204, D, EstadoHabitacion.RESERVADA),
                (301, S, EstadoHabitacion.RESERVADA), (302, D, EstadoHabitacion.OCUPADO), (303, I, EstadoHabitacion.EN_LIMPIEZA), (304, S, EstadoHabitacion.DISPONIBLE),
            };
            db.Habitaciones.AddRange(filas.Select(f => new Habitacion { NumeroHabitacion = f.n, Piso = f.n / 100, IdTipo = t[f.tipo], Estado = f.e }));
            await db.SaveChangesAsync();
        }

        if (!await db.Servicios.AnyAsync(s => s.Nombre == "Minibar"))
        {
            db.Servicios.Add(new Servicio { Nombre = "Minibar", Precio = 0m, Descripcion = "Consumo de minibar" });
            await db.SaveChangesAsync();
        }

        foreach (var rol in Roles.Todos)
        {
            var sec = config.GetSection($"Semilla:Usuarios:{rol}");
            var correo = sec["Correo"];
            var clave = sec["Contrasena"];
            if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave)) continue; // claves solo por user-secrets [D-29]
            if (await usuarios.FindByEmailAsync(correo) != null) continue;
            var u = new UsuarioOasis { UserName = correo, Email = correo, EmailConfirmed = true, Nombre = sec["Nombre"] ?? rol };
            var r = await usuarios.CreateAsync(u, clave);
            if (!r.Succeeded) throw new InvalidOperationException("No se pudo crear el usuario semilla: " + string.Join("; ", r.Errors.Select(e => e.Description)));
            await usuarios.AddToRoleAsync(u, rol);
        }
    }
}

/// <summary>Cada funcionalidad añade sus datos de escenario de Jira en su propio archivo, de forma idempotente [D-39].</summary>
public interface ISemillaDemo
{
    Task SembrarAsync(OasisStayDbContext db);
}

/// <summary>Ejecuta todas las ISemillaDemo registradas (argumento --semilla-demo, solo Development) [D-39].</summary>
public class EjecutorSemillaDemo
{
    private readonly IEnumerable<ISemillaDemo> _semillas;
    public EjecutorSemillaDemo(IEnumerable<ISemillaDemo> semillas) => _semillas = semillas;

    public async Task EjecutarAsync(OasisStayDbContext db)
    {
        foreach (var s in _semillas) await s.SembrarAsync(db);
    }
}
