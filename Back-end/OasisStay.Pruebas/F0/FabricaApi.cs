using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Pruebas.F0;

/// <summary>Host de pruebas de la API con BD en memoria y secretos ficticios (nunca reales).</summary>
public class FabricaApi : WebApplicationFactory<Program>
{
    public const string ClaveAdmin = "Admin#Prueba1";
    public const string ClaveRecepcion = "Recep#Prueba1";
    public const string ClaveAma = "Ama#Prueba1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Clave"] = "clave-jwt-de-prueba-0123456789-0123456789-abcdef",
            ["Seguridad:ClaveAes"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
            ["Semilla:Usuarios:Administrador:Contrasena"] = ClaveAdmin,
            ["Semilla:Usuarios:Recepcionista:Contrasena"] = ClaveRecepcion,
            ["Semilla:Usuarios:AmaDeLlaves:Contrasena"] = ClaveAma,
        }));
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType.IsGenericType &&
                         d.ServiceType.GenericTypeArguments.Contains(typeof(OasisStayDbContext))).ToList())
                services.Remove(d);
            var nombre = "OasisStay_" + Guid.NewGuid();
            services.AddDbContext<OasisStayDbContext>(o => o.UseInMemoryDatabase(nombre));
            services.AddControllers().AddApplicationPart(typeof(FabricaApi).Assembly);
        });
    }
}
