using System.Diagnostics;
using ClosedXML.Excel;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Reportes;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F6;

/// <summary>Reportes financieros exportables [JIRA US-11][DS3][D-46]: servicio, generadores, auditoría y semilla por escenario.</summary>
public class ReportesPruebas
{
    private static readonly DateTime Ini = new(2026, 10, 1), Fin = new(2026, 10, 15);
    private static readonly DateTime Ahora = new(2026, 11, 17, 9, 30, 0);

    private sealed class RelojDePrueba : IReloj { public DateTime Ahora => ReportesPruebas.Ahora; }

    private sealed class GeneradorQueFalla : IGeneradorReporte
    {
        public string Formato => "pdf";
        public ArchivoReporte Generar(DatosReporte datos) => throw new InvalidOperationException("disco lleno");
    }

    /// <summary>Banco en memoria con el escenario US11 ya sembrado.</summary>
    private static async Task<Banco> BancoConEscenario()
    {
        var banco = new Banco();
        banco.Sembrar((101, "Doble Clásica", EstadoHabitacion.DISPONIBLE));
        await using var db = banco.Contexto();
        await new EjecutorSemillaDemo(new ISemillaDemo[] { new SemillaDemoF6() }).EjecutarAsync(db, "US11");
        return banco;
    }

    private static ServicioReportes Servicio(OasisStayDbContext db, IEnumerable<IGeneradorReporte>? generadores = null)
        => new(new RepositorioReportes(db),
            new AuditoriaHistorial(new RepositorioHistorial(db), new RepositorioHistorial(db), generadores ?? new IGeneradorReporte[] { new GeneradorReporteExcel(), new GeneradorReportePdf() }, new RelojDePrueba()));

    [Fact] // [D-41][JIRA US-11 datos]
    public async Task Semilla_PorEscenario_US11()
    {
        var banco = await BancoConEscenario();
        await using (var otra = banco.Contexto())
            await new EjecutorSemillaDemo(new ISemillaDemo[] { new SemillaDemoF6() }).EjecutarAsync(otra, "US11"); // reinicia sin duplicar
        await using var db = banco.Contexto();

        var facturas = db.Facturas.ToList();
        Assert.Equal(45, facturas.Count);
        Assert.All(facturas, f => Assert.InRange(f.FechaEmision.Date, Ini, Fin));
        Assert.Equal(12_500_000m, facturas.Sum(f => f.ValorTotal));
        var pagos = db.Pagos.ToList();
        Assert.Equal(45, pagos.Count);
        Assert.Equal(new[] { MetodoPago.EFECTIVO, MetodoPago.TARJETA_CREDITO, MetodoPago.TARJETA_DEBITO, MetodoPago.TRANSFERENCIA },
            pagos.Select(p => p.MetodoPago).Distinct().Order().ToArray());
        Assert.Equal(12_500_000m, pagos.Sum(p => p.Monto));
        Assert.Empty(db.Facturas.Where(f => f.FechaEmision >= new DateTime(2027, 1, 1)));
    }

    [Fact] // [JIRA US-11 RN2][JIRA US-11 datos]
    public async Task US11_Datos_45Facturas_12500000_ElTotalCuadraAlCentavo()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var r = Servicio(db).ResumenFinanciero(Ini, Fin);

        Assert.Equal(45, r.Facturas);
        Assert.Equal(12_500_000m, r.IngresoTotal);
        Assert.Equal(12_500_000m, r.IngresoBruto);
        Assert.Equal(12_500_000m, r.IngresoNeto + db.Facturas.Sum(f => f.ImpuestoIVA)); // bruto = neto + IVA [D-46]
        Assert.True(r.IngresoNeto < r.IngresoBruto);
        Assert.Equal(12_500_000m, r.PorMetodoPago.Sum(m => m.Monto)); // el desglose por método cuadra con las facturas
        Assert.Equal(4, r.PorMetodoPago.Count);
        Assert.Equal(Math.Round(12_500_000m / 45, 2), r.TicketPromedio); // Ticket Promedio = ingreso total ÷ facturas [D-46]
        Assert.InRange(r.OcupacionPorcentaje, 0.01m, 100m);
        Assert.Equal(0m, r.Cancelaciones);
    }

    [Fact] // [JIRA US-11 ESC1]
    public async Task US11_ESC1_ExportaExcel_01al15Oct2026_ColumnasFechaFolioConceptoImpuestosMetodoTotal()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var archivo = Servicio(db).GenerarReporte(Ini, Fin, "xlsx", "admin@oasisstay.co", "Administrador");

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", archivo.TipoContenido);
        Assert.EndsWith(".xlsx", archivo.NombreArchivo);
        using var libro = new XLWorkbook(new MemoryStream(archivo.Contenido));
        var hoja = libro.Worksheet("Facturas");
        Assert.Equal(new[] { "Fecha", "Folio", "Concepto", "Impuestos", "Método de pago", "Total" },
            Enumerable.Range(1, 6).Select(c => hoja.Cell(1, c).GetString()).ToArray());
        Assert.Equal(46, hoja.LastRowUsed()!.RowNumber()); // encabezado + 45 facturas
        var total = Enumerable.Range(2, 45).Sum(f => hoja.Cell(f, 6).GetValue<decimal>());
        Assert.Equal(12_500_000m, total);
        Assert.Equal(45, Enumerable.Range(2, 45).Select(f => hoja.Cell(f, 2).GetValue<int>()).Distinct().Count());
        Assert.All(Enumerable.Range(2, 45), f =>
        {
            Assert.InRange(hoja.Cell(f, 1).GetDateTime().Date, Ini, Fin);
            Assert.Equal("Estadía", hoja.Cell(f, 3).GetString());
            Assert.NotEqual("", hoja.Cell(f, 5).GetString());
            Assert.True(hoja.Cell(f, 4).GetValue<decimal>() > 0);
        });
    }

    [Fact] // [JIRA US-11 NFR]
    public async Task US11_Excel_AbreEnExcel2016()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var archivo = Servicio(db).GenerarReporte(Ini, Fin, "xlsx", "admin@oasisstay.co", "Administrador");

        Assert.Equal((byte)'P', archivo.Contenido[0]); // contenedor OOXML (zip) que Excel 2016+ abre
        Assert.Equal((byte)'K', archivo.Contenido[1]);
        using var libro = new XLWorkbook(new MemoryStream(archivo.Contenido));
        Assert.Equal(new[] { "Facturas", "Resumen" }, libro.Worksheets.Select(h => h.Name).ToArray());
        var resumen = libro.Worksheet("Resumen");
        var texto = string.Join("|", resumen.CellsUsed().Select(c => c.GetString()));
        Assert.Contains("Ingreso Total", texto);
        Assert.Contains("Ticket Promedio", texto);
        Assert.Contains("Ocupación %", texto);
        Assert.Contains("EFECTIVO", texto);
    }

    [Fact] // [JIRA US-11 ESC2]
    public async Task US11_ESC2_ExportaPdf_ConGraficosYTotalesConsolidados()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var archivo = Servicio(db).GenerarReporte(Ini, Fin, "pdf", "admin@oasisstay.co", "Administrador");

        Assert.Equal("application/pdf", archivo.TipoContenido);
        Assert.EndsWith(".pdf", archivo.NombreArchivo);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(archivo.Contenido, 0, 4));
        Assert.True(archivo.Contenido.Length > 3000);

        // Los datos que alimentan los gráficos y los totales consolidados son los mismos del resumen
        var datos = new RepositorioReportes(db);
        Assert.Equal(15, datos.IngresoPorDia(Ini, Fin).Count);
        Assert.Equal(12_500_000m, datos.IngresoPorDia(Ini, Fin).Sum(d => d.Total));
    }

    [Fact] // [JIRA US-11 ESC3]
    public async Task US11_ESC3_SinMovimientos_Ano2027_NoGeneraArchivo()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var s = Servicio(db);
        var desde = new DateTime(2027, 1, 1);
        var hasta = new DateTime(2027, 1, 15);

        Assert.Equal(0, s.ResumenFinanciero(desde, hasta).Facturas); // el resumen avisa con facturas = 0
        var ex = Assert.Throws<ConflictoDominioException>(() => s.GenerarReporte(desde, hasta, "xlsx", "admin@oasisstay.co", "Administrador"));
        Assert.Equal(MensajesReportes.SinMovimientos, ex.Message);
        Assert.Empty(db.AuditoriasReporte); // no hubo archivo, no hay auditoría
    }

    [Fact] // [JIRA US-11 ESC3][JIRA US-11 RN3]
    public async Task US11_ESC3_Rango400Dias_Rechaza()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var s = Servicio(db);
        var hasta = Ini.AddDays(399); // 400 días continuos

        var ex = Assert.Throws<ValidacionDominioException>(() => s.GenerarReporte(Ini, hasta, "pdf", "admin@oasisstay.co", "Administrador"));
        Assert.Equal(MensajesReportes.RangoExcedido, ex.Message);
        Assert.Equal(MensajesReportes.RangoExcedido, Assert.Throws<ValidacionDominioException>(() => s.ResumenFinanciero(Ini, hasta)).Message);
        s.ResumenFinanciero(Ini, Ini.AddDays(364)); // 365 días exactos sí se permite
    }

    [Fact] // [JIRA US-11 ESC3]
    public async Task US11_ESC3_DesdeMayorQueHasta_Rechaza()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var ex = Assert.Throws<ValidacionDominioException>(() => Servicio(db).ResumenFinanciero(Fin, Ini));
        Assert.Equal(MensajesReportes.RangoInvertido, ex.Message);
    }

    [Theory] // [JIRA US-11 RN1][D-34]
    [InlineData("AmaDeLlaves")]
    [InlineData("Recepcionista")]
    public async Task US11_Roles_ServicioRechazaQuienNoEsAdministrador(string rol)
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var ex = Assert.Throws<AccesoProhibidoException>(() => Servicio(db).GenerarReporte(Ini, Fin, "xlsx", "otro@oasisstay.co", rol));
        Assert.Equal(MensajesReportes.SoloAdministrador, ex.Message);
        Assert.Empty(db.AuditoriasReporte);
    }

    [Fact] // [JIRA US-11 supuesto]
    public async Task US11_SoloFacturasYRecibosCerrados()
    {
        var banco = await BancoConEscenario();
        await using (var db = banco.Contexto())
        {
            var cliente = db.Clientes.First();
            // Cuenta abierta: reserva en curso con un abono sin factura dentro del rango
            var abierta = new Reserva { IdCliente = cliente.IdCliente, NumeroHabitacion = 101, FechaInicio = new(2026, 10, 10), FechaFin = new(2026, 10, 14), Huespedes = 1, Estado = EstadoReserva.EnCurso };
            abierta.Pagos.Add(new Pago { FechaPago = new(2026, 10, 11), Monto = 999_999m, MetodoPago = MetodoPago.EFECTIVO });
            db.Reservas.Add(abierta);
            await db.SaveChangesAsync();
        }
        await using var consulta = banco.Contexto();
        var r = Servicio(consulta).ResumenFinanciero(Ini, Fin);
        Assert.Equal(45, r.Facturas);
        Assert.Equal(12_500_000m, r.IngresoTotal);
        Assert.Equal(12_500_000m, r.PorMetodoPago.Sum(m => m.Monto));
    }

    [Fact] // [D-46][D-44]
    public async Task US11_Cancelaciones_SumaPenalidadesDelRango()
    {
        var banco = await BancoConEscenario();
        await using (var db = banco.Contexto())
        {
            var cliente = db.Clientes.First();
            db.Reservas.AddRange(
                new Reserva { IdCliente = cliente.IdCliente, NumeroHabitacion = 101, FechaInicio = new(2026, 10, 5), FechaFin = new(2026, 10, 7), Huespedes = 1, Estado = EstadoReserva.Cancelada, Penalidad = 180_000m },
                new Reserva { IdCliente = cliente.IdCliente, NumeroHabitacion = 101, FechaInicio = new(2026, 10, 8), FechaFin = new(2026, 10, 9), Huespedes = 1, Estado = EstadoReserva.NoShow, Penalidad = 150_000m },
                new Reserva { IdCliente = cliente.IdCliente, NumeroHabitacion = 101, FechaInicio = new(2026, 12, 8), FechaFin = new(2026, 12, 9), Huespedes = 1, Estado = EstadoReserva.Cancelada, Penalidad = 500_000m });
            await db.SaveChangesAsync();
        }
        await using var consulta = banco.Contexto();
        Assert.Equal(330_000m, Servicio(consulta).ResumenFinanciero(Ini, Fin).Cancelaciones);
    }

    [Theory] // [DS3 alt]
    [InlineData("csv")]
    [InlineData("")]
    [InlineData("docx")]
    public async Task US11_FormatoInvalido_ErrorGeneracion(string formato)
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var ex = Assert.Throws<ValidacionDominioException>(() => Servicio(db).GenerarReporte(Ini, Fin, formato, "admin@oasisstay.co", "Administrador"));
        Assert.Equal(MensajesReportes.FormatoInvalido, ex.Message);
        Assert.Empty(db.AuditoriasReporte);
    }

    [Fact] // [DS3 alt]
    public async Task US11_ErrorAlGenerar_ErrorGeneracionConMotivo_SinAuditoria()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var s = Servicio(db, new IGeneradorReporte[] { new GeneradorQueFalla(), new GeneradorReporteExcel() });
        var ex = Assert.Throws<ErrorGeneracionReporteException>(() => s.GenerarReporte(Ini, Fin, "pdf", "admin@oasisstay.co", "Administrador"));
        Assert.Equal(MensajesReportes.ErrorGeneracion("disco lleno"), ex.Message);
        Assert.Empty(db.AuditoriasReporte);
    }

    [Fact] // [DS3][D-46]
    public async Task US11_ExportarRegistraAuditoria()
    {
        var banco = await BancoConEscenario();
        await using (var db = banco.Contexto())
        {
            var s = Servicio(db);
            s.GenerarReporte(Ini, Fin, "xlsx", "admin@oasisstay.co", "Administrador");
            s.GenerarReporte(Ini, Fin, "pdf", "admin@oasisstay.co", "Administrador");
        }
        await using var consulta = banco.Contexto();
        var filas = consulta.AuditoriasReporte.OrderBy(a => a.IdAuditoria).ToList();
        Assert.Equal(2, filas.Count);
        Assert.Equal(("admin@oasisstay.co", Ahora, Ini, Fin, "xlsx"), (filas[0].Usuario, filas[0].FechaHora, filas[0].Desde, filas[0].Hasta, filas[0].Formato));
        Assert.Equal("pdf", filas[1].Formato);
    }

    [Fact] // [JIRA US-11 NFR]
    public async Task US11_Tiempo_Menor3sParaUnMes()
    {
        var banco = await BancoConEscenario();
        await using var db = banco.Contexto();
        var s = Servicio(db);
        var reloj = Stopwatch.StartNew();
        s.GenerarReporte(Ini, new DateTime(2026, 10, 31), "xlsx", "admin@oasisstay.co", "Administrador");
        s.GenerarReporte(Ini, new DateTime(2026, 10, 31), "pdf", "admin@oasisstay.co", "Administrador");
        reloj.Stop();
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(3), $"Tardó {reloj.Elapsed.TotalSeconds:N2} s");
    }
}
