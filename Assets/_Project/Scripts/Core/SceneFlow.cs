using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionDesign.Core
{
    /// <summary>Navegación entre el centro espacial (exploración) y la sala de diseño de misión.</summary>
    public static class SceneFlow
    {
        public const string MissionDesignScene = "MissionDesign_Main";
        public const string SpaceCenterScene = "SpaceCenter";

        private static bool skipMainMenuOnce;

        /// <summary>Entra a la sala de diseño. Si viene del centro espacial, se salta el menú principal.</summary>
        public static void OpenMissionDesign(bool skipMainMenu = true)
        {
            skipMainMenuOnce = skipMainMenu;
            Time.timeScale = 1f;
            SceneManager.LoadScene(MissionDesignScene);
        }

        public static void OpenSpaceCenter()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SpaceCenterScene);
        }

        /// <summary>Devuelve true una sola vez tras <see cref="OpenMissionDesign"/>.</summary>
        public static bool ConsumeSkipMainMenu()
        {
            bool skip = skipMainMenuOnce;
            skipMainMenuOnce = false;
            return skip;
        }
    }
}
