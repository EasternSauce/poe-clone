using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.UI;

namespace PoeClone.Network
{
    // The start menu (Single Player / Co-op) and the co-op screens: host or join, the host's
    // wait for a partner and the list of open games. Every screen past the first has a Back.
    public partial class NamePromptUI
    {
        private static readonly Color BackColor = new Color(.32f, .32f, .36f, .98f);

        // While the start menu is up (DevTest picks single player through it).
        private Action onSinglePlayer;

        private Font MenuFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void ShowScreen()
        {
            onSinglePlayer = null;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") Destroy(child.gameObject);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>Hides the menus (the game is starting).</summary>
        public void HideScreens()
        {
            onSinglePlayer = null;
            canvasRoot.SetActive(false);
            HasConfirmed = true;
            PlayerHUD.SetHiddenBy(this, false);
        }

        private void MakeBack(Action back, float y)
        {
            MakeButton("Back", MenuFont, new Vector2(0, y), new Vector2(180, 50), back).GetComponent<Image>().color = BackColor;
        }

        public void ShowStartMenu(Action singlePlayer, Action coop, string notice = null)
        {
            ShowScreen();
            MakeText("Choose Mode", MenuFont, 34, new Vector2(0, 150), new Vector2(1000, 60));
            if (!string.IsNullOrEmpty(notice))
                MakeText(notice, MenuFont, 22, new Vector2(0, 95), new Vector2(1000, 40)).color = new Color(1f, .75f, .45f);
            MakeButton("Single Player", MenuFont, new Vector2(0, 20), new Vector2(320, 64), singlePlayer);
            MakeButton("Co-op", MenuFont, new Vector2(0, -60), new Vector2(320, 64), coop);
            onSinglePlayer = singlePlayer;
        }

        private void PickSinglePlayer()
        {
            onSinglePlayer?.Invoke();
        }

        public void ShowCoopMenu(Action host, Action join, Action back)
        {
            ShowScreen();
            MakeText("Co-op", MenuFont, 34, new Vector2(0, 150), new Vector2(1000, 60));
            MakeText("Play together with one other player over the internet.", MenuFont, 22, new Vector2(0, 100), new Vector2(1000, 40));
            MakeButton("Host Game", MenuFont, new Vector2(0, 20), new Vector2(320, 64), host);
            MakeButton("Join Game", MenuFont, new Vector2(0, -60), new Vector2(320, 64), join);
            MakeBack(back, -150);
        }

        /// <summary>A co-op status line ("Waiting for another player...", an error) with a Back.</summary>
        public void ShowCoopStatus(string title, string detail, Action back)
        {
            ShowScreen();
            MakeText(title, MenuFont, 34, new Vector2(0, 60), new Vector2(1200, 60));
            if (!string.IsNullOrEmpty(detail))
                MakeText(detail, MenuFont, 22, new Vector2(0, 10), new Vector2(1200, 40));
            MakeBack(back, -80);
        }

        public void ShowCoopLobby(IReadOnlyList<PlayerInfo> hosts, Action<PlayerInfo> join, Action back)
        {
            ShowScreen();
            MakeText("Join Game", MenuFont, 34, new Vector2(0, 330), new Vector2(1000, 60));
            float y = 230;
            if (hosts == null || hosts.Count == 0)
            {
                MakeText("Nobody is hosting right now. This list updates by itself.", MenuFont, 22, new Vector2(0, y), new Vector2(1000, 40));
                y -= 90;
            }
            else
            {
                MakeText("Games waiting for a second player:", MenuFont, 22, new Vector2(0, y + 40), new Vector2(1000, 40));
                foreach (PlayerInfo host in hosts)
                {
                    PlayerInfo chosen = host;
                    MakeButton(host.name + "'s game", MenuFont, new Vector2(0, y - 20), new Vector2(560, 64), () => join(chosen));
                    y -= 80;
                    if (y < -260) break;
                }
                y -= 30;
            }
            MakeBack(back, y - 10);
        }
    }
}
