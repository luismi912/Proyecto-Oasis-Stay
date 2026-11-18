using System.Globalization;
using OasisStay.Aplicacion;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OasisStay.Infraestructura.Reportes;

/// <summary>
/// PDF membretado con QuestPDF [D-04][JIRA US-11 ESC2]: KPIs, totales consolidados, desglose por método de pago y gráficos de
/// ingresos (barras dibujadas con QuestPDF, sin paquetes nuevos). No calcula: imprime lo recibido.
/// </summary>
public class GeneradorReportePdf : IGeneradorReporte
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string Verde = Colors.Teal.Darken3;

    static GeneradorReportePdf() => QuestPDF.Settings.License = LicenseType.Community;

    public string Formato => "pdf";

    private static string Dinero(decimal v) => "$" + v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", Co);

    public ArchivoReporte Generar(DatosReporte d)
    {
        var s = d.Resumen;
        var bytes = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Lato"));

            page.Header().Column(h =>
            {
                h.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Oasis Stay").FontSize(24).Bold().FontColor(Verde);
                        c.Item().Text("Reporte financiero · ingresos, cancelaciones y ocupación").FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(190).Column(c =>
                    {
                        c.Item().AlignRight().Text($"Periodo: {d.Desde:dd/MM/yyyy} al {d.Hasta:dd/MM/yyyy}").Bold();
                        c.Item().AlignRight().Text($"Generado: {d.Generado:dd/MM/yyyy HH:mm}");
                        c.Item().AlignRight().Text($"Por: {d.Usuario}");
                    });
                });
                h.Item().PaddingTop(6).LineHorizontal(2).LineColor(Verde);
            });

            page.Content().PaddingVertical(14).Column(col =>
            {
                col.Spacing(14);

                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    Tarjeta(row, "Ingreso Total", Dinero(s.IngresoTotal));
                    Tarjeta(row, "Ticket Promedio", Dinero(s.TicketPromedio));
                    Tarjeta(row, "Ocupación %", s.OcupacionPorcentaje.ToString("N2", Co) + " %");
                });

                col.Item().Column(c =>
                {
                    c.Item().PaddingBottom(4).Text("Totales consolidados").FontSize(13).Bold().FontColor(Verde);
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); });
                        void Fila(string a, string b, bool negrita = false)
                        {
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Text(a).Style(negrita ? TextStyle.Default.Bold() : TextStyle.Default);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).AlignRight().Text(b).Style(negrita ? TextStyle.Default.Bold() : TextStyle.Default);
                        }
                        Fila("Facturas cerradas", s.Facturas.ToString("N0", Co));
                        Fila("Ingreso bruto (con IVA)", Dinero(s.IngresoBruto));
                        Fila("IVA", Dinero(s.IngresoBruto - s.IngresoNeto));
                        Fila("Ingreso neto (sin IVA)", Dinero(s.IngresoNeto));
                        Fila("Cancelaciones (penalidades)", Dinero(s.Cancelaciones));
                        Fila("Total consolidado", Dinero(s.IngresoTotal), true);
                    });
                });

                col.Item().Column(c =>
                {
                    c.Item().PaddingBottom(4).Text("Ingresos por método de pago").FontSize(13).Bold().FontColor(Verde);
                    var max = s.PorMetodoPago.Count == 0 ? 0m : s.PorMetodoPago.Max(m => m.Monto);
                    foreach (var m in s.PorMetodoPago)
                        Barra(c, m.Metodo.Replace('_', ' '), m.Monto, max, $"{Dinero(m.Monto)} · {m.Pagos} pagos", 110);
                });

                col.Item().Column(c =>
                {
                    c.Item().PaddingBottom(4).Text("Ingresos por día").FontSize(13).Bold().FontColor(Verde);
                    var max = d.IngresoPorDia.Count == 0 ? 0m : d.IngresoPorDia.Max(x => x.Total);
                    foreach (var x in d.IngresoPorDia)
                        Barra(c, x.Fecha.ToString("dd/MM/yyyy"), x.Total, max, Dinero(x.Total), 70);
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Oasis Stay · documento interno · página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        })).GeneratePdf();

        return new ArchivoReporte(bytes, $"reporte-financiero_{d.Desde:yyyyMMdd}_{d.Hasta:yyyyMMdd}.pdf", "application/pdf");
    }

    private static void Tarjeta(RowDescriptor row, string titulo, string valor)
        => row.RelativeItem().Background(Colors.Teal.Lighten5).Border(0.5f).BorderColor(Colors.Teal.Lighten2).Padding(8).Column(c =>
        {
            c.Item().Text(titulo).FontSize(9).FontColor(Colors.Grey.Darken2);
            c.Item().Text(valor).FontSize(15).Bold().FontColor(Verde);
        });

    /// <summary>Barra horizontal proporcional al máximo, dibujada con dos celdas de una fila.</summary>
    private static void Barra(ColumnDescriptor c, string etiqueta, decimal valor, decimal max, string texto, float anchoEtiqueta)
    {
        var parte = max <= 0 ? 0f : (float)(valor / max);
        c.Item().PaddingVertical(1.5f).Row(row =>
        {
            row.ConstantItem(anchoEtiqueta).Text(etiqueta).FontSize(8);
            row.RelativeItem().Row(barra =>
            {
                if (parte > 0) barra.RelativeItem(parte).Height(10).Background(Colors.Teal.Medium);
                if (parte < 1) barra.RelativeItem(1 - parte).Height(10);
            });
            row.ConstantItem(130).AlignRight().Text(texto).FontSize(8);
        });
    }
}
