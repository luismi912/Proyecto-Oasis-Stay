namespace OasisStay.Pruebas;

/// <summary>Nombres de base y cadenas de conexión únicos por ejecución, para que varias copias del repositorio
/// puedan correr las pruebas a la vez sin pisarse. Prefijo fijo OasisStay_Pruebas (nunca OasisStay) [D-06].</summary>
public static class NombresBasePruebas
{
    public const string Prefijo = "OasisStay_Pruebas";

    private static readonly string Sufijo = NuevoSufijoDeEjecucion();

    public static string SufijoActual => Sufijo;

    /// <summary>Nombre único para la ejecución actual; <paramref name="area"/> distingue la base (F2, F3Api...).</summary>
    public static string Nombre(string area = "") => Nombre(area, Sufijo);

    public static string Nombre(string area, string sufijoEjecucion) => $"{Prefijo}{area}_{sufijoEjecucion}";

    /// <summary>Genera un sufijo nuevo, como lo haría otra ejecución.</summary>
    public static string NuevoSufijoDeEjecucion() => $"{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..8]}";

    public static string Cadena(string area = "") =>
        $"Server=localhost;Database={Nombre(area)};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
}
