using UnityEngine;
using UnityEngine.InputSystem;

namespace PoeClone.Inventory
{
    /// <summary>
    /// A spectator's view of the watched player's menus. The spectator's own InventoryUI,
    /// CharacterPageUI, PassiveTreeUI and SkillBarUI show the player's copies read-only: they open
    /// when the player opens them, and point at what the player points at - until the spectator
    /// moves their own mouse, which hands them the pointer (see <see cref="FollowingPlayer"/>).
    /// Spectators can also open the menus themselves (I, C, P, K) while the player has them shut.
    /// Filled by Network.SpectatorReplica (this assembly can't reference it).
    /// </summary>
    public static class SpectatorMirror
    {
        // Same values as Network.Replication.UiState.Hover*.
        public const int HoverNone = 0;
        public const int HoverSlot = 1;
        public const int HoverBag = 2;
        public const int HoverSide = 3;
        public const int HoverPotion = 4;

        public const int SideNone = 0;
        public const int SideStash = 1;
        public const int SideTrader = 2;

        // After the spectator's own mouse stops moving for this long, the menus follow the
        // player's pointer again.
        private const float LocalControlSeconds = 2f;
        private const float MoveThresholdPixels = 3f;

        /// <summary>This tab is a spectator's: the menus mirror the watched player's.</summary>
        public static bool Active;

        // The player's menus.
        public static bool InventoryOpen;
        public static bool CharacterOpen;
        public static bool TreeOpen;
        public static bool SkillsOpen;
        public static int Side;
        public static VendorStock Trader;   // the side panel's goods while Side == SideTrader

        // What the player's pointer is on.
        public static int HoverKind;
        public static int HoverIndex;       // EquipSlot, or potion slot 1/2
        public static Vector2 HoverCell;    // bag / side grid position, in cells
        public static Vector2 Pointer;      // fraction of the screen
        public static string TreeHover;     // passive id, or null
        public static ItemData Held;        // on the player's cursor

        private static Vector2 lastMouse;
        private static bool hasMouse;
        private static float lastMoveAt = float.NegativeInfinity;
        private static int polledFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Active = false;
            ClearRemote();
            for (int k = 0; k < MenuCount; k++)
                own[k] = dismissed[k] = wasRemote[k] = false;
            hasMouse = false;
            lastMoveAt = float.NegativeInfinity;
            polledFrame = -1;
        }

        /// <summary>No player to mirror (left, or switching): every mirrored menu closes.</summary>
        public static void ClearRemote()
        {
            InventoryOpen = CharacterOpen = TreeOpen = SkillsOpen = false;
            Side = SideNone;
            Trader = null;
            HoverKind = HoverNone;
            HoverIndex = 0;
            HoverCell = Vector2.zero;
            Pointer = new Vector2(0.5f, 0.5f);
            TreeHover = null;
            Held = null;
        }

        // ------------------------------------------------------------------ the spectator's own say

        /// <summary>The four mirrored menus, for <see cref="Shown"/>, <see cref="Toggle"/> and <see cref="Close"/>.</summary>
        public enum Menu
        {
            Inventory,
            Character,
            Tree,
            Skills
        }

        private const int MenuCount = 4;

        // Per menu: opened by the spectator themselves; the player's open copy shut by the spectator
        // (for them only - it stays open for the player); whether the player had it open last frame.
        private static readonly bool[] own = new bool[MenuCount];
        private static readonly bool[] dismissed = new bool[MenuCount];
        private static readonly bool[] wasRemote = new bool[MenuCount];

        private static bool Remote(Menu menu)
        {
            switch (menu)
            {
                case Menu.Inventory: return InventoryOpen;
                case Menu.Character: return CharacterOpen;
                case Menu.Tree: return TreeOpen;
                default: return SkillsOpen;
            }
        }

        /// <summary>
        /// Whether the spectator's copy of a menu shows: the player has it open (unless the spectator
        /// shut it - until the player opens it again), or the spectator opened it themselves.
        /// The tree and the skills panel share the middle of the screen, so one hides the other.
        /// </summary>
        public static bool Shown(Menu menu)
        {
            int k = (int)menu;
            bool remote = Remote(menu);
            if (remote != wasRemote[k])
            {
                // The player just opened it (show it again) or shut it (nothing left to dismiss).
                dismissed[k] = false;
                wasRemote[k] = remote;
            }
            if (!ShownRaw(menu))
                return false;

            // Both middle panels wanted (the player opened one while the spectator had the other
            // up): the spectator's own choice wins, else the skills panel.
            if (menu == Menu.Tree && ShownRaw(Menu.Skills))
                return own[(int)Menu.Tree];
            if (menu == Menu.Skills && ShownRaw(Menu.Tree))
                return !own[(int)Menu.Tree];
            return true;
        }

        private static bool ShownRaw(Menu menu)
        {
            int k = (int)menu;
            return (Remote(menu) && !dismissed[k]) || own[k];
        }

        /// <summary>Whether what shows is the player's copy (it follows their pointer), not one the spectator opened.</summary>
        public static bool ShowsRemote(Menu menu)
        {
            return Remote(menu) && !dismissed[(int)menu];
        }

        /// <summary>The spectator's button or key for a menu: shows it if hidden, hides it if showing.</summary>
        public static void Toggle(Menu menu)
        {
            if (Shown(menu))
            {
                Close(menu);
                return;
            }
            int k = (int)menu;
            own[k] = true;
            dismissed[k] = false;
            // One panel in the middle at a time.
            if (menu == Menu.Tree)
                Close(Menu.Skills);
            else if (menu == Menu.Skills)
                Close(Menu.Tree);
        }

        /// <summary>Hides the spectator's copy (the panel's X, Escape); the player's stays open for them.</summary>
        public static void Close(Menu menu)
        {
            int k = (int)menu;
            own[k] = false;
            if (Remote(menu))
                dismissed[k] = true;
        }

        public static void CloseAll()
        {
            for (int k = 0; k < MenuCount; k++)
                Close((Menu)k);
        }

        /// <summary>
        /// True while the spectator leaves their own pointer alone, so the menus show what the
        /// player is pointing at; false for a while after they move the mouse or touch the screen.
        /// </summary>
        public static bool FollowingPlayer
        {
            get
            {
                Poll();
                return Time.unscaledTime - lastMoveAt > LocalControlSeconds;
            }
        }

        private static void Poll()
        {
            if (polledFrame == Time.frameCount)
                return;
            polledFrame = Time.frameCount;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (hasMouse && (pos - lastMouse).sqrMagnitude > MoveThresholdPixels * MoveThresholdPixels)
                    lastMoveAt = Time.unscaledTime;
                lastMouse = pos;
                hasMouse = true;
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                lastMoveAt = Time.unscaledTime;
        }
    }
}
