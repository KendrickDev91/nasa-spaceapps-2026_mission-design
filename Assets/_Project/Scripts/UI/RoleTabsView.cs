using MissionDesign.Core;
using MissionDesign.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>Pestañas de los 4 roles, botón de turno y selector de número de jugadores.</summary>
    public sealed class RoleTabsView : MonoBehaviour
    {
        [SerializeField] private RoleTurnManager roles;
        [Tooltip("Un botón por rol, en el orden de RoleTurnManager.AllRoles.")]
        [SerializeField] private Button[] roleButtons = new Button[4];
        [SerializeField] private Button nextTurnButton;
        [SerializeField] private Button playersButton;
        [SerializeField] private TMP_Text playersText;
        [SerializeField] private TMP_Text turnText;

        private void Start()
        {
            if (roles == null) return;
            for (int i = 0; i < roleButtons.Length && i < RoleTurnManager.AllRoles.Length; i++)
            {
                var role = RoleTurnManager.AllRoles[i];
                if (roleButtons[i] != null) roleButtons[i].onClick.AddListener(() => roles.SetActiveRole(role));
            }
            if (nextTurnButton != null) nextTurnButton.onClick.AddListener(roles.NextTurn);
            if (playersButton != null) playersButton.onClick.AddListener(() => roles.SetPlayerCount(roles.PlayerCount % 4 + 1));

            roles.ActiveRoleChanged += Refresh;
            Refresh(roles.ActiveRole, roles.PlayerForRole(roles.ActiveRole));
        }

        private void OnDestroy()
        {
            if (roles != null) roles.ActiveRoleChanged -= Refresh;
        }

        private void Refresh(MissionRole active, int player)
        {
            for (int i = 0; i < roleButtons.Length && i < RoleTurnManager.AllRoles.Length; i++)
            {
                var button = roleButtons[i];
                if (button == null) continue;
                var role = RoleTurnManager.AllRoles[i];
                bool isActive = role == active;
                var image = button.GetComponent<Image>();
                if (image != null) image.color = isActive ? UiTheme.Accent : UiTheme.PanelLight;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = $"<b>{RoleTurnManager.RoleDisplayName(role)}</b>\n<size=75%>{roles.PlayerName(roles.PlayerForRole(role))}</size>";
                    label.color = isActive ? Color.white : UiTheme.TextDim;
                }
            }
            if (playersText != null) playersText.text = Loc.L($"{roles.PlayerCount} jugador{(roles.PlayerCount > 1 ? "es" : "")}", $"{roles.PlayerCount} player{(roles.PlayerCount > 1 ? "s" : "")}");
            if (turnText != null) turnText.text = $"{Loc.L("Turno", "Turn")}: <b>{roles.PlayerName(player)}</b>";
        }
    }
}
