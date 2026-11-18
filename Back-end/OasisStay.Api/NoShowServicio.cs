using OasisStay.Aplicacion;

namespace OasisStay.Api;

/// <summary>
/// Rutina de No-Show en segundo plano [DE#4 CP-R05][JIRA US-08 contexto][D-44]: cada hora procesa las reservas
/// Confirmadas cuyo día de llegada ya terminó (corte = ayer, hora UTC-5); es idempotente y se pone al día tras un reinicio.
/// </summary>
public class NoShowServicio : BackgroundService
{
    /// <summary>Regla de arranque: activo por defecto; no corre en pruebas, con NoShow:AutoActivo=false ni con --semilla-demo en Development (para demostrar la acción manual).</summary>
    public static bool DebeEjecutarse(IConfiguration config, IHostEnvironment entorno, string[] args)
        => !entorno.IsEnvironment("Testing")
           && config.GetValue("NoShow:AutoActivo", true)
           && !(entorno.IsDevelopment() && args.Contains("--semilla-demo"));

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NoShowServicio> _log;
    public NoShowServicio(IServiceScopeFactory scopes, ILogger<NoShowServicio> log) { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        using var reloj = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var hoy = scope.ServiceProvider.GetRequiredService<IReloj>().Ahora.Date;
                var n = scope.ServiceProvider.GetRequiredService<ICancelacionReservas>().EjecutarNoShow(hoy.AddDays(-1));
                if (n > 0) _log.LogInformation("No-Show: {Procesadas} reservas procesadas", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Falló la rutina de No-Show");
            }
        } while (await SiguienteAsync(reloj, ct));
    }

    private static async Task<bool> SiguienteAsync(PeriodicTimer t, CancellationToken ct)
    {
        try { return await t.WaitForNextTickAsync(ct); } catch (OperationCanceledException) { return false; }
    }
}
