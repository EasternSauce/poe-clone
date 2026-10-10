using System;

namespace PoeClone.Network
{
    public enum SessionRole
    {
        Player,
        Spectator
    }

    // Wire format shared with server/room.js. (The 10Hz gameplay "state" message lives in
    // Replication/StateSnapshot.cs.) Kept as plain JsonUtility-serializable
    // classes (no dictionaries/polymorphism) since JsonUtility can't handle either.

    // Catch-all inbound shape: JsonUtility silently ignores JSON fields that don't
    // exist on this class and leaves fields the JSON didn't set at their default,
    // so one class can parse every message "type" the server ever sends.
    [Serializable]
    public class ServerMessage
    {
        public string type;
        public string role;
        public bool granted;
        public string reason;
        public bool playerActive;
        public int playerCount;
        public int maxPlayers;
        public int spectatorCount;
        public PlayerInfo[] players; // status: everyone playing, oldest first
        public int watching;         // status, spectators only: the player id being watched (0 = nobody)
        public string from;
        public string text;
        public long ts;
        public string state;         // coop: hosting / started / failed / ended
        public string coopRole;      // coop started: host / guest
        public string partnerName;
        public PlayerInfo[] hosts;   // lobby: players waiting for a partner
    }

    [Serializable]
    public class PlayerInfo
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class HelloMessage
    {
        public string type = "hello";
        public string role;
        public string name;
    }

    [Serializable]
    public class FrameMessage
    {
        public string type = "frame";
    }

    /// <summary>A spectator switching to another player's game.</summary>
    [Serializable]
    public class WatchMessage
    {
        public string type = "watch";
        public int id;
    }

    [Serializable]
    public class ChatOutMessage
    {
        public string type = "chat";
        public string text;
    }

    public readonly struct ChatEnvelope
    {
        public readonly string From;
        public readonly string Role;
        public readonly string Text;
        public readonly long Ts;

        public ChatEnvelope(string from, string role, string text, long ts)
        {
            From = from;
            Role = role;
            Text = text;
            Ts = ts;
        }
    }
}
