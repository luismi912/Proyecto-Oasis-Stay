using System.Globalization;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OasisStay.Infraestructura.Facturacion;

/// <summary>PDF de la factura cerrada con QuestPDF; no calcula nada, solo imprime lo emitido [CTR §5][D-04][E1 RF-06].</summary>
public class FacturaPdfGenerator : IGeneradorFacturaPdf
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    static FacturaPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Dinero(decimal v) => "$" + v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", Co);

    public byte[] Generar(Factura factura, string huesped, int numeroHabitacion)
    {
        var subtotal = factura.ValorTotal - factura.ImpuestoIVA;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Lato"));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Oasis Stay").FontSize(22).Bold().FontColor(Colors.Teal.Darken3);
                    c.Item().Text("Factura de venta (documento interno)").FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(180).Column(c =>
                {
                    c.Item().AlignRight().Text($"Factura No. {factura.Folio:D6}").FontSize(14).Bold();
                    c.Item().AlignRight().Text($"Emitida: {factura.FechaEmision:dd/MM/yyyy HH:mm}");
                });
            });

            page.Content().PaddingVertical(20).Column(col =>
            {
                col.Spacing(12);
                col.Item().Text(t =>
                {
                    t.Line($"Huésped: {huesped}");
                    t.Line($"Habitación: {numeroHabitacion}");
                    t.Line($"Reserva: #{factura.IdReserva}");
                });

                col.Item().Table(tabla =>
                {
                    tabla.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    tabla.Header(h =>
                    {
                        foreach (var titulo in new[] { "Concepto", "Cant.", "Valor unitario", "Subtotal" })
                            h.Cell().Background(Colors.Teal.Darken3).Padding(5).Text(titulo).FontColor(Colors.White).Bold();
                    });
                    foreach (var d in factura.Detalles)
                    {
                        tabla.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.Concepto);
                        tabla.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(d.Cantidad.ToString());
                        tabla.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(Dinero(d.ValorUnitario));
                        tabla.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(Dinero(d.SubTotal));
                    }
                });

                col.Item().AlignRight().Width(240).Column(c =>
                {
                    c.Item().Row(r => { r.RelativeItem().Text("Subtotal"); r.ConstantItem(100).AlignRight().Text(Dinero(subtotal)); });
                    c.Item().Row(r => { r.RelativeItem().Text("IVA (19%)"); r.ConstantItem(100).AlignRight().Text(Dinero(factura.ImpuestoIVA)); });
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Total (COP)").Bold().FontSize(12);
                        r.ConstantItem(100).AlignRight().Text(Dinero(factura.ValorTotal)).Bold().FontSize(12);
                    });
                });
            });

            page.Footer().AlignCenter().Text("Oasis Stay · Documento interno de auditoría, sin integración con la DIAN").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
    }
}
