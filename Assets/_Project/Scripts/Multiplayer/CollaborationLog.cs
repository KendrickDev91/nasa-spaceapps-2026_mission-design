using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.Multiplayer
{
    /// <summary>
    /// Mide cómo trabajó el equipo hasta el lanzamiento: cuántos roles aportaron decisiones y
    /// cuántos conflictos bloqueantes se resolvieron negociando. Lógica pura (testeable).
    /// </summary>
    public sealed class CollaborationLog
    {
        private const string IncompleteTitle = "Selección incompleta";

        private readonly Dictionary<MissionRole, int> contributions = new Dictionary<MissionRole, int>();
        private readonly HashSet<string> seenConflicts = new HashSet<string>();
        private readonly HashSet<string> resolvedConflicts = new HashSet<string>();

        public int RolesContributed => contributions.Count(kv => kv.Value > 0);
        public int ConflictsResolved => resolvedConflicts.Count;

        public void RecordChange(MissionRole activeRole)
        {
            contributions.TryGetValue(activeRole, out int n);
            contributions[activeRole] = n + 1;
        }

        public void RecordEvaluation(LaunchValidationResult validation)
        {
            if (validation == null) return;
            var current = new HashSet<string>(validation.Blocking.Where(i => i.title != IncompleteTitle).Select(i => i.title));
            foreach (var t in seenConflicts.Where(t => !current.Contains(t))) resolvedConflicts.Add(t);
            seenConflicts.UnionWith(current);
        }

        public void Reset()
        {
            contributions.Clear();
            seenConflicts.Clear();
            resolvedConflicts.Clear();
        }

        /// <summary>0-10: hasta 6 por roles que aportaron (1.5 c/u) + hasta 4 por conflictos resueltos.</summary>
        public int Score => Mathf.Clamp(Mathf.RoundToInt(RolesContributed * 1.5f + Mathf.Min(4, ConflictsResolved)), 0, 10);

        public string Notes =>
            $"{RolesContributed} de 4 roles tomaron decisiones y el equipo resolvió {ConflictsResolved} conflicto(s) antes de lanzar";
    }
}
