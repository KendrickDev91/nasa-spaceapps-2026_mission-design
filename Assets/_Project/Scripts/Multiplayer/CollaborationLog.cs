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
        private static string IncompleteTitle => Loc.L("Selección incompleta", "Incomplete selection");

        private readonly Dictionary<MissionRole, int> contributions = new Dictionary<MissionRole, int>();
        private readonly HashSet<MissionRole> approvers = new HashSet<MissionRole>();
        private readonly HashSet<string> seenConflicts = new HashSet<string>();
        private readonly HashSet<string> resolvedConflicts = new HashSet<string>();

        /// <summary>Roles que cambiaron el diseño.</summary>
        public int RolesContributed => contributions.Count(kv => kv.Value > 0);
        /// <summary>Roles que aprobaron el diseño en algún momento.</summary>
        public int RolesApproved => approvers.Count;
        /// <summary>Roles que participaron: cambiaron el diseño o lo aprobaron.</summary>
        public int RolesParticipated => contributions.Where(kv => kv.Value > 0).Select(kv => kv.Key).Union(approvers).Count();
        public int ConflictsResolved => resolvedConflicts.Count;

        public void RecordChange(MissionRole activeRole)
        {
            contributions.TryGetValue(activeRole, out int n);
            contributions[activeRole] = n + 1;
        }

        /// <summary>Aprobar el diseño también es participar en la decisión.</summary>
        public void RecordApproval(MissionRole role) => approvers.Add(role);

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
            approvers.Clear();
            seenConflicts.Clear();
            resolvedConflicts.Clear();
        }

        /// <summary>
        /// 0-10: por rol, 1.5 si cambió el diseño o 1.25 si solo lo aprobó (máx. 6) + hasta 4 por conflictos resueltos.
        /// </summary>
        public int Score
        {
            get
            {
                var changed = new HashSet<MissionRole>(contributions.Where(kv => kv.Value > 0).Select(kv => kv.Key));
                float roles = changed.Count * 1.5f + approvers.Count(r => !changed.Contains(r)) * 1.25f;
                return Mathf.Clamp(Mathf.RoundToInt(roles + Mathf.Min(4, ConflictsResolved)), 0, 10);
            }
        }

        public string Notes =>
            // Corto a propósito: la tarjeta post-misión recorta cada viñeta a ~110 caracteres.
            Loc.L($"{RolesParticipated} de 4 roles {(RolesParticipated == 1 ? "participó" : "participaron")} " +
                  $"({RolesApproved} {(RolesApproved == 1 ? "aprobó" : "aprobaron")}, {RolesContributed} con cambios) " +
                  $"y {(ConflictsResolved == 1 ? "resolvió 1 conflicto" : $"resolvieron {ConflictsResolved} conflictos")}",
                  $"{RolesParticipated} of 4 roles took part " +
                  $"({RolesApproved} approved, {RolesContributed} made changes) " +
                  $"and resolved {(ConflictsResolved == 1 ? "1 conflict" : $"{ConflictsResolved} conflicts")}");
    }
}
