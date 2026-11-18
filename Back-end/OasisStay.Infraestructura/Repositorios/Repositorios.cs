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
    {
        var h = _db.Habitaciones.Include(h => h.Tipo).FirstOrDefault(h => h.NumeroHabitacion == numero);
        h?.ReconstruirEstado(); // estado de comportamiento real desde el texto persistido [E2 §3 State]
        return h;
    }

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
        => _db.Reservas
            .Where(r => r.NumeroHabitacion == habitacion.NumeroHabitacion
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso)
                && r.FechaInicio < fechaFin && r.FechaFin > fechaInicio)
            .ToList();

    public Reserva? BuscarPorId(int idReserva)
        => _db.Reservas.Include(r => r.Cliente).Include(r => r.Habitacion).ThenInclude(h => h!.Tipo)
            .FirstOrDefault(r => r.IdReserva == idReserva);

    public IReadOnlyList<Reserva> Listar(EstadoReserva? estado, string? texto)
    {
        var q = _db.Reservas.Include(r => r.Cliente).Include(r => r.Habitacion).AsQueryable();
        if (estado.HasValue) q = q.Where(r => r.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(r => r.Cliente!.Nombre.Contains(t) || r.IdReserva.ToString() == t);
        }
        return q.OrderByDescending(r => r.FechaInicio).ThenByDescending(r => r.IdReserva).ToList();
    }

    public IReadOnlyList<Reserva> ListarConfirmadasHasta(DateTime fechaCorte)
        => _db.Reservas.Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaInicio <= fechaCorte).ToList();

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

    public Cliente? BuscarPorDocumento(TipoDocumento tipo, string documento)
        => _db.Clientes.FirstOrDefault(c => c.TipoDocumento == tipo && c.Documento == documento);

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
    public Servicio? BuscarServicio(string nombre) => _db.Servicios.FirstOrDefault(s => s.Nombre == nombre);
    public IReadOnlyList<ConsumoReserva> ConsultarPorReserva(int idReserva)
        => _db.ConsumosReserva.Where(c => c.IdReserva == idReserva).OrderBy(c => c.IdConsumo).ToList();
    public void Agregar(ConsumoReserva consumo)
    {
        _db.ConsumosReserva.Add(consumo);
        _db.SaveChanges();
    }
}

/// <summary>La factura emitida es inmutable: no se actualiza ni se emite una segunda por reserva [JIRA US-06 RN3 y riesgo].</summary>
public class FacturaRepository : IFacturaRepository
{
    private readonly OasisStayDbContext _db;
    public FacturaRepository(OasisStayDbContext db) => _db = db;

    public void Guardar(Factura factura)
    {
        if (factura.IdFactura != 0) throw new ConflictoDominioException("La factura emitida no se puede modificar");
        _db.Facturas.Add(factura);
        try { _db.SaveChanges(); }
        catch (DbUpdateException) when (_db.Facturas.Any(f => f.IdReserva == factura.IdReserva && f.IdFactura != factura.IdFactura))
        {
            _db.Entry(factura).State = EntityState.Detached;
            throw new ConflictoDominioException(MensajesFacturacion.YaFacturada);
        }
    }

    public Factura? BuscarPorId(int idFactura) => _db.Facturas.Include(f => f.Detalles).FirstOrDefault(f => f.IdFactura == idFactura);
    public Factura? BuscarPorReserva(int idReserva) => _db.Facturas.Include(f => f.Detalles).FirstOrDefault(f => f.IdReserva == idReserva);
}

public class RepositorioPagos : IRepositorioPagos
{
    private readonly OasisStayDbContext _db;
    public RepositorioPagos(OasisStayDbContext db) => _db = db;

    public void Guardar(Pago pago)
    {
        if (_db.Entry(pago).State == EntityState.Detached)
        {
            if (pago.IdPago == 0) _db.Pagos.Add(pago); else _db.Pagos.Update(pago);
        }
        _db.SaveChanges();
    }

    public IReadOnlyList<Pago> ConsultarPorReserva(int idReserva)
        => _db.Pagos.Where(p => p.IdReserva == idReserva).OrderBy(p => p.IdPago).ToList();
}

/// <summary>Solo inserción [D-23][JIRA US-07 RN2].</summary>
public class RepositorioHistorial : IRepositorioHistorial, IRepositorioAuditoriaReportes
{
    private readonly OasisStayDbContext _db;
    private readonly IUsuarioActual? _usuario;
    public RepositorioHistorial(OasisStayDbContext db) => _db = db;
    /// <summary>El evento del check-out registra el usuario autenticado [D-47].</summary>
    public RepositorioHistorial(OasisStayDbContext db, IUsuarioActual usuario) : this(db) => _usuario = usuario;

    public void RegistrarEvento(HistorialReserva evento)
    {
        _db.HistorialReservas.Add(evento);
        _db.SaveChanges();
    }

    /// <summary>Eventos en orden cronológico según los filtros [DS3 BuscarEventos(filtros)][JIRA US-07 ESC1/ESC2].</summary>
    public IReadOnlyList<HistorialReserva> BuscarEventos(FiltrosHistorial filtros)
    {
        var q = _db.HistorialReservas.AsNoTracking().AsQueryable();
        if (filtros.IdReserva.HasValue) q = q.Where(h => h.IdReserva == filtros.IdReserva.Value);
        if (!string.IsNullOrWhiteSpace(filtros.Usuario))
        {
            var u = filtros.Usuario.Trim().ToLower();
            q = q.Where(h => h.Usuario.ToLower() == u);
        }
        if (filtros.Desde.HasValue)
        {
            var d = filtros.Desde.Value.Date;
            q = q.Where(h => h.FechaRegistro >= d);
        }
        if (filtros.Hasta.HasValue)
        {
            var h2 = filtros.Hasta.Value.Date.AddDays(1);
            q = q.Where(h => h.FechaRegistro < h2);
        }
        if (!string.IsNullOrWhiteSpace(filtros.Texto))
        {
            var t = filtros.Texto.Trim().ToLower();
            q = q.Where(h => h.AccionRealizada.ToLower().Contains(t) || h.Usuario.ToLower().Contains(t)
                || (h.ValorAnterior != null && h.ValorAnterior.ToLower().Contains(t))
                || (h.ValorNuevo != null && h.ValorNuevo.ToLower().Contains(t)));
        }
        return q.OrderBy(h => h.FechaRegistro).ThenBy(h => h.IdHistorial).ToList();
    }

    /// <summary>Constancia de la exportación: usuario, fecha/hora, rango y formato [DS3 GuardarAuditoria][D-46].</summary>
    public void GuardarAuditoria(AuditoriaReporte reporte)
    {
        _db.AuditoriasReporte.Add(reporte);
        _db.SaveChanges();
    }

    /// <summary>
    /// Último paso del check-out: Reserva EnCurso → Finalizada y evento en la bitácora [DE#4 archivarReserva][JIRA US-06 RN2][D-23].
    /// Firma del GoF Archivar(idReserva, factura) [CTR §5].
    /// </summary>
    public void Archivar(int idReserva, Factura factura)
    {
        var reserva = _db.Reservas.FirstOrDefault(r => r.IdReserva == idReserva)
            ?? throw new NoEncontradoException("La reserva no existe.");
        var anterior = reserva.Estado;
        reserva.HacerCheckOut();
        RegistrarEvento(new HistorialReserva
        {
            FechaRegistro = factura.FechaEmision,
            AccionRealizada = "Check-out",
            IdReserva = idReserva,
            Usuario = string.IsNullOrWhiteSpace(_usuario?.Nombre) ? "sistema" : _usuario.Nombre, // [D-47]
            ValorAnterior = anterior.ToString(),
            ValorNuevo = $"{reserva.Estado} · Factura folio {factura.Folio}",
        });
    }
}
