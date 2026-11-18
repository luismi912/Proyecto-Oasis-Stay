using ClosedXML.Excel;
using OasisStay.Aplicacion;

namespace OasisStay.Infraestructura.Reportes;

/// <summary>
/// .xlsx con ClosedXML [D-46]: hoja "Facturas" (fecha, folio, concepto, impuestos, método de pago, total) y hoja "Resumen"
/// con KPIs y desglose por método de pago [JIRA US-11 ESC1]. No calcula: escribe lo recibido.
/// </summary>
public class GeneradorReporteExcel : IGeneradorReporte
{
    public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string FormatoDinero = "#,##0.00";

    public string Formato => "xlsx";

    public ArchivoReporte Generar(DatosReporte d)
    {
        using var libro = new XLWorkbook();

        var hoja = libro.Worksheets.Add("Facturas");
        string[] titulos = { "Fecha", "Folio", "Concepto", "Impuestos", "Método de pago", "Total" };
        for (var c = 0; c < titulos.Length; c++) hoja.Cell(1, c + 1).Value = titulos[c];
        var encabezado = hoja.Range(1, 1, 1, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
        encabezado.Style.Font.FontColor = XLColor.White;
        var fila = 2;
        foreach (var f in d.Filas)
        {
            hoja.Cell(fila, 1).Value = f.Fecha;
            hoja.Cell(fila, 2).Value = f.Folio;
            hoja.Cell(fila, 3).Value = f.Concepto;
            hoja.Cell(fila, 4).Value = f.Impuestos;
            hoja.Cell(fila, 5).Value = f.MetodoPago;
            hoja.Cell(fila, 6).Value = f.Total;
            fila++;
        }
        hoja.Column(1).Style.DateFormat.Format = "dd/MM/yyyy";
        hoja.Columns(4, 4).Style.NumberFormat.Format = FormatoDinero;
        hoja.Column(6).Style.NumberFormat.Format = FormatoDinero;
        hoja.SheetView.FreezeRows(1);
        hoja.Columns().AdjustToContents();

        var r = libro.Worksheets.Add("Resumen");
        var s = d.Resumen;
        r.Cell(1, 1).Value = "Oasis Stay · Reporte financiero";
        r.Cell(1, 1).Style.Font.Bold = true;
        r.Cell(1, 1).Style.Font.FontSize = 14;
        r.Cell(2, 1).Value = "Periodo";
        r.Cell(2, 2).Value = $"{d.Desde:dd/MM/yyyy} al {d.Hasta:dd/MM/yyyy}";
        r.Cell(3, 1).Value = "Generado";
        r.Cell(3, 2).Value = $"{d.Generado:dd/MM/yyyy HH:mm} por {d.Usuario}";
        var kpis = new (string Nombre, XLCellValue Valor, string? Formato)[]
        {
            ("Ingreso Total", s.IngresoTotal, FormatoDinero), ("Ingreso bruto (con IVA)", s.IngresoBruto, FormatoDinero),
            ("Ingreso neto (sin IVA)", s.IngresoNeto, FormatoDinero), ("Facturas", s.Facturas, null),
            ("Ticket Promedio", s.TicketPromedio, FormatoDinero), ("Ocupación %", s.OcupacionPorcentaje, "0.00"),
            ("Cancelaciones (penalidades)", s.Cancelaciones, FormatoDinero),
        };
        var k = 5;
        foreach (var (nombre, valor, formato) in kpis)
        {
            r.Cell(k, 1).Value = nombre;
            r.Cell(k, 1).Style.Font.Bold = true;
            r.Cell(k, 2).Value = valor;
            if (formato != null) r.Cell(k, 2).Style.NumberFormat.Format = formato;
            k++;
        }
        k++;
        r.Cell(k, 1).Value = "Método de pago";
        r.Cell(k, 2).Value = "Monto";
        r.Cell(k, 3).Value = "Pagos";
        r.Range(k, 1, k, 3).Style.Font.Bold = true;
        foreach (var m in s.PorMetodoPago)
        {
            k++;
            r.Cell(k, 1).Value = m.Metodo;
            r.Cell(k, 2).Value = m.Monto;
            r.Cell(k, 2).Style.NumberFormat.Format = FormatoDinero;
            r.Cell(k, 3).Value = m.Pagos;
        }
        r.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return new ArchivoReporte(ms.ToArray(), $"reporte-financiero_{d.Desde:yyyyMMdd}_{d.Hasta:yyyyMMdd}.xlsx", TipoXlsx);
    }
}
