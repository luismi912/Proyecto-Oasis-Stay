using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Identidad;
using OasisStay.Infraestructura.Seguridad;

namespace OasisStay.Infraestructura.Persistencia;

/// <summary>Contexto EF Core de Oasis Stay [D-06]; Documento, Telefono y Correo se cifran con AES-256 [D-03].</summary>
public class OasisStayDbContext : IdentityDbContext<UsuarioOasis>
{
    private readonly ICifradorDatos _cifrador;

    public OasisStayDbContext(DbContextOptions<OasisStayDbContext> options, ICifradorDatos cifrador) : base(options)
        => _cifrador = cifrador;

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<TipoHabitacion> TiposHabitacion => Set<TipoHabitacion>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<DetalleFactura> DetallesFactura => Set<DetalleFactura>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<HistorialReserva> HistorialReservas => Set<HistorialReserva>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Cliente>().HasKey(x => x.IdCliente);
        b.Entity<Reserva>().HasKey(x => x.IdReserva);
        b.Entity<TipoHabitacion>().HasKey(x => x.IdTipo);
        b.Entity<Servicio>().HasKey(x => x.IdServicio);
        b.Entity<Factura>().HasKey(x => x.IdFactura);
        b.Entity<DetalleFactura>().HasKey(x => x.IdDetalle);
        b.Entity<Pago>().HasKey(x => x.IdPago);
        b.Entity<HistorialReserva>().HasKey(x => x.IdHistorial);
        b.Entity<Notificacion>().HasKey(x => x.IdNotificacion);

        var aes = new ValueConverter<string, string>(v => _cifrador.Cifrar(v), v => _cifrador.Descifrar(v));
        b.Entity<Cliente>(e =>
        {
            e.Property(c => c.TipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Nombre).HasMaxLength(150);
            e.Property(c => c.Documento).HasConversion(aes).HasMaxLength(450);
            e.Property(c => c.Telefono).HasConversion(aes).HasMaxLength(450);
            e.Property(c => c.Correo).HasConversion(aes).HasMaxLength(450);
            e.HasIndex(c => c.Documento);
        });

        b.Entity<TipoHabitacion>().Property(t => t.Nombre).HasMaxLength(100);

        b.Entity<Habitacion>(e =>
        {
            e.HasKey(h => h.NumeroHabitacion);
            e.Property(h => h.NumeroHabitacion).ValueGeneratedNever();
            e.Property(h => h.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(h => h.MotivoMantenimiento).HasMaxLength(500);
            e.Ignore(h => h.estadoActual);
            e.HasOne(h => h.Tipo).WithMany().HasForeignKey(h => h.IdTipo);
        });

        b.Entity<Reserva>(e =>
        {
            e.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasOne(r => r.Cliente).WithMany().HasForeignKey(r => r.IdCliente);
            e.HasOne(r => r.Habitacion).WithMany().HasForeignKey(r => r.NumeroHabitacion);
            e.HasMany(r => r.Servicios).WithMany(s => s.Reservas);
            e.HasMany(r => r.Pagos).WithOne().HasForeignKey(p => p.IdReserva);
        });

        b.HasSequence<int>("SeqFolioFactura");
        b.Entity<Factura>(e =>
        {
            e.Property(f => f.Folio).HasDefaultValueSql("NEXT VALUE FOR SeqFolioFactura");
            e.HasIndex(f => f.Folio).IsUnique();
            e.HasOne<Reserva>().WithMany().HasForeignKey(f => f.IdReserva);
            e.HasMany(f => f.Detalles).WithOne().HasForeignKey(d => d.IdFactura);
        });

        b.Entity<Pago>(e =>
        {
            e.Property(p => p.MetodoPago).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Factura>().WithMany().HasForeignKey(p => p.IdFactura);
        });

        b.Entity<HistorialReserva>(e =>
        {
            e.Property(h => h.AccionRealizada).HasMaxLength(200);
            e.Property(h => h.Usuario).HasMaxLength(256);
            e.HasIndex(h => h.IdReserva);
        });

        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(Cliente).Namespace)
                     .SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}

/// <summary>Fábrica solo para `dotnet ef` (clave ficticia; no cifra datos reales).</summary>
public class OasisStayDbContextFabrica : IDesignTimeDbContextFactory<OasisStayDbContext>
{
    public OasisStayDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("OASISSTAY_CADENA")
            ?? "Server=localhost;Database=OasisStay;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var opciones = new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(cadena).Options;
        return new OasisStayDbContext(opciones, new CifradorAes256(new byte[32]));
    }
}
