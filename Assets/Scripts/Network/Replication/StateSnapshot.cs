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
        public EntityState p;
        public PlayerHudState hud;
        public string[] eq;    // equipped item id per EquipSlot (by enum order), "" = empty
        public EntityState[] e;
    }
}
