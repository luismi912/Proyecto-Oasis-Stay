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
        => throw new NotImplementedException("F1");

    public IReadOnlyList<Habitacion> ConsultarTodas()
        => _db.Habitaciones.Include(h => h.Tipo).OrderBy(h => h.NumeroHabitacion).ToList();

    public Habitacion? BuscarPorNumero(int numero)
        => _db.Habitaciones.Include(h => h.Tipo).FirstOrDefault(h => h.NumeroHabitacion == numero);

    public void Guardar(Habitacion habitacion)
    {
        if (_db.Entry(habitacion).State == EntityState.Detached) _db.Habitaciones.Update(habitacion);
        _db.SaveChanges();
    }
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
