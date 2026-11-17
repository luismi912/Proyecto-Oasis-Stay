using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace OasisStay.Infraestructura.Repositorios;

/// <summary>[CTR §5] BuscarLibres es de F1.</summary>
public class RepositorioHabitaciones : IRepositorioHabitaciones
{
    private readonly OasisStayDbContext _db;
    public RepositorioHabitaciones(OasisStayDbContext db) => _db = db;

    public IReadOnlyList<Habitacion> BuscarLibres(DateTime fechaInicio, DateTime fechaFin, TipoHabitacion? tipo, int numHuespedes)
    {
        // Solo Disponible, capacidad suficiente y sin reserva solapada [D-35][RN-01][RN-05]
        var consulta = _db.Habitaciones.Include(h => h.Tipo)
            .Where(h => h.Estado == EstadoHabitacion.DISPONIBLE && h.Tipo!.CapacidadMax >= numHuespedes);
        if (tipo is not null) consulta = consulta.Where(h => h.IdTipo == tipo.IdTipo);
        consulta = consulta.Where(h => !_db.Reservas.Any(r => r.NumeroHabitacion == h.NumeroHabitacion
            && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso)
            && r.FechaInicio < fechaFin && r.FechaFin > fechaInicio));
        var libres = consulta.OrderBy(h => h.NumeroHabitacion).ToList();
        foreach (var h in libres) h.ReconstruirEstado();
        return libres;
    }

    public IReadOnlyList<Habitacion> ConsultarTodas()
        => _db.Habitaciones.Include(h => h.Tipo).OrderBy(h => h.NumeroHabitacion).ToList();

    public Habitacion? BuscarPorNumero(int numero)
        => _db.Habitaciones.Include(h => h.Tipo).FirstOrDefault(h => h.NumeroHabitacion == numero);

    public void Guardar(Habitacion habitacion)
    {
        if (_db.Entry(habitacion).State == EntityState.Detached)
        {
            if (_db.Habitaciones.Any(h => h.NumeroHabitacion == habitacion.NumeroHabitacion)) _db.Habitaciones.Update(habitacion);
            else _db.Habitaciones.Add(habitacion);
        }
        _db.SaveChanges();
    }
}

/// <summary>[CTR §2 TipoHabitacion]</summary>
public class RepositorioTiposHabitacion : IRepositorioTiposHabitacion
{
    private readonly OasisStayDbContext _db;
    public RepositorioTiposHabitacion(OasisStayDbContext db) => _db = db;
    public IReadOnlyList<TipoHabitacion> ConsultarTodos() => _db.TiposHabitacion.OrderBy(t => t.IdTipo).ToList();
    public TipoHabitacion? BuscarPorId(int idTipo) => _db.TiposHabitacion.FirstOrDefault(t => t.IdTipo == idTipo);
}

/// <summary>[CTR §5] BuscarPorHabitacionYFechas es de F2.</summary>
public class RepositorioReservas : IRepositorioReservas
{
    private readonly OasisStayDbContext _db;
    public RepositorioReservas(OasisStayDbContext db) => _db = db;

    public IReadOnlyList<Reserva> BuscarPorHabitacionYFechas(Habitacion habitacion, DateTime fechaInicio, DateTime fechaFin)
        => throw new NotImplementedException("F2");

    public void Guardar(Reserva reserva)
    {
        if (_db.Entry(reserva).State == EntityState.Detached) _db.Reservas.Update(reserva);
        _db.SaveChanges();
    }
}

/// <summary>Búsqueda por igualdad sobre el dato cifrado de forma determinista [D-03][JIRA US-03 ESC2].</summary>
public class RepositorioHuespedes : IRepositorioHuespedes
{
    private readonly OasisStayDbContext _db;
    public RepositorioHuespedes(OasisStayDbContext db) => _db = db;

    public Cliente? BuscarPorDocumento(string documento)
        => _db.Clientes.FirstOrDefault(c => c.Documento == documento);

    public void Guardar(Cliente cliente)
    {
        if (_db.Entry(cliente).State == EntityState.Detached) _db.Clientes.Update(cliente);
        _db.SaveChanges();
    }
}

public class RepositorioConsumos : IRepositorioConsumos
{
    private readonly OasisStayDbContext _db;
    public RepositorioConsumos(OasisStayDbContext db) => _db = db;
    public IReadOnlyList<Servicio> ConsultarServicios() => _db.Servicios.OrderBy(s => s.Nombre).ToList();
}

public class FacturaRepository : IFacturaRepository
{
    private readonly OasisStayDbContext _db;
    public FacturaRepository(OasisStayDbContext db) => _db = db;
    public void Guardar(Factura factura)
    {
        if (_db.Entry(factura).State == EntityState.Detached) _db.Facturas.Update(factura);
        _db.SaveChanges();
    }
}

public class RepositorioPagos : IRepositorioPagos
{
    private readonly OasisStayDbContext _db;
    public RepositorioPagos(OasisStayDbContext db) => _db = db;
    public void Guardar(Pago pago)
    {
        if (_db.Entry(pago).State == EntityState.Detached) _db.Pagos.Update(pago);
        _db.SaveChanges();
    }
}

/// <summary>Solo inserción [D-23][JIRA US-07 RN2]. Archivar es de F4.</summary>
public class RepositorioHistorial : IRepositorioHistorial
{
    private readonly OasisStayDbContext _db;
    public RepositorioHistorial(OasisStayDbContext db) => _db = db;

    public void RegistrarEvento(HistorialReserva evento)
    {
        _db.HistorialReservas.Add(evento);
        _db.SaveChanges();
    }

    public void Archivar(int idReserva, Factura factura) => throw new NotImplementedException("F4");
}
