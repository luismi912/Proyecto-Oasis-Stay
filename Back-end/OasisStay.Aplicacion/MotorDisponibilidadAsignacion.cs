using System.Collections.Concurrent;

namespace OasisStay.Aplicacion;

/// <summary>
/// Único dueño del bloqueo transaccional de habitaciones [CTR §5][C4-N3][E1 RF-04][JIRA US-04 NFR].
/// Serializa las operaciones sobre una misma habitación (bloqueo por número) y las ejecuta dentro de una
/// transacción de la unidad de trabajo: si algo falla, se revierte todo [RN-01][DS1].
/// </summary>
public class MotorDisponibilidadAsignacion
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Bloqueos = new();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;

    public MotorDisponibilidadAsignacion(IUnidadDeTrabajo unidadDeTrabajo) => _unidadDeTrabajo = unidadDeTrabajo;

    /// <summary>Bloquea las habitaciones (en orden, para no provocar interbloqueos) y ejecuta la operación en una transacción.</summary>
    public T EjecutarBloqueado<T>(IEnumerable<int> numerosHabitacion, Func<T> operacion)
    {
        var tomados = new List<SemaphoreSlim>();
        try
        {
            foreach (var numero in numerosHabitacion.Distinct().OrderBy(n => n))
            {
                var bloqueo = Bloqueos.GetOrAdd(numero, _ => new SemaphoreSlim(1, 1));
                bloqueo.Wait();
                tomados.Add(bloqueo);
            }
            using var tx = _unidadDeTrabajo.Iniciar();
            var resultado = operacion();
            tx.Confirmar();
            return resultado;
        }
        finally
        {
            foreach (var b in tomados) b.Release();
        }
    }
}
