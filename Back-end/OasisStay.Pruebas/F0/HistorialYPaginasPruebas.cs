extern alias Front;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using OasisStay.Aplicacion;
using OasisStay.Infraestructura.Repositorios;

namespace OasisStay.Pruebas.F0;

public class HistorialYPaginasPruebas
{
    private static readonly Regex Prohibido = new("^(Actualizar|Borrar|Eliminar|Modificar|Update|Delete|Remove|Editar)", RegexOptions.IgnoreCase);

    [Fact] // [D-23][JIRA US-07 RN2]
    public void F0_Historial_SoloInsercion()
    {
        foreach (var tipo in new[] { typeof(IRepositorioHistorial), typeof(RepositorioHistorial) })
        {
            var nombres = tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToList();
            Assert.Contains("RegistrarEvento", nombres);
            Assert.Contains("Archivar", nombres);
            Assert.DoesNotContain(nombres, n => Prohibido.IsMatch(n));
        }
    }

    private static string[] Roles(Type pagina)
    {
        var a = pagina.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(a);
        return a!.Roles!.Split(',').Select(r => r.Trim()).OrderBy(r => r).ToArray();
    }

    [Fact] // [D-34][RNF-06][E2 §6]
    public void F0_Paginas_PorRol()
    {
        var asm = typeof(Front::Front_end.Pages.IndexModel).Assembly;
        Type T(string n) => asm.GetType("Front_end.Pages." + n)!;

        Assert.Equal(new[] { "Administrador", "AmaDeLlaves", "Recepcionista" }, Roles(T("HabitacionesModel")));
        foreach (var n in new[] { "PanelControlModel", "ReservasModel", "FacturacionModel" })
            Assert.Equal(new[] { "Administrador", "Recepcionista" }, Roles(T(n)));
        foreach (var n in new[] { "HistorialModel", "ConfiguracionModel" })
            Assert.Equal(new[] { "Administrador" }, Roles(T(n)));
    }
}
