using System.Collections;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// Escena cinemática que el runner reproduce al terminar las fases de vuelo y antes del análisis post-misión
    /// (p. ej. el amerizaje de la cápsula). Vive en la capa de UI; la simulación solo conoce esta interfaz.
    /// </summary>
    public interface IMissionCinematic
    {
        /// <summary>¿Corresponde mostrarla para este resultado? (p. ej. solo si la cápsula vuelve a casa).</summary>
        bool WantsToPlay(MissionOutcomeReport outcome);

        /// <summary>Reproduce la cinemática. Debe terminar pronto si <paramref name="skipRequested"/> pasa a true.</summary>
        IEnumerator Play(MissionOutcomeReport outcome, System.Func<bool> skipRequested);
    }
}
