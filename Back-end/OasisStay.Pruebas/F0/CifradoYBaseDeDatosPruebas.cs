using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;

namespace OasisStay.Pruebas.F0;

/// <summary>Pruebas contra SQL Server local, base aparte OasisStay_Pruebas (nunca OasisStay) [D-06].</summary>
[Collection("SqlServer")]
public class CifradoYBaseDeDatosPruebas
{
    private static readonly string Cadena = NombresBasePruebas.Cadena();
    private static readonly byte[] Clave = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private static OasisStayDbContext Contexto() =>
        new(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(Cadena).Options, new CifradorAes256(Clave));

    private static async Task<OasisStayDbContext> BdNueva()
    {
        var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    private static Cliente ClienteEjemplo() => new()
    {
        Nombre = "Camila Ríos", TipoDocumento = TipoDocumento.CC, Documento = "1020304050",
        Telefono = "3001234567", Correo = "camila@correo.co", Edad = 30
    };

    [Fact] // [D-03][RNF-04]
    public async Task F0_Aes256_ClienteCifradoEnBD_YDescifradoAlLeer()
    {
        await using var db = await BdNueva();
        db.Clientes.Add(ClienteEjemplo());
        await db.SaveChangesAsync();

        await using var cn = new SqlConnection(Cadena);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Documento, Telefono, Correo FROM Clientes", cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        Assert.True(await rd.ReadAsync());
        Assert.NotEqual("1020304050", rd.GetString(0));
        Assert.NotEqual("3001234567", rd.GetString(1));
        Assert.NotEqual("camila@correo.co", rd.GetString(2));
        await rd.CloseAsync();

        await using var db2 = Contexto();
        var c = await db2.Clientes.SingleAsync();
        Assert.Equal("1020304050", c.Documento);
        Assert.Equal("3001234567", c.Telefono);
        Assert.Equal("camila@correo.co", c.Correo);
    }

    [Fact] // [D-03][JIRA US-03 ESC2]
    public async Task F0_Aes_BusquedaPorDocumentoFunciona()
    {
        await using var db = await BdNueva();
        db.Clientes.Add(ClienteEjemplo());
        await db.SaveChangesAsync();

        await using var db2 = Contexto();
        var repo = new RepositorioHuespedes(db2);
        var c = repo.BuscarPorDocumento("1020304050");
        Assert.NotNull(c);
        Assert.Equal("Camila Ríos", c!.Nombre);
        Assert.Null(repo.BuscarPorDocumento("999"));
    }

    [Fact] // [D-06]
    public async Task F0_Migracion_CreaEsquema()
    {
        await using var db = await BdNueva();
        await using var cn = new SqlConnection(Cadena);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT name FROM sys.tables", cn);
        var tablas = new List<string>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) tablas.Add(rd.GetString(0));
        foreach (var t in new[] { "Clientes", "Reservas", "TiposHabitacion", "Habitaciones", "Servicios", "Facturas",
                     "DetallesFactura", "Pagos", "HistorialReservas", "Notificaciones", "AspNetUsers", "AspNetRoles" })
            Assert.Contains(t, tablas);
    }

    [Fact]
    public void F0_Aes256_Deterministico_ViajeDeIdaYVuelta()
    {
        var c = new CifradorAes256(Clave);
        var a = c.Cifrar("1020304050");
        Assert.Equal(a, c.Cifrar("1020304050"));
        Assert.NotEqual(a, c.Cifrar("1020304051"));
        Assert.Equal("1020304050", c.Descifrar(a));
    }
}

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public class SqlServerCollection : ICollectionFixture<LimpiezaBasesDeEjecucion> { }

/// <summary>Al terminar la colección borra todas las bases de esta ejecución (también si alguna prueba falló).</summary>
public sealed class LimpiezaBasesDeEjecucion : IDisposable
{
    public void Dispose()
    {
        try
        {
            SqlConnection.ClearAllPools();
            using var cn = new SqlConnection("Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
            cn.Open();
            var nombres = new List<string>();
            using (var q = new SqlCommand("SELECT name FROM sys.databases WHERE name LIKE @p ESCAPE '\\'", cn))
            {
                q.Parameters.AddWithValue("@p", NombresBasePruebas.Prefijo.Replace("_", "\\_") + "%\\_" + NombresBasePruebas.SufijoActual);
                using var rd = q.ExecuteReader();
                while (rd.Read()) nombres.Add(rd.GetString(0));
            }
            foreach (var n in nombres)
            {
                using var d = new SqlCommand($"ALTER DATABASE [{n}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{n}]", cn);
                d.ExecuteNonQuery();
            }
        }
        catch { /* limpieza de mejor esfuerzo */ }
    }
}
