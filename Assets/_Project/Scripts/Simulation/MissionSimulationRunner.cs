using System;
using System.Collections;
using System.Linq;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// Al aprobarse el lanzamiento calcula el resultado con MissionSimulator y lo reproduce fase a fase
    /// (tiempo acelerado), emitiendo cada entrada de la cronología para la UI y la sala de control.
    /// Al final exporta el reporte JSON/TXT y entrega el resultado al MissionStateManager.
    /// </summary>
    public sealed class MissionSimulationRunner : MonoBehaviour
    {
        public static readonly MissionPhase[] PlaybackPhases =
        {
            MissionPhase.Launch, MissionPhase.OrbitInsertion, MissionPhase.ScienceOperations, MissionPhase.DataTransmission
        };

        [Tooltip("Segundos de pantalla que dura cada fase.")]
        [Min(0.1f)] [SerializeField] private float secondsPerPhase = 3f;
        [Tooltip("Pausa extra tras cada evento aleatorio, para leerlo en la demo.")]
        [Min(0f)] [SerializeField] private float secondsPerEvent = 1.5f;
        [Tooltip("-1 = semilla aleatoria. Fija un número para repetir exactamente la misma misión.")]
        [SerializeField] private int fixedSeed = -1;
        [SerializeField] private bool allowLaunchFailure = true;
        [SerializeField] private bool exportReport = true;
        [SerializeField] private bool recordLeaderboard = true;

        private MissionStateManager manager;
        private Coroutine playback;
        private bool skipRequested;

        public event Action<SimulationLogEntry> EntryEmitted;
        /// <summary>Progreso global 0-1 de la reproducción.</summary>
        public event Action<float> ProgressChanged;

        public bool IsRunning => playback != null;
        public MissionOutcomeReport Current { get; private set; }

        public float SecondsPerPhase
        {
            get => secondsPerPhase;
            set => secondsPerPhase = Mathf.Max(0.01f, value);
        }

        public int FixedSeed
        {
            get => fixedSeed;
            set => fixedSeed = value;
        }

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager != null) manager.LaunchApproved += OnLaunchApproved;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.LaunchApproved -= OnLaunchApproved;
        }

        /// <summary>Termina la reproducción al instante (botón "Saltar").</summary>
        public void SkipToEnd() => skipRequested = true;

        private void OnLaunchApproved()
        {
            // Básico: un solo evento y sin fallos de lanzamiento. Avanzado: 1-2 eventos y riesgo real del cohete.
            bool advanced = manager.Design.IsAdvanced;
            var db = manager.Database;
            var settings = new SimulationSettings
            {
                seed = fixedSeed >= 0 ? fixedSeed : Environment.TickCount & int.MaxValue,
                allowLaunchFailure = advanced && allowLaunchFailure,
                minEvents = 1,
                maxEvents = advanced ? 2 : 1,
                realMissions = db != null ? db.realMissions : null
            };
            Current = MissionSimulator.Run(manager.Design, db != null ? db.randomEvents : null, settings);

            // Trabajo en equipo medido durante el diseño (si hay tracker en la escena).
            var tracker = FindAnyObjectByType<Multiplayer.CollaborationTracker>();
            if (tracker != null)
            {
                MissionSimulator.ApplyCollaboration(Current, tracker.Log.Score, tracker.Log.Notes);
                tracker.Log.Reset();
            }
            if (playback != null) StopCoroutine(playback);
            skipRequested = false;
            playback = StartCoroutine(Play(Current));
        }

        private IEnumerator Play(MissionOutcomeReport outcome)
        {
            float total = PlaybackPhases.Length;
            for (int p = 0; p < PlaybackPhases.Length; p++)
            {
                var phase = PlaybackPhases[p];
                // La misión terminó antes de esta fase (fallo de lanzamiento o de inserción).
                if (!PhaseReached(outcome, phase)) break;
                var entries = outcome.timeline.Where(e => e.phase == phase).ToList();

                manager.SetPhase(phase);
                float slot = entries.Count > 0 ? secondsPerPhase / entries.Count : secondsPerPhase;
                if (entries.Count == 0) yield return Wait(secondsPerPhase, p / total, (p + 1) / total);
                for (int i = 0; i < entries.Count; i++)
                {
                    EntryEmitted?.Invoke(entries[i]);
                    bool isEvent = entries[i].tone == LogTone.Warning || entries[i].tone == LogTone.Critical
                                   || (phase == MissionPhase.ScienceOperations && i > 0);
                    float from = (p + (float)i / entries.Count) / total;
                    float to = (p + (float)(i + 1) / entries.Count) / total;
                    yield return Wait(slot + (isEvent ? secondsPerEvent : 0f), from, to);
                }
            }

            foreach (var e in outcome.timeline.Where(e => e.phase == MissionPhase.PostMission))
                EntryEmitted?.Invoke(e);
            ProgressChanged?.Invoke(1f);

            string path = null;
            if (exportReport)
            {
                try { path = MissionReportExporter.Export(outcome); }
                catch (Exception ex) { Debug.LogWarning($"[Simulation] No se pudo exportar el reporte: {ex.Message}"); }
            }
            if (path != null) Debug.Log($"[Simulation] Reporte exportado: {path}");

            if (recordLeaderboard)
            {
                var roles = FindAnyObjectByType<Multiplayer.RoleTurnManager>();
                try { Leaderboard.Add(Leaderboard.From(outcome, roles != null ? roles.TeamName : null)); }
                catch (Exception ex) { Debug.LogWarning($"[Simulation] No se pudo guardar el ranking: {ex.Message}"); }
            }

            playback = null;
            manager.CompleteMission(outcome, path);
        }

        private static bool PhaseReached(MissionOutcomeReport o, MissionPhase phase)
        {
            switch (phase)
            {
                case MissionPhase.Launch: return true;
                case MissionPhase.OrbitInsertion: return o.launchSucceeded;
                default: return o.launchSucceeded && o.orbitInsertionSucceeded;
            }
        }

        private IEnumerator Wait(float seconds, float fromProgress, float toProgress)
        {
            float time = 0f;
            while (time < seconds && !skipRequested)
            {
                time += Time.deltaTime;
                ProgressChanged?.Invoke(Mathf.Lerp(fromProgress, toProgress, time / seconds));
                yield return null;
            }
            ProgressChanged?.Invoke(toProgress);
        }
    }
}
