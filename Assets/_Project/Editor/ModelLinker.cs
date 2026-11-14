using System.Collections.Generic;
using System.IO;
using MissionDesign.Data;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Asigna los modelos FBX generados en Blender (ArtSource/Blender) a los ScriptableObjects del catálogo:
    /// piezas por componentId (o genérico por prefijo) y cohetes por vehicleId. También fija los ajustes
    /// de importación. Si cambias un modelo en Blender y lo reexportas con el mismo nombre, no hay que tocar nada.
    /// </summary>
    public static class ModelLinker
    {
        public const string ModelsRoot = "Assets/_Project/Art/Models";

        private static readonly Dictionary<string, string> RocketFiles = new Dictionary<string, string>
        {
            { "LV_FALCON9", "LV_Falcon9" }, { "LV_ELECTRON", "LV_Electron" }, { "LV_SLS_B1", "LV_SLS_Block1" },
            { "LV_ARIANE64", "LV_Ariane64" }, { "LV_RIDESHARE", "LV_Falcon9" }   // el rideshare viaja en un Falcon 9
        };

        private static readonly (string prefix, string file)[] GenericParts =
        {
            ("BAT_", "BAT_Generic"), ("CDH_", "CDH_Generic"), ("ADCS_", "ADCS_Generic")
        };

        [MenuItem("NASA SpaceApps/Link 3D Models To Catalog")]
        public static void LinkFromMenu()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>("Assets/_Project/Data/Generated/NasaDatabase.asset");
            if (db != null) Debug.Log($"[ModelLinker] {LinkAll(db)} modelos vinculados.");
        }

        public static int LinkAll(NasaDatabaseSO db)
        {
            ConfigureImporters();
            int linked = 0;
            foreach (var c in db.AllComponents)
            {
                var model = Load("Spacecraft", c.componentId);
                if (model == null)
                    foreach (var (prefix, file) in GenericParts)
                        if (c.componentId.StartsWith(prefix)) model = Load("Spacecraft", file);
                if (model == null || c.prefab == model) { if (model != null) linked++; continue; }
                c.prefab = model;
                EditorUtility.SetDirty(c);
                linked++;
            }
            foreach (var lv in db.launchVehicles)
            {
                if (lv == null || !RocketFiles.TryGetValue(lv.vehicleId, out var file)) continue;
                var model = Load("Rockets", file);
                if (model == null) continue;
                if (lv.prefab != model) { lv.prefab = model; EditorUtility.SetDirty(lv); }
                linked++;
            }
            AssetDatabase.SaveAssets();
            return linked;
        }

        public static GameObject Load(string category, string file) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsRoot}/{category}/{file}.fbx");

        /// <summary>Escala 1 m = 1 unidad, sin cámaras/luces/animación; materiales desde el FBX (URP Lit).</summary>
        private static void ConfigureImporters()
        {
            if (!Directory.Exists(ModelsRoot)) return;
            foreach (var path in Directory.GetFiles(ModelsRoot, "*.fbx", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter mi)) continue;
                bool dirty = false;
                if (mi.importCameras) { mi.importCameras = false; dirty = true; }
                if (mi.importLights) { mi.importLights = false; dirty = true; }
                if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
                if (!Mathf.Approximately(mi.globalScale, 1f)) { mi.globalScale = 1f; dirty = true; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                { mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; dirty = true; }
                if (dirty) mi.SaveAndReimport();
            }
        }
    }
}
