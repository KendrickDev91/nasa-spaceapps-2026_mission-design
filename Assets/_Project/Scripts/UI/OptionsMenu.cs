using System;
using System.Collections.Generic;
using MissionDesign.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Pantalla de Opciones (vídeo, audio y controles). Se construye por código sobre todo lo demás, así funciona igual
    /// desde el menú principal, la pausa y el centro espacial. Cada cambio se aplica y se guarda al instante.
    /// </summary>
    public sealed class OptionsMenu : MonoBehaviour
    {
        private static OptionsMenu instance;

        public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

        private readonly List<Action> refreshers = new List<Action>();
        private List<Vector2Int> resolutions;

        /// <summary>Abre las Opciones (las crea la primera vez en la escena actual).</summary>
        public static void Open()
        {
            if (instance == null)
            {
                var go = new GameObject("OptionsMenu");
                instance = go.AddComponent<OptionsMenu>();
                instance.Build();
            }
            instance.gameObject.SetActive(true);
            instance.Refresh();
        }

        public static void Close()
        {
            if (instance == null) return;
            GameSettings.Save();
            instance.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ───────────── Construcción ─────────────

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var card = Rect("Card", dim);
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 980f));
            card.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.11f, 1f);

            var title = Label("Title", card, Loc.L("<b>OPCIONES</b>", "<b>OPTIONS</b>"), 36, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 50f));

            resolutions = GameSettings.AvailableResolutions();
            float y = -120f;

            Section(card, Loc.L("IDIOMA", "LANGUAGE"), ref y);
            Stepper(card, "Idioma / Language", ref y,
                () => Loc.LanguageName(Loc.Current),
                _ =>
                {
                    GameSettings.Save();
                    Loc.SetLanguage(Loc.English ? GameLanguage.Spanish : GameLanguage.English);
                });
            Section(card, Loc.L("VÍDEO", "VIDEO"), ref y);
            Stepper(card, Loc.L("Modo de pantalla", "Display mode"), ref y,
                () => GameSettings.DisplayModeLabel(GameSettings.DisplayMode),
                d =>
                {
                    int i = Array.IndexOf(GameSettings.DisplayModes, GameSettings.DisplayMode);
                    i = (Math.Max(0, i) + d + GameSettings.DisplayModes.Length) % GameSettings.DisplayModes.Length;
                    GameSettings.SetDisplay(GameSettings.DisplayModes[i], CurrentResolution());
                });
            Stepper(card, Loc.L("Resolución", "Resolution"), ref y,
                () => { var r = CurrentResolution(); return $"{r.x} × {r.y}"; },
                d =>
                {
                    int i = resolutions.IndexOf(CurrentResolution());
                    i = Mathf.Clamp((i < 0 ? resolutions.Count - 1 : i) + d, 0, resolutions.Count - 1);
                    GameSettings.SetDisplay(GameSettings.DisplayMode, resolutions[i]);
                });
            Stepper(card, Loc.L("Calidad gráfica", "Graphics quality"), ref y,
                () => QualityName(QualitySettings.GetQualityLevel()),
                d => GameSettings.SetQuality(Mathf.Clamp(QualitySettings.GetQualityLevel() + d, 0, QualitySettings.names.Length - 1)));
            Stepper(card, Loc.L("Sincronización vertical (VSync)", "Vertical sync (VSync)"), ref y,
                () => GameSettings.VSync ? Loc.L("Activada", "On") : Loc.L("Desactivada", "Off"),
                _ => GameSettings.SetVSync(!GameSettings.VSync));

            Section(card, "AUDIO", ref y);
            SliderRow(card, Loc.L("Volumen general", "Master volume"), ref y, 0f, 1f, () => GameSettings.MasterVolume, GameSettings.SetMasterVolume, v => $"{v:P0}");
            SliderRow(card, Loc.L("Efectos de sonido", "Sound effects"), ref y, 0f, 1f, () => GameSettings.SfxVolume, GameSettings.SetSfxVolume, v => $"{v:P0}");

            Section(card, Loc.L("CONTROLES (centro espacial)", "CONTROLS (space center)"), ref y);
            SliderRow(card, Loc.L("Sensibilidad del ratón", "Mouse sensitivity"), ref y, 0.2f, 3f, () => GameSettings.MouseSensitivity, GameSettings.SetMouseSensitivity, v => $"×{v:0.0}");
            Stepper(card, Loc.L("Invertir eje vertical", "Invert vertical axis"), ref y,
                () => GameSettings.InvertY ? Loc.L("Sí", "Yes") : "No",
                _ => GameSettings.SetInvertY(!GameSettings.InvertY));

            var note = Label("Note", card, Loc.L("Los cambios se aplican y se guardan al instante.", "Changes are applied and saved instantly."), 17, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(48f, 52f), new Vector2(460f, 40f), new Vector2(0f, 0.5f));
            var reset = MakeButton(card, Loc.L("Restablecer", "Reset"), UiTheme.PanelLight, () => { GameSettings.ResetToDefaults(); Refresh(); });
            Place((RectTransform)reset.transform, new Vector2(1f, 0f), new Vector2(-250f, 52f), new Vector2(190f, 54f), new Vector2(1f, 0.5f));
            var close = MakeButton(card, Loc.L("<b>Cerrar</b>", "<b>Close</b>"), UiTheme.Accent, Close);
            Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(-44f, 52f), new Vector2(190f, 54f), new Vector2(1f, 0.5f));
        }

        private static Vector2Int CurrentResolution() =>
            GameSettings.ResolutionWidth > 0
                ? new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight)
                : new Vector2Int(Screen.width, Screen.height);

        private static string QualityName(int level)
        {
            string n = level >= 0 && level < QualitySettings.names.Length ? QualitySettings.names[level] : "?";
            return n switch
            {
                "Very Low" => Loc.L("Muy baja", "Very low"), "Low" => Loc.L("Baja", "Low"), "Medium" => Loc.L("Media", "Medium"),
                "High" => Loc.L("Alta", "High"), "Very High" => Loc.L("Muy alta", "Very high"), "Ultra" => "Ultra", _ => n
            };
        }

        private void Refresh()
        {
            foreach (var r in refreshers) r();
        }

        private void Section(RectTransform card, string text, ref float y)
        {
            y -= 8f;
            var t = Label("Section", card, $"<b>{text}</b>", 18, UiTheme.Accent, TextAlignmentOptions.BottomLeft);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(48f, y), new Vector2(840f, 30f), new Vector2(0f, 1f));
            y -= 38f;
        }

        private void Stepper(RectTransform card, string label, ref float y, Func<string> value, Action<int> change)
        {
            RowLabel(card, label, y);
            var prev = MakeButton(card, "‹", UiTheme.AccentDim, () => { change(-1); Refresh(); });
            Place((RectTransform)prev.transform, new Vector2(1f, 1f), new Vector2(-428f, y), new Vector2(52f, 46f), new Vector2(0f, 1f));
            var box = Rect("Value", card);
            Place(box, new Vector2(1f, 1f), new Vector2(-370f, y), new Vector2(260f, 46f), new Vector2(0f, 1f));
            box.gameObject.AddComponent<Image>().color = UiTheme.PanelLight;
            var text = Label("Text", box, "", 20, UiTheme.Text, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            var next = MakeButton(card, "›", UiTheme.AccentDim, () => { change(1); Refresh(); });
            Place((RectTransform)next.transform, new Vector2(1f, 1f), new Vector2(-104f, y), new Vector2(52f, 46f), new Vector2(0f, 1f));
            refreshers.Add(() => text.text = value());
            y -= 56f;
        }

        private void SliderRow(RectTransform card, string label, ref float y, float min, float max,
            Func<float> get, Action<float> set, Func<float, string> format)
        {
            RowLabel(card, label, y);
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(1f, 1f), new Vector2(-428f, y - 13f), new Vector2(300f, 20f), new Vector2(0f, 1f));
            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            foreach (var img in go.GetComponentsInChildren<Image>())
                img.color = img.name == "Background" ? UiTheme.PanelLight : img.name == "Fill" ? UiTheme.Accent : UiTheme.Text;
            var valueText = Label("Value", card, "", 20, UiTheme.Text, TextAlignmentOptions.MidlineRight);
            Place(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, y), new Vector2(100f, 46f), new Vector2(1f, 1f));
            bool syncing = false;
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = format(v);
                if (!syncing) set(v);
            });
            refreshers.Add(() =>
            {
                syncing = true;
                slider.SetValueWithoutNotify(get());
                valueText.text = format(get());
                syncing = false;
            });
            y -= 56f;
        }

        private void RowLabel(RectTransform card, string label, float y)
        {
            var t = Label("Label", card, label, 21, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(64f, y), new Vector2(400f, 46f), new Vector2(0f, 1f));
        }

        // ───────────── Ayudas de UI ─────────────

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static Button MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var t = Label("Label", rt, label, 22, UiTheme.Text, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            return b;
        }
    }
}
