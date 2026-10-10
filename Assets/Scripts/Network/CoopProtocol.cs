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
        public const string Skill = "skl";      // host -> guest: an enemy used its special skill
        public const string BossMove = "bmv";   // host -> guest: a boss started one of its moves
        public const string Damage = "dmg";     // host -> guest: a boss blow judged on the host landed on you
        public const string Effect = "fx";      // host -> guest: a boss's warning or projectile to show
        public const string World = "wld";      // guest -> host: the monsters round the guest, as the host arrives

        // CoopEvent.f bits.
        public const int FlagHead = 1;
        public const int FlagEnrage = 2;
        public const int FlagFlinch = 4;
        public const int FlagOrigin = 8;
        public const int FlagAtYou = 16;
        public const int FlagPops = 32;
        public const int FlagAttack = 64;
    }

    /// <summary>One co-op event. Which fields mean what depends on <see cref="k"/>.</summary>
    [Serializable]
    public class CoopEvent
    {
        public string type = CoopProtocol.Event;
        public string k;
        public int ar;        // area where this event happened
        public int id;        // enemy id (hit, atk) or drop id (drop, gone, claim, got, deny)
        public float a;       // damage, or gold amount
        public int dt;        // DamageType
        public float ap;      // armour / elemental penetration
        public float ep;
        public int f;         // CoopProtocol.Flag* bits
        public int n;         // attack: Party.AttackKind; effect: Party.EffectKind
        public int ek;        // enemy kind (attack, kill, skill, boss move)
        public float r;       // attack: melee reach; kill: monster level
        public int xp;
        public float x, y, z;     // hit origin, attack start, kill/drop position, blow's source
        public float tx, ty, tz;  // attack/skill aim, drop's pop origin
        public GearItem it;       // drop, got, put: the item with all its stats
        public int mv;            // boss move: which (Party.BossMoveCloser / BossMoveRoar for those)
        public int sd;            // boss move: random seed, so both games roll the same spots
        public float sp;          // boss move: attack speed multiplier (rage, haste)
        public float dm;          // boss move: damage multiplier (rage)
        public int lv;            // boss move: monster level; world: the area
        public float[] v;         // effect: its numbers (see Party.EffectKind)
        public EnemyHandoff[] w;  // world: the monsters handed over
    }

    /// <summary>A monster handed from the guest's game to the host's (see CoopProtocol.World).</summary>
    [Serializable]
    public class EnemyHandoff
    {
        public int k;         // kind
        public int lv;        // level
        public float x, y, z, r;
        public float hp;      // share of its life left (0-1)
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
