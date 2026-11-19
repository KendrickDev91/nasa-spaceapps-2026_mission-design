using System;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Catálogo de piezas filtrado por el rol activo y la dificultad. Cada botón lleva un tooltip
    /// educativo con la vista previa de consecuencias ("Si lo eliges…"). Clonar plantillas permite
    /// re-estilizar el botón y el encabezado en el editor sin tocar código.
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
            public Func<bool> isSelected;
            public Button button;
            public Image background;
            public TMP_Text nameText;
            public string baseName;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private MissionStateManager manager;
        private DifficultyLevel lastDifficulty;
        private OrbitSO lastOrbit;
        private GroundNetworkSO lastNetwork;
        private MissionScenarioSO lastScenario;

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

        private void OnDesignEvaluated(TelemetryReport t, LaunchValidationResult v)
        {
            var d = manager.Design;
            // La dificultad cambia qué piezas existen; la órbita y la red, los GB/día de las antenas; el escenario, qué instrumentos son objetivo.
            if (d.Difficulty != lastDifficulty || d.Orbit != lastOrbit || d.GroundNetwork != lastNetwork || d.Scenario != lastScenario)
                Rebuild(roles.ActiveRole);
            RefreshSelection();
        }

        // ───────────── Construcción por rol ─────────────

        public void Rebuild(MissionRole role)
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();
            items.Clear();

            var db = manager.Database;
            var design = manager.Design;
            lastDifficulty = design.Difficulty;
            lastOrbit = design.Orbit;
            lastNetwork = design.GroundNetwork;
            lastScenario = design.Scenario;
            if (db == null) return;

            if (titleText != null) titleText.text = RoleTurnManager.RoleDisplayName(role).ToUpperInvariant();
            bool advanced = design.IsAdvanced;

            switch (role)
            {
                case MissionRole.MissionDirector:
                    SetHint(Loc.L("Define dificultad, objetivo científico y órbita; aprueba el presupuesto final. Pasa el ratón por cada opción para ver sus consecuencias.",
                        "Set the difficulty, science goal and orbit; approve the final budget. Hover over each option to see its consequences."));
                    Section(Loc.L("Dificultad", "Difficulty"));
                    foreach (var level in new[] { DifficultyLevel.Basic, DifficultyLevel.Advanced })
                    {
                        var l = level;
                        AddItem($"Difficulty_{l}", l == DifficultyLevel.Basic ? Loc.L("Básico", "Basic") : Loc.L("Avanzado", "Advanced"),
                            l == DifficultyLevel.Basic ? Loc.L("Piezas esenciales · 1 evento · el cohete no falla", "Essential parts · 1 event · the rocket never fails")
                                                    : Loc.L("+ computadora y control de orientación · 1-2 eventos · riesgo real de lanzamiento", "+ computer and attitude control · 1-2 events · real launch risk"),
                            () => manager.SetDifficulty(l), () => manager.Design.Difficulty == l, () => EducationalTips.ForDifficulty(l));
                    }
                    Section(Loc.L("Escenario / objetivo", "Scenario / goal"));
                    foreach (var s in db.scenarios)
                        AddData(s, s.displayName, Loc.L($"Límite ${s.budgetLimitMUSD:N0}M · {s.maxDevelopmentMonths:N0} meses · ciencia mín. {s.minScienceValue}", $"Limit ${s.budgetLimitMUSD:N0}M · {s.maxDevelopmentMonths:N0} months · min. science {s.minScienceValue}"),
                            () => manager.SelectScenario(s));
                    Section(Loc.L("Órbita", "Orbit"));
                    foreach (var o in db.orbits)
                        AddData(o, o.displayName, Loc.L($"Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contacto {o.dailyContactHours:N0} h/día", $"Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contact {o.dailyContactHours:N0} h/day"),
                            () => manager.SelectOrbit(o));
                    break;

                case MissionRole.SystemsEngineer:
                    SetHint(advanced
                        ? Loc.L("Elige estructura, energía, computadora y control de orientación. Vigila masa, energía y apuntamiento.",
                                "Choose the structure, power, computer and attitude control. Watch mass, power and pointing.")
                        : Loc.L("Elige estructura, energía y batería. Vigila masa y balance energético.",
                                "Choose the structure, power and battery. Watch mass and the power balance."));
                    Section(Loc.L("Bus satelital", "Spacecraft bus"));
                    foreach (var b in Visible(db.buses, advanced))
                        AddData(b, b.displayName, Loc.L($"{b.WetMassKg:N0} kg · carga útil {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · {b.payloadSlots} ranuras · ${b.costMUSD:N0}M", $"{b.WetMassKg:N0} kg · payload {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · {b.payloadSlots} slots · ${b.costMUSD:N0}M"),
                            () => manager.SelectComponent(b));
                    Section(Loc.L("Paneles solares", "Solar arrays"));
                    foreach (var s in Visible(db.solarArrays, advanced))
                        AddData(s, s.displayName, Loc.L($"{s.powerAt1AU_W:N0} W a 1 UA · {s.massKg:N0} kg · ${s.costMUSD:N0}M", $"{s.powerAt1AU_W:N0} W at 1 AU · {s.massKg:N0} kg · ${s.costMUSD:N0}M"),
                            () => manager.SelectComponent(s));
                    Section(Loc.L("Baterías", "Batteries"));
                    foreach (var b in Visible(db.batteries, advanced))
                        AddData(b, b.displayName, Loc.L($"{b.UsableWh:N0} Wh útiles · {b.massKg:N0} kg · ${b.costMUSD:N0}M", $"{b.UsableWh:N0} Wh usable · {b.massKg:N0} kg · ${b.costMUSD:N0}M"),
                            () => manager.SelectComponent(b));
                    if (advanced)
                    {
                        Section(Loc.L("Computadora de a bordo", "Onboard computer"));
                        foreach (var c in Visible(db.computers, true))
                            AddData(c, c.displayName, $"{c.storageGB:N0} GB · {(c.radiationHardened ? Loc.L("endurecida", "rad-hard") : Loc.L("comercial", "commercial"))} · {c.massKg:N0} kg · ${c.costMUSD:N1}M",
                                () => manager.SelectComponent(c));
                        Section(Loc.L("Control de orientación (ADCS)", "Attitude control (ADCS)"));
                        foreach (var a in Visible(db.attitudeControls, true))
                            AddData(a, a.displayName, $"±{a.pointingAccuracyDeg:0.###}° · {a.massKg:N0} kg · {a.peakPowerW:N0} W · ${a.costMUSD:N1}M",
                                () => manager.SelectComponent(a));
                    }
                    break;

                case MissionRole.PrincipalScientist:
                    SetHint(Loc.L("Clic para añadir o quitar instrumentos. Maximiza el valor científico sin saturar el enlace.",
                        "Click to add or remove instruments. Maximize the science value without saturating the downlink."));
                    Section(Loc.L("Instrumentos científicos (NASA)", "Science instruments (NASA)"));
                    var scenario = manager.Design.Scenario;
                    // Primero los que estudian el objetivo del escenario: valen el doble.
                    foreach (var i in Visible(db.instruments, advanced).OrderBy(x => MissionTelemetry.MatchesObjective(x, scenario) ? 0 : 1))
                    {
                        bool onTarget = MissionTelemetry.MatchesObjective(i, scenario);
                        string objective = scenario == null || scenario.isSandbox || string.IsNullOrEmpty(scenario.scienceObjectiveTag) ? ""
                            : onTarget ? $" · <color={UiTheme.Hex(UiTheme.Ok)}>{Loc.L("objetivo", "goal")}: {Loc.Tag(scenario.scienceObjectiveTag)}</color>"
                                       : $" · <color={UiTheme.Hex(UiTheme.Warning)}>{Loc.L("fuera del objetivo", "off-goal")} (×{MissionTelemetry.OffObjectiveScienceFactor:0.#})</color>";
                        AddData(i, i.displayName,
                            $"{i.massKg:N0} kg · {i.peakPowerW:N0} W · {i.dataVolumeGBPerDay:N0} {Loc.L("GB/día", "GB/day")} · {Loc.L("ciencia", "science")} {i.scienceValue} · {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}{objective}",
                            () => manager.ToggleInstrument(i));
                    }
                    break;

                case MissionRole.TelecomLaunchEngineer:
                    SetHint(Loc.L("Elige el cohete, la antena y la red de estaciones terrenas: capacidad de carga y enlace con tierra.",
                        "Choose the rocket, the antenna and the ground station network: payload capacity and the link to Earth."));
                    Section(Loc.L("Cohete lanzador", "Launch vehicle"));
                    foreach (var lv in db.launchVehicles)
                        AddData(lv, lv.displayName, RocketStats(lv), () => manager.SelectLaunchVehicle(lv));
                    Section(Loc.L("Antena", "Antenna"));
                    foreach (var a in Visible(db.antennas, advanced))
                    {
                        var orbit = design.Orbit;
                        string rate = orbit != null
                            ? Loc.L($"{orbit.GetDailyDownlinkGB(a, design.GroundNetwork):N1} GB/día en {orbit.regime}", $"{orbit.GetDailyDownlinkGB(a, design.GroundNetwork):N1} GB/day in {orbit.regime}")
                            : Loc.L($"banda {a.band}", $"{a.band} band");
                        AddData(a, a.displayName, $"{rate} · {a.massKg:N0} kg · {a.peakPowerW:N0} W{(a.dsnCompatible ? " · DSN" : "")}",
                            () => manager.SelectComponent(a));
                    }
                    Section(Loc.L("Red de estaciones terrenas", "Ground station network"));
                    foreach (var n in db.groundNetworks)
                    {
                        bool covers = design.Orbit == null || n.Supports(design.Orbit.regime);
                        AddData(n, n.displayName,
                            Loc.L($"{(covers ? "" : "NO CUBRE ESTA ÓRBITA · ")}ganancia ×{n.gainFactor:0.#} · contacto ×{n.contactHoursMultiplier:0.#} · ${n.costPerMonthMUSD:0.##}M/mes",
                                  $"{(covers ? "" : "DOES NOT COVER THIS ORBIT · ")}gain ×{n.gainFactor:0.#} · contact ×{n.contactHoursMultiplier:0.#} · ${n.costPerMonthMUSD:0.##}M/month"),
                            () => manager.SelectGroundNetwork(n));
                    }
                    break;
            }
        }

        private static IEnumerable<T> Visible<T>(IEnumerable<T> list, bool advanced) where T : SpacecraftComponentSO =>
            list.Where(c => c != null && (advanced || !c.advancedOnly));

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

        private void AddData(ScriptableObject data, string title, string stats, Action onClick) =>
            AddItem($"Item_{data.name}", title, stats, onClick, () => IsSelected(data),
                () => EducationalTips.For(data, manager.Design, manager.Database));

        private void AddItem(string objectName, string title, string stats, Action onClick, Func<bool> isSelected, Func<string> tooltip)
        {
            if (itemTemplate == null || content == null) return;
            var button = Instantiate(itemTemplate, content);
            button.gameObject.SetActive(true);
            button.name = objectName;
            var texts = button.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0) texts[0].text = title;
            if (texts.Length > 1) texts[1].text = stats;
            button.onClick.AddListener(() => onClick());

            var trigger = button.gameObject.AddComponent<TooltipTrigger>();
            trigger.Content = tooltip;

            items.Add(new Item
            {
                isSelected = isSelected, button = button, background = button.GetComponent<Image>(),
                nameText = texts.Length > 0 ? texts[0] : null, baseName = title
            });
            spawned.Add(button.gameObject);
        }

        private void RefreshSelection()
        {
            if (manager == null) return;
            foreach (var item in items)
            {
                bool selected = item.isSelected();
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
                case GroundNetworkSO n: return d.GroundNetwork == n;
                case InstrumentSO i: return d.Instruments.Contains(i);
                case SpacecraftComponentSO c: return d.AllComponents.Contains(c);
                default: return false;
            }
        }
    }
}
