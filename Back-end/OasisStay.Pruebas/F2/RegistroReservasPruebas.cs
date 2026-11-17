using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;

namespace OasisStay.Pruebas.F2;

/// <summary>Registro, huésped, reasignación y check-in con repositorios reales sobre EF InMemory y dobles de estado.</summary>
public class RegistroReservasPruebas
{
    private static Banco BancoConHabitaciones()
    {
        var b = new Banco();
        b.Sembrar((304, "Suite Ejecutiva", EstadoHabitacion.DISPONIBLE), (201, "Suite Ejecutiva", EstadoHabitacion.DISPONIBLE),
                  (105, "Doble Clásica", EstadoHabitacion.DISPONIBLE), (107, "Doble Clásica", EstadoHabitacion.DISPONIBLE),
                  (102, "Doble Clásica", EstadoHabitacion.DISPONIBLE));
        return b;
    }

    [Fact] // [DE#4 CP-R01][DS1]
    public void CP_R01_RegistrarReserva_Supera4Validaciones_CreaConfirmada()
    {
        var b = BancoConHabitaciones();
        var (s, db) = b.Nuevo();
        var r = s.RegistrarReserva(Banco.Solicitud(304));
        Assert.True(r.IdReserva > 0);
        Assert.Equal(EstadoReserva.Confirmada, r.Estado);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
        Assert.Equal(1, v.Reservas.Count());
    }

    [Fact] // [DE#4 CP-R02]
    public void CP_R02_RegistrarReserva_Solapamiento_NoSeCrea()
    {
        var b = BancoConHabitaciones();
        b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304));
        // la habitación vuelve a Disponible (p. ej. otra reserva del mismo cuarto en fechas cruzadas): solo queda RN-01
        using (var db = b.Contexto()) { db.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado = EstadoHabitacion.DISPONIBLE; db.SaveChanges(); }
        var ex = Assert.Throws<ConflictoDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304, 2, "2026-12-12", "2026-12-16")));
        Assert.Equal("Las fechas se solapan con otra reserva (RN-01)", ex.Message);
        using var v = b.Contexto();
        Assert.Equal(1, v.Reservas.Count());
    }

    [Fact] // [DE#1 CP-H02]
    public void CP_H02_Reservar_ExcedeCapacidad_RechazadoSigueDisponible()
    {
        var b = BancoConHabitaciones();
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(102, 5)));
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", ex.Message);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 102).Estado);
        Assert.Empty(v.Reservas);
    }

    [Fact] // [DE#4 CP-R06][D-31]
    public void CP_R06_CheckInSinDocumento_RechazadoSigueConfirmada()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304)).IdReserva;
        foreach (var doc in new string?[] { null, "", "999" })
        {
            var ex = Assert.Throws<ConflictoDominioException>(() => b.Nuevo().Servicio.HacerCheckIn(id, doc, "Recepcionista Demo"));
            Assert.Equal("Debe verificar el documento del huésped", ex.Message);
        }
        using var v = b.Contexto();
        Assert.Equal(EstadoReserva.Confirmada, v.Reservas.Single().Estado);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
    }

    [Fact] // [DE#4][DE#1][D-31]
    public void CheckIn_ConDocumento_ReservaEnCursoYHabitacionOcupada()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304)).IdReserva;
        var r = b.Nuevo().Servicio.HacerCheckIn(id, "1020304050", "Recepcionista Demo");
        Assert.Equal(EstadoReserva.EnCurso, r.Estado);
        using var v = b.Contexto();
        Assert.Equal(EstadoReserva.EnCurso, v.Reservas.Single().Estado);
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
        Assert.Contains(v.HistorialReservas, h => h.AccionRealizada == "Check-in" && h.IdReserva == id);
    }

    [Fact] // [D-31]
    public void CheckIn_AntesDeFechaOReservaNoConfirmada_Rechazado()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304, 2, "2026-12-20", "2026-12-22")).IdReserva;
        var ex = Assert.Throws<ConflictoDominioException>(() => b.Nuevo().Servicio.HacerCheckIn(id, "1020304050", "u"));
        Assert.Equal("El check-in no puede hacerse antes de la fecha de llegada", ex.Message);

        b.Reloj.Ahora = new DateTime(2026, 12, 20, 15, 0, 0);
        b.Nuevo().Servicio.HacerCheckIn(id, "1020304050", "u");
        var ex2 = Assert.Throws<ConflictoDominioException>(() => b.Nuevo().Servicio.HacerCheckIn(id, "1020304050", "u"));
        Assert.Equal("La reserva no está confirmada", ex2.Message);
    }

    [Fact] // [JIRA US-03 ESC1]
    public void US03_ESC1_HuespedNuevo_Guarda()
    {
        var b = BancoConHabitaciones();
        var r = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304, 2, "2026-12-10", "2026-12-14"));
        using var v = b.Contexto();
        var c = v.Clientes.Single();
        Assert.Equal((TipoDocumento.CC, "1020304050", "Carlos Mendoza", "3101234567", "carlos.mendoza@email.com"),
            (c.TipoDocumento, c.Documento, c.Nombre, c.Telefono, c.Correo));
        Assert.Equal(c.IdCliente, r.IdCliente);
        Assert.Equal(new DateTime(2026, 12, 14), v.Reservas.Single().FechaFin);
        Assert.Equal(2, v.Reservas.Single().Huespedes);
    }

    [Fact] // [JIRA US-03 ESC1] y RN3: el documento existente se actualiza sin duplicar
    public void US03_ESC1_DocumentoExistente_ActualizaDatosSinDuplicar()
    {
        var b = BancoConHabitaciones();
        b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304));
        var s2 = Banco.Solicitud(201, 2, "2026-12-20", "2026-12-22");
        s2.Cliente!.Telefono = "3200000000";
        b.Nuevo().Servicio.RegistrarReserva(s2);
        using var v = b.Contexto();
        Assert.Equal("3200000000", v.Clientes.Single().Telefono);
    }

    [Fact] // [JIRA US-03 ESC2]
    public void US03_ESC2_Autocompletado_CC1020304050()
    {
        var b = BancoConHabitaciones();
        b.Nuevo().Servicio.GuardarHuesped(Banco.Carlos());
        var c = b.Nuevo().Servicio.BuscarHuesped(TipoDocumento.CC, "1020304050");
        Assert.NotNull(c);
        Assert.Equal(("Carlos Mendoza", "3101234567", "carlos.mendoza@email.com"), (c!.Nombre, c.Telefono, c.Correo));
        Assert.Null(b.Nuevo().Servicio.BuscarHuesped(TipoDocumento.CE, "1020304050"));
        Assert.Null(b.Nuevo().Servicio.BuscarHuesped(TipoDocumento.CC, "999"));
    }

    [Fact] // [JIRA US-03 ESC3]
    public void US03_ESC3_CorreoSinArrobaOTelefono310ABC_Invalido()
    {
        var b = BancoConHabitaciones();
        var c = Banco.Carlos();
        c.Correo = "carlos.mendoza.com"; c.Telefono = "310ABC";
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.GuardarHuesped(c));
        Assert.Equal(new[] { "correo", "telefono" }, ex.Errores.Keys.OrderBy(k => k).ToArray());
        Assert.False(ex.Errores.ContainsKey("nombre"));

        var s = Banco.Solicitud(304); s.Cliente = c;
        Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(s));
        using var v = b.Contexto();
        Assert.Empty(v.Reservas);
        Assert.Empty(v.Clientes);
    }

    [Fact] // [JIRA US-03 RN1]
    public void US03_RN1_CamposObligatorios()
    {
        var c = new Cliente { TipoDocumento = (TipoDocumento)(-1) };
        var ex = Assert.Throws<ValidacionDominioException>(() => c.RegistrarDatos());
        Assert.Equal(new[] { "correo", "documento", "nombre", "telefono", "tipoDocumento" }, ex.Errores.Keys.OrderBy(k => k).ToArray());
    }

    [Fact] // [JIRA US-04 ESC1]
    public void US04_ESC1_Hab304Suite_Asigna_PasaAReservada()
    {
        var b = BancoConHabitaciones();
        var r = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304, 2));
        using var v = b.Contexto();
        var rr = v.Reservas.Single();
        Assert.Equal(304, rr.NumeroHabitacion);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
        Assert.Equal(r.IdReserva, rr.IdReserva);
    }

    [Fact] // [JIRA US-04 ESC2][E1 RN-05]
    public void US04_ESC2_5HuespedesEnEstandarCapMax2_RechazaPorCapacidad()
    {
        var b = BancoConHabitaciones();
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105, 5)));
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", ex.Message);
    }

    [Fact] // [JIRA US-04 ESC2][E1 RN-01][JIRA US-04 NFR]
    public async Task US04_ESC2_DosAsignacionesSimultaneas_SoloUnaGana()
    {
        var b = BancoConHabitaciones();
        var puerta = new ManualResetEventSlim(false);
        Task<Exception?> Intento(string doc) => Task.Run<Exception?>(() =>
        {
            var s = Banco.Solicitud(304);
            s.Cliente!.Documento = doc;
            puerta.Wait();
            try { b.Nuevo().Servicio.RegistrarReserva(s); return null; } catch (Exception e) { return e; }
        });
        var tareas = new[] { Intento("111"), Intento("222"), Intento("333") };
        puerta.Set();
        var resultados = await Task.WhenAll(tareas);
        Assert.Equal(1, resultados.Count(e => e == null));
        Assert.All(resultados.Where(e => e != null), e => Assert.False(string.IsNullOrWhiteSpace(e!.Message)));
        using var v = b.Contexto();
        Assert.Equal(1, v.Reservas.Count());
    }

    [Fact] // [JIRA US-04 ESC3][D-21]
    public void US04_ESC3_Reasigna105a107_105Disponible_107Reservada()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105)).IdReserva;
        var r = b.Nuevo().Servicio.Reasignar(id, 107, false, "Recepcionista Demo");
        Assert.Equal(107, r.NumeroHabitacion);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado);
        Assert.Equal(107, v.Reservas.Single().NumeroHabitacion);
        var h = v.HistorialReservas.Single(x => x.AccionRealizada == "Habitación reasignada");
        Assert.Equal(("Hab. 105", "Hab. 107"), (h.ValorAnterior, h.ValorNuevo));
    }

    [Fact] // [JIRA US-04 RN3][D-21][D-32]
    public void US04_ReasignaCategoriaSuperior_PideConfirmacionUpgrade()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105)).IdReserva;
        var ex = Assert.Throws<ConflictoDominioException>(() => b.Nuevo().Servicio.Reasignar(id, 201, false, "u"));
        Assert.Equal("La habitación nueva es de categoría superior (Upgrade). Confirme para continuar.", ex.Message);
        using (var v = b.Contexto())
        {
            Assert.Equal(105, v.Reservas.Single().NumeroHabitacion);
            Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
            Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 201).Estado);
        }
        Assert.Equal(201, b.Nuevo().Servicio.Reasignar(id, 201, true, "u").NumeroHabitacion);
    }

    [Fact] // [JIRA US-04 ESC3] la nueva pasa por la cadena
    public void US04_Reasigna_ANuevaOcupadaOCapacidadInsuficiente_Rechazada()
    {
        var b = BancoConHabitaciones();
        var s = Banco.Solicitud(304, 4);
        var id = b.Nuevo().Servicio.RegistrarReserva(s).IdReserva;
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.Reasignar(id, 105, false, "u"));
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", ex.Message);
        b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(107));
        var id2 = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105, 2, "2026-12-20", "2026-12-22")).IdReserva;
        var ex2 = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.Reasignar(id2, 107, false, "u"));
        Assert.Equal("La habitación no está disponible para reservar", ex2.Message);
    }

    [Fact] // [JIRA US-07 RN1/RN3][D-23]
    public void Historial_CrearReserva_RegistraUsuarioYValores()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304)).IdReserva;
        using var v = b.Contexto();
        var h = v.HistorialReservas.Single();
        Assert.Equal(id, h.IdReserva);
        Assert.Equal("Recepcionista Demo", h.Usuario);
        Assert.Equal("Reserva creada", h.AccionRealizada);
        Assert.Equal(b.Reloj.Ahora, h.FechaRegistro);
        Assert.Null(h.ValorAnterior);
        Assert.Contains("Confirmada", h.ValorNuevo);
    }

    [Fact] // [DE#4][D-04]
    public void Notificador_Confirmada_RegistraSimulada()
    {
        var b = BancoConHabitaciones();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304)).IdReserva;
        using var v = b.Contexto();
        var n = v.Notificaciones.Single();
        Assert.Equal("carlos.mendoza@email.com", n.Destinatario);
        Assert.Contains($"#{id}", n.Mensaje);
    }
}
