using System;

namespace PoeClone.Network
{
    public enum SessionRole
    {
        Player,
        Spectator
    }

    // Wire format shared with server/room.js. Kept as plain JsonUtility-serializable
    // classes (no dictionaries/polymorphism) since JsonUtility can't handle either.

    [Serializable]
    public class HudPayload
    {
        public float hp;
        public float maxHp;
        public float mp;
        public float maxMp;
        public int level;
        public string area;
    }

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
        public int spectatorCount;
        public string image;
        public HudPayload hud;
        public string from;
        public string text;
        public long ts;
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
        public string image;
        public HudPayload hud;
    }

    [Serializable]
    public class ChatOutMessage
    {
        public string type = "chat";
        public string text;
    }

    public readonly struct FrameEnvelope
    {
        public readonly string ImageBase64;
        public readonly HudPayload Hud;
        public readonly long Ts;

        public FrameEnvelope(string imageBase64, HudPayload hud, long ts)
        {
            ImageBase64 = imageBase64;
            Hud = hud;
            Ts = ts;
        }
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
