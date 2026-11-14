using System;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Catálogo de piezas filtrado por el rol activo. Clonar plantillas permite re-estilizar
    /// el botón y el encabezado en el editor sin tocar código.
    /// </summary>
    public sealed class CatalogPanel : MonoBehaviour
    {
        [SerializeField] private RoleTurnManager roles;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private RectTransform content;
        [Tooltip("Botón desactivado con dos TMP_Text hijos: nombre y estadísticas.")]
        [SerializeField] private Button itemTemplate;
        [SerializeField] private TMP_Text sectionTemplate;

        private sealed class Item
        {
            public ScriptableObject data;
            public Button button;
            public Image background;
            public TMP_Text nameText;
            public string baseName;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private MissionStateManager manager;

        public int ItemCount => items.Count;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            if (sectionTemplate != null) sectionTemplate.gameObject.SetActive(false);
            if (manager == null || roles == null) return;

            roles.ActiveRoleChanged += OnRoleChanged;
            manager.DesignEvaluated += OnDesignEvaluated;
            OnRoleChanged(roles.ActiveRole, roles.PlayerForRole(roles.ActiveRole));
        }

        private void OnDestroy()
        {
            if (roles != null) roles.ActiveRoleChanged -= OnRoleChanged;
            if (manager != null) manager.DesignEvaluated -= OnDesignEvaluated;
        }

        private void OnRoleChanged(MissionRole role, int player)
        {
            Rebuild(role);
            RefreshSelection();
        }

        private void OnDesignEvaluated(Simulation.TelemetryReport t, Simulation.LaunchValidationResult v) => RefreshSelection();

        // ───────────── Construcción por rol ─────────────

        public void Rebuild(MissionRole role)
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();
            items.Clear();

            var db = manager.Database;
            if (db == null) return;

            if (titleText != null) titleText.text = RoleTurnManager.RoleDisplayName(role).ToUpperInvariant();

            switch (role)
            {
                case MissionRole.MissionDirector:
                    SetHint("Define el objetivo científico y la órbita; aprueba el presupuesto final.");
                    Section("Escenario / objetivo");
                    foreach (var s in db.scenarios)
                        AddItem(s, s.displayName, $"Límite ${s.budgetLimitMUSD:N0}M · {s.maxDevelopmentMonths:N0} meses · ciencia mín. {s.minScienceValue}",
                            () => manager.SelectScenario(s));
                    Section("Órbita");
                    foreach (var o in db.orbits)
                        AddItem(o, o.displayName, $"Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contacto {o.dailyContactHours:N0} h/día",
                            () => manager.SelectOrbit(o));
                    break;

                case MissionRole.SystemsEngineer:
                    SetHint("Elige estructura, energía y batería. Vigila masa y balance energético.");
                    Section("Bus satelital");
                    foreach (var b in db.buses)
                        AddItem(b, b.displayName, $"{b.WetMassKg:N0} kg · carga útil {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · {b.payloadSlots} ranuras · ${b.costMUSD:N0}M",
                            () => manager.SelectComponent(b));
                    Section("Paneles solares");
                    foreach (var s in db.solarArrays)
                        AddItem(s, s.displayName, $"{s.powerAt1AU_W:N0} W a 1 UA · {s.massKg:N0} kg · ${s.costMUSD:N0}M",
                            () => manager.SelectComponent(s));
                    Section("Baterías");
                    foreach (var b in db.batteries)
                        AddItem(b, b.displayName, $"{b.UsableWh:N0} Wh útiles · {b.massKg:N0} kg · ${b.costMUSD:N0}M",
                            () => manager.SelectComponent(b));
                    break;

                case MissionRole.PrincipalScientist:
                    SetHint("Clic para añadir o quitar instrumentos. Maximiza el valor científico sin saturar el enlace.");
                    Section("Instrumentos científicos (NASA)");
                    foreach (var i in db.instruments)
                        AddItem(i, i.displayName,
                            $"{i.massKg:N0} kg · {i.peakPowerW:N0} W · {i.dataVolumeGBPerDay:N0} GB/día · ciencia {i.scienceValue} · {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}",
                            () => manager.ToggleInstrument(i));
                    break;

                case MissionRole.TelecomLaunchEngineer:
                    SetHint("Elige el cohete y la antena: asegura la capacidad de carga y el enlace con la DSN.");
                    Section("Cohete lanzador");
                    foreach (var lv in db.launchVehicles)
                        AddItem(lv, lv.displayName, RocketStats(lv), () => manager.SelectLaunchVehicle(lv));
                    Section("Antena");
                    foreach (var a in db.antennas)
                    {
                        string rate = manager.Design.Orbit != null ? $"{manager.Design.Orbit.GetDailyDownlinkGB(a):N1} GB/día en {manager.Design.Orbit.regime}" : $"banda {a.band}";
                        AddItem(a, a.displayName, $"{rate} · {a.massKg:N0} kg · {a.peakPowerW:N0} W{(a.dsnCompatible ? " · DSN" : "")}",
                            () => manager.SelectComponent(a));
                    }
                    break;
            }
        }

        private static string RocketStats(LaunchVehicleSO lv)
        {
            var caps = lv.payloadCapacities.Where(c => c.payloadKg > 0f).Select(c => $"{c.regime} {c.payloadKg:N0}");
            return $"{string.Join(" · ", caps)} kg · ${lv.launchCostMUSD:N1}M · {lv.reliability:P0}";
        }

        private void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        private void Section(string title)
        {
            if (sectionTemplate == null || content == null) return;
            var header = Instantiate(sectionTemplate, content);
            header.gameObject.SetActive(true);
            header.text = title.ToUpperInvariant();
            spawned.Add(header.gameObject);
        }

        private void AddItem(ScriptableObject data, string title, string stats, Action onClick)
        {
            if (itemTemplate == null || content == null) return;
            var button = Instantiate(itemTemplate, content);
            button.gameObject.SetActive(true);
            button.name = $"Item_{data.name}";
            var texts = button.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0) texts[0].text = title;
            if (texts.Length > 1) texts[1].text = stats;
            button.onClick.AddListener(() => onClick());

            items.Add(new Item
            {
                data = data, button = button, background = button.GetComponent<Image>(),
                nameText = texts.Length > 0 ? texts[0] : null, baseName = title
            });
            spawned.Add(button.gameObject);
        }

        private void RefreshSelection()
        {
            if (manager == null) return;
            foreach (var item in items)
            {
                bool selected = IsSelected(item.data);
                if (item.background != null) item.background.color = selected ? UiTheme.Selected : UiTheme.PanelLight;
                if (item.nameText != null) item.nameText.text = selected ? $"<color={UiTheme.Hex(UiTheme.Ok)}>»</color> {item.baseName}" : item.baseName;
                item.button.interactable = manager.Phase == MissionPhase.Design;
            }
        }

        private bool IsSelected(ScriptableObject data)
        {
            var d = manager.Design;
            switch (data)
            {
                case MissionScenarioSO s: return d.Scenario == s;
                case OrbitSO o: return d.Orbit == o;
                case LaunchVehicleSO lv: return d.LaunchVehicle == lv;
                case InstrumentSO i: return d.Instruments.Contains(i);
                case SpacecraftComponentSO c: return d.AllComponents.Contains(c);
                default: return false;
            }
        }
    }
}
