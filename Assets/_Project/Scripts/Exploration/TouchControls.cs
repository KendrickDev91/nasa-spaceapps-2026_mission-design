using MissionDesign.Core;
using MissionDesign.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Controles táctiles del centro espacial (móvil y tablet, también en el navegador): joystick a la izquierda para
    /// caminar, arrastrar en la mitad derecha para girar la cámara y botones para correr, saltar, interactuar, mapa y menú.
    /// Se construye en tiempo de ejecución y solo aparece en pantallas táctiles. Los demás scripts leen los valores
    /// estáticos (<see cref="Move"/>, <see cref="LookDelta"/>…) además del teclado y el ratón.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public static Vector2 Move { get; private set; }
        public static bool Run { get; private set; }
        public static bool Active { get; private set; }

        private static Vector2 lookAccum;
        private static bool jumpPressed, interactPressed, menuPressed, mapPressed;

        /// <summary>Para las pruebas: muestra los controles aunque no haya pantalla táctil.</summary>
        public static bool ForceForTests;

        /// <summary>Arrastre acumulado desde la última lectura (en píxeles de pantalla).</summary>
        public static Vector2 ConsumeLook() { var d = lookAccum; lookAccum = Vector2.zero; return d; }
        public static bool ConsumeJump() { bool b = jumpPressed; jumpPressed = false; return b; }
        public static bool ConsumeInteract() { bool b = interactPressed; interactPressed = false; return b; }
        public static bool ConsumeMenu() { bool b = menuPressed; menuPressed = false; return b; }
        public static bool ConsumeMap() { bool b = mapPressed; mapPressed = false; return b; }

        /// <summary>Para las pruebas: simula el joystick y un arrastre de cámara.</summary>
        public static void SimulateForTests(Vector2 move, Vector2 look)
        {
            Move = move;
            lookAccum += look;
        }

        public static bool IsTouchDevice => ForceForTests || Application.isMobilePlatform || Touchscreen.current != null;

        private Button interactButton;
        private TextMeshProUGUI runLabel;
        private Image runImage;

        /// <summary>Crea los controles si el dispositivo es táctil. Devuelve null en escritorio sin pantalla táctil.</summary>
        public static TouchControls CreateIfTouch()
        {
            if (!IsTouchDevice) return null;
            var go = new GameObject("TouchControls");
            return go.AddComponent<TouchControls>();
        }

        private void Awake()
        {
            Active = true;
            Move = Vector2.zero; Run = false; lookAccum = Vector2.zero;
            jumpPressed = interactPressed = menuPressed = mapPressed = false;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;   // sobre el HUD, debajo de pausa, opciones y paneles (que van más arriba)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Zona para girar la cámara: mitad derecha, por debajo de la barra superior.
            var look = Rect("LookArea", transform);
            look.anchorMin = new Vector2(0.45f, 0f);
            look.anchorMax = new Vector2(1f, 0.82f);
            look.offsetMin = look.offsetMax = Vector2.zero;
            var lookImg = look.gameObject.AddComponent<Image>();
            lookImg.color = new Color(0f, 0f, 0f, 0f);
            look.gameObject.AddComponent<LookArea>();

            // Joystick.
            var baseRt = Rect("Joystick", transform);
            Place(baseRt, new Vector2(0f, 0f), new Vector2(230f, 230f), new Vector2(300f, 300f));
            var baseImg = baseRt.gameObject.AddComponent<Image>();
            baseImg.sprite = Circle();
            baseImg.color = new Color(1f, 1f, 1f, 0.12f);
            var knob = Rect("Knob", baseRt);
            Place(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            var knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.sprite = Circle();
            knobImg.color = new Color(0.62f, 0.78f, 1f, 0.55f);
            knobImg.raycastTarget = false;
            var stick = baseRt.gameObject.AddComponent<Joystick>();
            stick.knob = knob;

            // Botones (abajo a la derecha y arriba a la derecha).
            Button(Loc.L("Saltar", "Jump"), new Vector2(1f, 0f), new Vector2(-150f, 150f), 150f, () => jumpPressed = true);
            interactButton = Button(Loc.L("Usar", "Use"), new Vector2(1f, 0f), new Vector2(-330f, 110f), 150f, () => interactPressed = true);
            var run = Button(Loc.L("Correr", "Run"), new Vector2(1f, 0f), new Vector2(-180f, 330f), 130f, () => { Run = !Run; RefreshRun(); });
            runLabel = run.GetComponentInChildren<TextMeshProUGUI>();
            runImage = run.GetComponent<Image>();
            // Debajo del minimapa (arriba a la derecha) para no taparlo.
            Button(Loc.L("Menú", "Menu"), new Vector2(1f, 1f), new Vector2(-90f, -450f), 110f, () => menuPressed = true, "Touch_Menu");
            Button(Loc.L("Mapa", "Map"), new Vector2(1f, 1f), new Vector2(-220f, -450f), 110f, () => mapPressed = true, "Touch_Map");
            RefreshRun();
        }

        private void Start()
        {
            // La ayuda de abajo pasa de teclado y ratón a gestos.
            var hint = GameObject.Find("Controls");
            var t = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (t != null)
                t.text = Loc.L("Joystick: caminar · Arrastra a la derecha: mirar · Correr · Saltar · Usar (cerca de puertas y paneles) · Mapa · Menú",
                               "Joystick: walk · Drag on the right: look around · Run · Jump · Use (near doors and panels) · Map · Menu");
        }

        private void OnDisable()
        {
            // Si se abre un menú a mitad de un arrastre, el personaje no debe seguir caminando al volver.
            Move = Vector2.zero;
            lookAccum = Vector2.zero;
            var knob = transform.Find("Joystick/Knob") as RectTransform;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            Active = false;
            Move = Vector2.zero; Run = false;
        }

        /// <summary>El botón «Usar» solo se resalta cuando hay algo con qué interactuar.</summary>
        public void SetInteractAvailable(bool available)
        {
            if (interactButton == null) return;
            var img = interactButton.GetComponent<Image>();
            img.color = available ? new Color(UiTheme.Launch.r, UiTheme.Launch.g, UiTheme.Launch.b, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
        }

        private void RefreshRun()
        {
            if (runImage != null) runImage.color = Run ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.85f) : new Color(1f, 1f, 1f, 0.16f);
        }

        // ───────────── Piezas ─────────────

        private Button Button(string label, Vector2 anchor, Vector2 pos, float size, UnityEngine.Events.UnityAction onClick, string id = null)
        {
            var rt = Rect(id ?? "Touch_" + label, transform);
            Place(rt, anchor, pos, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle();
            img.color = new Color(1f, 1f, 1f, 0.16f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var t = Rect("Label", rt).gameObject.AddComponent<TextMeshProUGUI>();
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.text = $"<b>{label}</b>";
            t.fontSize = size * 0.22f;
            t.color = new Color(1f, 1f, 1f, 0.92f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return b;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Sprite circle;

        private static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        private sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public RectTransform knob;

            public void OnPointerDown(PointerEventData e) => OnDrag(e);

            public void OnDrag(PointerEventData e)
            {
                var rt = (RectTransform)transform;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
                float radius = rt.rect.width * 0.5f;
                var v = Vector2.ClampMagnitude(local / radius, 1f);
                knob.anchoredPosition = v * radius * 0.6f;
                // Zona muerta pequeña para que el personaje no camine solo.
                Move = v.magnitude < 0.12f ? Vector2.zero : v;
            }

            public void OnPointerUp(PointerEventData e)
            {
                knob.anchoredPosition = Vector2.zero;
                Move = Vector2.zero;
            }
        }

        private sealed class LookArea : MonoBehaviour, IDragHandler
        {
            public void OnDrag(PointerEventData e) => lookAccum += e.delta;
        }
    }
}
