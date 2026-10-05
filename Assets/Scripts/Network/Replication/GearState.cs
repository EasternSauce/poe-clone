using System;

namespace PoeClone.Network.Replication
{
    // What the player's menus show, so a spectator can open the same ones and read the same item
    // stats. Unlike StateSnapshot this is sent only when something in it changes (an item moved,
    // a passive taken, the stash opened...), and the server keeps the latest one for spectators
    // who join or switch players later. Short keys and JsonUtility-compatible, like StateSnapshot.

    /// <summary>One item with everything its tooltip needs.</summary>
    [Serializable]
    public class GearItem
    {
        public string i;    // base id (icon / look)
        public string n;    // name
        public int ct;      // stack count
        public int t;       // ItemType
        public int w;
        public int h;
        public int wp;      // WeaponType
        public int q;       // ItemRarity
        public int c;       // 1 = has a cape
        public string tn;   // tint, RRGGBBAA
        public float[] m;   // modifiers as (stat, value) pairs
        public int x;       // grid position, or the EquipSlot for worn gear
        public int y;
    }

    [Serializable]
    public class QuestProgressState
    {
        public string id;
        public int progress;
    }

    [Serializable]
    public class GearState
    {
        public const string MessageType = "gear";

        public string type = MessageType;
        public int pid;          // stamped by the server, like StateSnapshot.pid
        public GearItem[] bag;
        public GearItem[] eq;    // x = EquipSlot
        public int so;           // 1 = the stash / trader panel is open (side is its contents)
        public GearItem[] side;  // the stash or the trader's goods, while that panel is open
        public string sn;        // the side panel's title (trader name)
        public int st;           // stash tab showing
        public string[] stn;     // stash tab names ("" = the default "Tab N")
        public GearItem held;    // the item on the player's cursor (n empty = none)
        public GearItem[] gnd;   // items on the ground near the player, x = drop id (for spectators' tooltips)
        public int gold;
        public int hpot;
        public int mpot;

        // Base stats (before gear and passives) - the character page builds its numbers from these.
        public int lv;
        public int xp;
        public int xpr;
        public float str;
        public float dex;
        public float itl;
        public float life;
        public float mana;

        public string[] pas;     // passives taken
        public int rc;           // passive respec charges
        public int[] sk;         // skill per slot (SkillId), -1 = empty
        public string[] qdone;   // quests handed in
        public QuestProgressState[] qactive; // quests taken, including those ready to hand in
    }

    /// <summary>
    /// Which menus the player has open and what their pointer is on, carried in every
    /// StateSnapshot (it changes with every mouse move, and is tiny).
    /// </summary>
    [Serializable]
    public class UiState
    {
        public const int HoverNone = 0;
        public const int HoverSlot = 1;    // hs = EquipSlot
        public const int HoverBag = 2;     // hx, hy = position in bag cells
        public const int HoverSide = 3;    // hx, hy = position in stash/trader cells
        public const int HoverPotion = 4;  // hs = 1 health, 2 mana

        public const int SideNone = 0;
        public const int SideStash = 1;
        public const int SideTrader = 2;

        public int inv;    // 1 = inventory open
        public int side;   // SideNone / SideStash / SideTrader
        public int chr;    // 1 = character page open
        public int tree;   // 1 = passive tree open
        public int skl;    // 1 = skills panel open
        public int hk;     // Hover* kind
        public int hs;
        public float hx;
        public float hy;
        public float px;   // pointer position as a fraction of the screen (for a held item over nothing)
        public float py;
        public string tn;  // passive under the pointer (tree open), "" = none
    }
}
