using System;
using PoeClone.Network.Replication;

namespace PoeClone.Network
{
    /// <summary>
    /// Co-op wire format, relayed by server/room.js between the two partners. "co" is a
    /// <see cref="StateSnapshot"/> (the host's world, or the guest's character) and "cev" a
    /// <see cref="CoopEvent"/>; the server forwards both as is, so "type" must stay first.
    /// </summary>
    public static class CoopProtocol
    {
        public const string Snapshot = "co";
        public const string Event = "cev";
        public const string SnapshotPrefix = "{\"type\":\"co\",";
        public const string EventPrefix = "{\"type\":\"cev\",";

        // CoopEvent.k values.
        public const string Hit = "hit";        // guest -> host: a hit on one of the host's enemies
        public const string Attack = "atk";     // host -> guest: an enemy attack to show or take
        public const string Kill = "kill";      // host -> guest: an enemy near the guest died
        public const string Drop = "drop";      // host -> guest: an item appeared on the ground
        public const string Gone = "gone";      // host -> guest: an item left the ground
        public const string Claim = "claim";    // guest -> host: wants an item on the ground
        public const string Granted = "got";    // host -> guest: it's yours
        public const string Denied = "deny";    // host -> guest: someone else got it first
        public const string Place = "put";      // guest -> host: put this item on the ground

        // CoopEvent.f bits.
        public const int FlagHead = 1;
        public const int FlagEnrage = 2;
        public const int FlagFlinch = 4;
        public const int FlagOrigin = 8;
        public const int FlagAtYou = 16;
        public const int FlagPops = 32;
    }

    /// <summary>One co-op event. Which fields mean what depends on <see cref="k"/>.</summary>
    [Serializable]
    public class CoopEvent
    {
        public string type = CoopProtocol.Event;
        public string k;
        public int id;        // enemy id (hit, atk) or drop id (drop, gone, claim, got, deny)
        public float a;       // damage, or gold amount
        public int dt;        // DamageType
        public float ap;      // armour / elemental penetration
        public float ep;
        public int f;         // CoopProtocol.Flag* bits
        public int n;         // attack: Party.AttackKind
        public int ek;        // enemy kind (attack, kill)
        public float r;       // attack: melee reach; kill: monster level
        public int xp;
        public float x, y, z;     // hit origin, attack start, kill/drop position
        public float tx, ty, tz;  // attack aim, drop's pop origin
        public GearItem it;       // drop, got, put: the item with all its stats
    }

    [Serializable]
    public class CoopHostMessage
    {
        public string type = "coopHost";
        public string name;
    }

    [Serializable]
    public class CoopListMessage
    {
        public string type = "coopList";
    }

    [Serializable]
    public class CoopJoinMessage
    {
        public string type = "coopJoin";
        public int id;
        public string name;
    }

    [Serializable]
    public class CoopLeaveMessage
    {
        public string type = "coopLeave";
    }
}
