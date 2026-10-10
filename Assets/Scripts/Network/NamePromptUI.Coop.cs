using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.Network
{
    // The start menu (Single Player / Co-op) and the co-op screens: host or join, the host's
    // wait for a partner and the list of open games. Every screen past the first has a Back.
    public partial class NamePromptUI
    {
        // While the start menu is up (DevTest picks single player through it).
        private Action onSinglePlayer;

        // A screen that rebuilds itself with the same key (the self-updating lobby) skips the fade-in.
        private void ShowScreen(string key = null)
        {
            onSinglePlayer = null;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            ClearScreen(key);
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
            MakeButton("Back", new Vector2(0, y), new Vector2(180, 50), back, UiKit.MutedTint);
        }

        public void ShowStartMenu(Action singlePlayer, Action coop, string notice = null)
        {
            ShowScreen();
            MakeText("Choose Mode", 34, new Vector2(0, 150), new Vector2(1000, 60));
            if (!string.IsNullOrEmpty(notice))
                MakeText(notice, 22, new Vector2(0, 95), new Vector2(1000, 40)).color = new Color(1f, .75f, .45f);
            MakeButton("Single Player", new Vector2(0, 20), new Vector2(320, 64), singlePlayer);
            MakeButton("Co-op", new Vector2(0, -60), new Vector2(320, 64), coop);
            onSinglePlayer = singlePlayer;
        }

        private void PickSinglePlayer()
        {
            onSinglePlayer?.Invoke();
        }

        public void ShowCoopMenu(Action host, Action join, Action back)
        {
            ShowScreen();
            MakeText("Co-op", 34, new Vector2(0, 150), new Vector2(1000, 60));
            MakeText("Play together with one other player over the internet.", 22, new Vector2(0, 100), new Vector2(1000, 40));
            MakeButton("Host Game", new Vector2(0, 20), new Vector2(320, 64), host);
            MakeButton("Join Game", new Vector2(0, -60), new Vector2(320, 64), join);
            MakeBack(back, -150);
        }

        /// <summary>A co-op status line ("Waiting for another player...", an error) with a Back.</summary>
        public void ShowCoopStatus(string title, string detail, Action back)
        {
            ShowScreen();
            MakeText(title, 34, new Vector2(0, 60), new Vector2(1200, 60));
            // Still working on it ("Connecting...", "Waiting for another player..."): show it's alive.
            if (title.EndsWith("..."))
                Appear(UiKit.RuneSpinner(canvasRoot.transform, new Vector2(0, 220), 120f));
            if (!string.IsNullOrEmpty(detail))
                MakeText(detail, 22, new Vector2(0, 10), new Vector2(1200, 40));
            MakeBack(back, -80);
        }

        public void ShowCoopLobby(IReadOnlyList<PlayerInfo> hosts, Action<PlayerInfo> join, Action back)
        {
            ShowScreen("lobby");
            MakeText("Join Game", 34, new Vector2(0, 330), new Vector2(1000, 60));
            float y = 230;
            if (hosts == null || hosts.Count == 0)
            {
                MakeText("Nobody is hosting right now. This list updates by itself.", 22, new Vector2(0, y), new Vector2(1000, 40));
                y -= 90;
            }
            else
            {
                MakeText("Games waiting for a second player:", 22, new Vector2(0, y + 40), new Vector2(1000, 40));
                foreach (PlayerInfo host in hosts)
                {
                    PlayerInfo chosen = host;
                    MakeButton(host.name + "'s game", new Vector2(0, y - 20), new Vector2(560, 64), () => join(chosen));
                    y -= 80;
                    if (y < -260) break;
                }
                y -= 30;
            }
            MakeBack(back, y - 10);
        }
    }
}
