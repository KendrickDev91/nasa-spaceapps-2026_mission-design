using System.Collections.Generic;
using System.Reflection;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>
    /// Traduce en memoria los textos del catálogo (piezas, órbitas, escenarios, eventos, misiones reales) al idioma actual.
    /// Los .asset siguen en español; los originales se guardan la primera vez y se restauran al volver al español
    /// (y, en el editor, al salir del modo de juego, para no ensuciar los assets).
    /// </summary>
    public static class DataLocalizer
    {
        private static readonly string[] Fields =
        {
            "displayName", "description", "heritage", "sourceNotes", "educationalNote", "briefing", "country",
            "operatorName", "note", "mitigationAdvice", "educationalFact", "requiredBusName"
        };

        private static readonly Dictionary<(ScriptableObject, string), string> originals = new Dictionary<(ScriptableObject, string), string>();
        private static readonly List<ScriptableObject> touched = new List<ScriptableObject>();

        public static void Apply(NasaDatabaseSO db)
        {
            if (db == null) return;
            foreach (var so in Enumerate(db)) Apply(so);
        }

        private static IEnumerable<ScriptableObject> Enumerate(NasaDatabaseSO db)
        {
            foreach (var x in db.launchVehicles) yield return x;
            foreach (var x in db.orbits) yield return x;
            foreach (var x in db.instruments) yield return x;
            foreach (var x in db.buses) yield return x;
            foreach (var x in db.solarArrays) yield return x;
            foreach (var x in db.batteries) yield return x;
            foreach (var x in db.antennas) yield return x;
            foreach (var x in db.computers) yield return x;
            foreach (var x in db.attitudeControls) yield return x;
            foreach (var x in db.groundNetworks) yield return x;
            foreach (var x in db.randomEvents) yield return x;
            foreach (var x in db.scenarios) yield return x;
            foreach (var x in db.realMissions) yield return x;
        }

        private static void Apply(ScriptableObject so)
        {
            if (so == null) return;
            var type = so.GetType();
            foreach (var name in Fields)
            {
                var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f == null || f.FieldType != typeof(string)) continue;
                var key = (so, name);
                if (!originals.TryGetValue(key, out var es))
                {
                    es = (string)f.GetValue(so);
                    originals[key] = es;
                    if (!touched.Contains(so)) touched.Add(so);
                }
                string value = es;
                if (Loc.English && !string.IsNullOrEmpty(es))
                {
                    if (DataTranslations.ByAsset.TryGetValue(so.name + "." + name, out var en) ||
                        DataTranslations.ByText.TryGetValue(es, out en))
                        value = en;
                }
                f.SetValue(so, value);
            }
        }

        /// <summary>Devuelve todos los textos a su original en español.</summary>
        public static void RestoreAll()
        {
            foreach (var so in touched)
            {
                if (so == null) continue;
                var type = so.GetType();
                foreach (var name in Fields)
                {
                    var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (f != null && originals.TryGetValue((so, name), out var es)) f.SetValue(so, es);
                }
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditor()
        {
            UnityEditor.EditorApplication.playModeStateChanged += s =>
            {
                if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode || s == UnityEditor.PlayModeStateChange.EnteredEditMode) RestoreAll();
            };
        }
#endif
    }
}
