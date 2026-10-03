using System;

namespace PoeClone.Network.Replication
{
    // Wire format for the spectator "live cam": the player's client sends one of these ~10 times a
    // second, the server relays it, and spectators replay it through SnapshotTimeline. Field names
    // are deliberately one/two letters - this goes out 10x/s to every spectator, so the JSON keys
    // are most of the payload. Kept JsonUtility-compatible (plain [Serializable] classes, arrays,
    // no dictionaries/polymorphism) so the receiving side can parse it with no extra library.

    /// <summary>One character (the player or an enemy) at one instant.</summary>
    [Serializable]
    public class EntityState
    {
        public int i;      // stable id (enemy instance id; 0 for the player)
        public float x;    // world position
        public float y;
        public float z;
        public float r;    // yaw in degrees (characters only ever rotate around Y while alive)
        public float hp;
        public float mhp;
        public int d;      // 1 = dead
        public int atk;    // attacks started so far - a counter, so a swing that starts and finishes between two snapshots is never lost
        public int ap;     // attack profile id of the latest swing (see CharacterAttackAnimator.ProfileId)
        public int stg;    // staggers (non-lethal hits taken) so far - same counter trick as atk
        public int ch;     // enemies: 1 = chasing the player (drives the aggro sound)
        public int k;      // enemies: kind (index into EnemyKinds) - decides the look
        public int sk;     // enemies: special skills used so far (EnemySkills) - same counter trick as atk
        public float sx;   // ...and where the latest one was aimed
        public float sz;
        public int en;     // enemies: 1 = enraged

        public EntityState Clone()
        {
            return (EntityState)MemberwiseClone();
        }
    }

    /// <summary>What the player's own HUD shows, so the spectator can draw the same one.</summary>
    [Serializable]
    public class PlayerHudState
    {
        public int lv;
        public int xp;
        public int str;
        public int dex;
        public int itl;
        public float hp;
        public float mhp;
        public float mp;
        public float mmp;
        public int dead;
        public float cd;   // seconds left on the death countdown
        public int rv;     // 1 = countdown finished, waiting for the player to press a key to revive
        public int arw;    // extra arrows each bow shot looses (so the spectator's replay fans out the same)
    }

    /// <summary>
    /// One skill the player used (a staff's spell, a bar skill), for spectators to replay its
    /// visuals. Snapshots carry the last few, numbered, so a cast between two snapshots is never lost.
    /// </summary>
    [Serializable]
    public class SkillCastState
    {
        public int n;        // cast number (counts up; only ones newer than the last seen are played)
        public int s;        // SkillId
        public int lv;       // skill level (sizes some effects)
        public float x;      // where it was cast from (the player's feet)
        public float y;
        public float z;
        public float dx;     // which way it went (flat)
        public float dz;
        public float sz;     // size: a burst's or nova's radius
        public int c;        // projectiles fired
        public float[] pts;  // Chain Lightning: the arc's points, x,y,z per point
    }

    /// <summary>An item lying on the ground near the player.</summary>
    [Serializable]
    public class LootState
    {
        public int i;      // drop id, stable while it lies there
        public float x;
        public float y;
        public float z;
        public string b;   // item base id (its icon)
        public string n;   // item name
        public int q;      // rarity (ItemRarity)
    }

    [Serializable]
    public class StateSnapshot
    {
        public const string MessageType = "state";

        public string type = MessageType;
        public int seq;        // monotonically increasing per player session
        public double t;       // sender clock, seconds (unscaled, so it keeps ticking through loading-screen freezes)
        public int pid;        // player session id, stamped by the server - a change means "new player, reset the replica"
        public int area;
        public int fade;       // 1 = the player's loading screen is up (area switch / revive)
        public int dev;        // 1 = the player is on a phone/tablet (touch controls), 0 = a computer
        public EntityState p;
        public PlayerHudState hud;
        public string[] eq;    // equipped item id per EquipSlot (by enum order), "" = empty
        public EntityState[] e;
        public LootState[] l;  // items on the ground nearby
        public SkillCastState[] sc; // the player's latest skill casts (see SkillCastState)
        public UiState ui;     // the player's open menus and pointer (see GearState.cs)
    }
}
