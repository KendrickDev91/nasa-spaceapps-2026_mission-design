using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Puente con el arte de Blender: cada vez que el diseño gana o pierde una pieza, instancia
    /// (o destruye) su prefab en el socket correspondiente con un efecto de escala y partículas.
    /// Si la pieza aún no tiene prefab, dibuja un placeholder primitivo para poder jugar ya.
    /// </summary>
    public sealed class ModularSpacecraftAssembler : MonoBehaviour
    {
        [Header("Sockets (Transforms hijos)")]
        [SerializeField] private Transform coreBusSocket;
        [SerializeField] private Transform topPayloadSocket;
        [SerializeField] private Transform solarWingLeft;
        [SerializeField] private Transform solarWingRight;
        [SerializeField] private Transform antennaSocket;
        [SerializeField] private Transform thrusterSocket;
        [SerializeField] private Transform internalBay;

        [Header("Efecto de aparición")]
        [SerializeField] private float popDuration = 0.35f;
        [SerializeField] private AnimationCurve popCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.7f, 1.12f), new Keyframe(1f, 1f));
        [Tooltip("Partículas opcionales que se instancian al montar una pieza.")]
        [SerializeField] private ParticleSystem spawnFxPrefab;

        [Header("Placeholders")]
        [SerializeField] private bool usePlaceholders = true;
        [SerializeField] private float instrumentSpacing = 0.7f;

        private sealed class Mounted
        {
            public SpacecraftComponentSO component;
            public readonly List<GameObject> instances = new List<GameObject>();
        }

        private readonly List<Mounted> mounted = new List<Mounted>();
        private MissionStateManager manager;
        private static Shader litShader;

        public int MountedCount => mounted.Count;
        public IEnumerable<GameObject> MountedInstances => mounted.SelectMany(m => m.instances);

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[Assembler] No hay MissionStateManager en la escena.", this);
                return;
            }
            manager.Design.ComponentAdded += Mount;
            manager.Design.ComponentRemoved += Unmount;
            foreach (var c in manager.Design.AllComponents) Mount(c);
        }

        private void OnDestroy()
        {
            if (manager == null || manager.Design == null) return;
            manager.Design.ComponentAdded -= Mount;
            manager.Design.ComponentRemoved -= Unmount;
        }

        /// <summary>Copia solo-visual de la nave montada (para otras vistas, p. ej. el sobrevuelo lunar).</summary>
        public GameObject CreateVisualCopy(Transform parent)
        {
            var root = new GameObject("SpacecraftCopy");
            root.transform.SetParent(parent, false);
            foreach (var go in MountedInstances)
            {
                if (go == null) continue;
                var c = Instantiate(go, root.transform);
                c.transform.localPosition = transform.InverseTransformPoint(go.transform.position);
                c.transform.localRotation = Quaternion.Inverse(transform.rotation) * go.transform.rotation;
                c.transform.localScale = go.transform.lossyScale;
            }
            return root;
        }

        public Transform GetSocket(SpacecraftSocket socket)
        {
            switch (socket)
            {
                case SpacecraftSocket.CoreBus: return coreBusSocket;
                case SpacecraftSocket.TopPayloadSocket: return topPayloadSocket;
                case SpacecraftSocket.SolarWingLeft: return solarWingLeft;
                case SpacecraftSocket.SolarWingRight: return solarWingRight;
                case SpacecraftSocket.AntennaSocket: return antennaSocket;
                case SpacecraftSocket.ThrusterSocket: return thrusterSocket;
                default: return internalBay;
            }
        }

        private void Mount(SpacecraftComponentSO component)
        {
            if (component == null) return;
            var entry = new Mounted { component = component };

            // Los paneles en ala se montan en pareja simétrica.
            if (component is SolarArraySO && component.socket == SpacecraftSocket.SolarWingLeft)
            {
                entry.instances.Add(Spawn(component, solarWingLeft, mirror: false));
                entry.instances.Add(Spawn(component, solarWingRight, mirror: true));
            }
            else
            {
                entry.instances.Add(Spawn(component, GetSocket(component.socket), mirror: false));
            }

            entry.instances.RemoveAll(go => go == null);
            mounted.Add(entry);
            LayoutInstruments();
        }

        private void Unmount(SpacecraftComponentSO component)
        {
            int index = mounted.FindLastIndex(m => m.component == component);
            if (index < 0) return;
            foreach (var go in mounted[index].instances)
                if (go != null) Destroy(go);
            mounted.RemoveAt(index);
            LayoutInstruments();
        }

        private GameObject Spawn(SpacecraftComponentSO component, Transform socket, bool mirror)
        {
            if (socket == null) socket = transform;

            GameObject go;
            if (component.prefab != null)
                go = Instantiate(component.prefab, socket);
            else if (usePlaceholders)
                go = CreatePlaceholder(component, socket);
            else
                return null;

            go.name = $"{component.componentId}{(mirror ? "_R" : "")}";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            if (mirror)
            {
                var s = go.transform.localScale;
                go.transform.localScale = new Vector3(-s.x, s.y, s.z);
            }

            StartCoroutine(PopIn(go.transform, go.transform.localScale));
            if (spawnFxPrefab != null)
            {
                var fx = Instantiate(spawnFxPrefab, socket.position, socket.rotation);
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }
            return go;
        }

        /// <summary>Reparte los instrumentos en fila sobre el TopPayloadSocket.</summary>
        private void LayoutInstruments()
        {
            var instruments = mounted.Where(m => m.component is InstrumentSO).SelectMany(m => m.instances)
                .Where(go => go != null).ToList();
            float start = -(instruments.Count - 1) * instrumentSpacing * 0.5f;
            for (int i = 0; i < instruments.Count; i++)
                instruments[i].transform.localPosition = new Vector3(start + i * instrumentSpacing, 0f, 0f);
        }

        private IEnumerator PopIn(Transform t, Vector3 targetScale)
        {
            float time = 0f;
            t.localScale = Vector3.zero;
            while (time < popDuration && t != null)
            {
                time += Time.deltaTime;
                t.localScale = targetScale * popCurve.Evaluate(Mathf.Clamp01(time / popDuration));
                yield return null;
            }
            if (t != null) t.localScale = targetScale;
        }

        // ───────────── Placeholders hasta tener los modelos de Blender ─────────────

        private static GameObject CreatePlaceholder(SpacecraftComponentSO c, Transform parent)
        {
            var root = new GameObject();
            root.transform.SetParent(parent, false);

            switch (c)
            {
                case SpacecraftBusSO bus:
                {
                    float size = Mathf.Clamp(Mathf.Pow(bus.massKg, 1f / 3f) / 6f, 0.7f, 2.2f);
                    AddPrimitive(root, PrimitiveType.Cube, Vector3.zero, new Vector3(size, size * 1.2f, size), new Color(0.78f, 0.62f, 0.22f));
                    break;
                }
                case SolarArraySO solar:
                {
                    float len = Mathf.Clamp(Mathf.Sqrt(solar.powerAt1AU_W) / 40f, 0.5f, 1.8f);
                    AddPrimitive(root, PrimitiveType.Cube, new Vector3(-len * 0.5f, 0f, 0f), new Vector3(len, 0.9f, 0.04f), new Color(0.10f, 0.22f, 0.55f));
                    AddPrimitive(root, PrimitiveType.Cylinder, new Vector3(-0.1f, 0f, 0f), new Vector3(0.05f, 0.1f, 0.05f), Color.gray, Quaternion.Euler(0, 0, 90));
                    break;
                }
                case AntennaSO antenna:
                {
                    float d = antenna.band == CommBand.Ka ? 1f : antenna.band == CommBand.X ? 0.6f : 0.3f;
                    AddPrimitive(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(d, d, d * 0.25f), new Color(0.9f, 0.9f, 0.92f));
                    AddPrimitive(root, PrimitiveType.Cylinder, new Vector3(0, 0, -d * 0.2f), new Vector3(0.04f, d * 0.2f, 0.04f), Color.gray, Quaternion.Euler(90, 0, 0));
                    break;
                }
                case BatterySO _:
                    AddPrimitive(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.2f, 0.3f), new Color(0.2f, 0.7f, 0.3f));
                    break;
                case InstrumentSO inst:
                {
                    float s = Mathf.Clamp(Mathf.Pow(inst.massKg, 1f / 3f) / 10f, 0.25f, 0.85f);
                    var color = inst.instrumentType == InstrumentType.Radar ? new Color(0.85f, 0.3f, 0.25f)
                        : inst.instrumentType == InstrumentType.Spectrometer ? new Color(0.6f, 0.35f, 0.8f)
                        : new Color(0.25f, 0.75f, 0.85f);
                    AddPrimitive(root, PrimitiveType.Cylinder, new Vector3(0, s * 0.5f, 0), new Vector3(s, s * 0.5f, s), color);
                    break;
                }
                default:
                    AddPrimitive(root, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.3f, Color.white);
                    break;
            }
            return root;
        }

        private static void AddPrimitive(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color,
            Quaternion? rot = null)
        {
            var p = GameObject.CreatePrimitive(type);
            var col = p.GetComponent<Collider>();
            if (col != null) Destroy(col);
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot ?? Quaternion.identity;
            p.transform.localScale = scale;

            if (litShader == null)
                litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(litShader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.3f);
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
