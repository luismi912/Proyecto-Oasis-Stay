using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura;

/// <summary>Reloj en hora local de Colombia.</summary>
public class RelojColombia : IReloj
{
    private static readonly TimeZoneInfo Zona = Buscar();
    private static TimeZoneInfo Buscar()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Local;
    }
    public DateTime Ahora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);
}

/// <summary>Unidad de trabajo sobre transacción de EF Core [GoF Facade].</summary>
public class UnidadDeTrabajoEf : IUnidadDeTrabajo
{
    private readonly OasisStayDbContext _db;
    public UnidadDeTrabajoEf(OasisStayDbContext db) => _db = db;
    public ITransaccion Iniciar() => new TransaccionEf(_db.Database.BeginTransaction());

    private sealed class TransaccionEf : ITransaccion
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;
        public TransaccionEf(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;
        public void Confirmar() => _tx.Commit();
        public void Dispose() => _tx.Dispose();
    }
}

/// <summary>Adaptador simulado: guarda la notificación en BD [D-04].</summary>
public class NotificadorSimulado : INotificador
{
    private readonly OasisStayDbContext _db;
    private readonly IReloj _reloj;
    public NotificadorSimulado(OasisStayDbContext db, IReloj reloj) { _db = db; _reloj = reloj; }

    public void Notificar(string destinatario, string asunto, string mensaje)
    {
        _db.Notificaciones.Add(new Notificacion { Fecha = _reloj.Ahora, Destinatario = destinatario, Asunto = asunto, Mensaje = mensaje });
        _db.SaveChanges();
    }
}
