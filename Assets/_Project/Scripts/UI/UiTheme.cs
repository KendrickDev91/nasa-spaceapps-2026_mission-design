using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.UI
{
    /// <summary>Paleta única del HUD (cámbiala aquí para re-estilizar toda la interfaz).</summary>
    public static class UiTheme
    {
        public static readonly Color Background = new Color(0.02f, 0.03f, 0.07f, 1f);
        public static readonly Color Panel = new Color(0.05f, 0.08f, 0.14f, 0.97f);
        public static readonly Color PanelLight = new Color(0.09f, 0.13f, 0.22f, 1f);
        public static readonly Color Accent = new Color(0.20f, 0.62f, 1.00f, 1f);
        public static readonly Color AccentDim = new Color(0.12f, 0.30f, 0.52f, 1f);
        public static readonly Color Selected = new Color(0.13f, 0.42f, 0.30f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.95f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.70f, 0.82f, 1f);
        public static readonly Color Ok = new Color(0.30f, 0.85f, 0.45f, 1f);
        public static readonly Color Warning = new Color(1.00f, 0.76f, 0.20f, 1f);
        public static readonly Color Critical = new Color(1.00f, 0.34f, 0.30f, 1f);
        public static readonly Color Launch = new Color(0.85f, 0.30f, 0.12f, 1f);

        public static Color StateColor(ResourceState state) =>
            state == ResourceState.Critical ? Critical : state == ResourceState.Warning ? Warning : Ok;

        public static Color SeverityColor(IssueSeverity severity) =>
            severity == IssueSeverity.Blocking ? Critical : severity == IssueSeverity.Warning ? Warning : TextDim;

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static string RoleShort(MissionRole role)
        {
            switch (role)
            {
                case MissionRole.MissionDirector: return "DIR";
                case MissionRole.SystemsEngineer: return Loc.L("SIS", "SYS");
                case MissionRole.PrincipalScientist: return Loc.L("CIE", "SCI");
                case MissionRole.TelecomLaunchEngineer: return "TEL";
                default: return "?";
            }
        }
    }
}
