using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Simulation
{
    [Serializable]
    public struct LeaderboardEntry
    {
        public string team;
        public string scenarioId;
        public string scenarioName;
        public DifficultyLevel difficulty;
        public int score;
        public string rank;
        public MissionVerdict verdict;
        public int collaboration;
        public string similarMission;
        public int seed;
        public string dateUtc;
    }

    /// <summary>Ranking local de misiones (persistentDataPath/leaderboard.json), por escenario.</summary>
    public static class Leaderboard
    {
        [Serializable]
        private sealed class Data
        {
            public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "leaderboard.json");

        public static LeaderboardEntry From(MissionOutcomeReport o, string team) => new LeaderboardEntry
        {
            team = string.IsNullOrWhiteSpace(team) ? MissionDesign.Core.Loc.L("Equipo", "Team") : team.Trim(),
            scenarioId = o.scenarioId,
            scenarioName = o.scenarioName,
            difficulty = o.difficulty,
            score = o.score,
            rank = o.rank,
            verdict = o.verdict,
            collaboration = o.collaborationScore,
            similarMission = o.similarMission,
            seed = o.seed,
            dateUtc = o.generatedAtUtc
        };

        public static List<LeaderboardEntry> Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) return new List<LeaderboardEntry>();
            try
            {
                return JsonUtility.FromJson<Data>(File.ReadAllText(path))?.entries ?? new List<LeaderboardEntry>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Leaderboard] Archivo dañado, se empieza de cero: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        public static void Add(LeaderboardEntry entry, string path = null)
        {
            path = path ?? DefaultPath;
            var all = Load(path);
            all.Add(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonUtility.ToJson(new Data { entries = all }, true));
        }

        /// <summary>Mejores entradas de un escenario: puntuación y luego colaboración.</summary>
        public static List<LeaderboardEntry> Top(string scenarioId, int count = 5, string path = null) =>
            Load(path).Where(e => e.scenarioId == scenarioId)
                .OrderByDescending(e => e.score).ThenByDescending(e => e.collaboration)
                .Take(count).ToList();
    }
}
