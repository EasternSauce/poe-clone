using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// One-line helpers for play-testing through the Unity MCP bridge's execute_code, so a test
    /// session doesn't paste the same twenty lines of setup C# again and again. Every method returns
    /// a short string to read back. Typical session (see .claude/skills/unity-playtest):
    /// <code>
    /// return PoeClone.EditorTools.DevTest.Begin();          // edit mode: back up prefs, point at ws://localhost:8099
    /// (manage_editor play)
    /// return PoeClone.EditorTools.DevTest.Ready();          // until it says "running": name prompt, patch notes
    /// return PoeClone.EditorTools.DevTest.God();            // huge life and mana
    /// return PoeClone.EditorTools.DevTest.Equip("bone_sceptre", "GrantRaiseSkeletons=1");
    /// return PoeClone.EditorTools.DevTest.Spawn("Zombie", 1, 3, -6, 2);
    /// return PoeClone.EditorTools.DevTest.Status();         // player, minions, enemies in one line each
    /// (manage_editor stop)
    /// return PoeClone.EditorTools.DevTest.End();            // edit mode: restore prefs
    /// </code>
    /// Editor-only (Assets/Editor), never in a build.
    /// </summary>
    public static class DevTest
    {
        private const string SaveKey = "PoeClone.Save.v1";
        private const string DevQueryKey = "PoeClone.DevQuery";
        private const string SeenKey = "PoeClone.PatchNotesSeen";
        private static readonly string BackupPath = Path.Combine("Library", "DevTestPrefsBackup.txt");
        private const string None = "<<NONE>>";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ------------------------------------------------------------------ session

        /// <summary>
        /// Before Play: backs up the user's save and patch-notes-seen prefs (once: a second Begin
        /// keeps the first backup), clears the save for a fresh character, and points the game at
        /// the local session server.
        /// </summary>
        public static string Begin(string server = "ws://localhost:8099", bool freshCharacter = true)
        {
            if (!File.Exists(BackupPath))
                File.WriteAllText(BackupPath, Get(SaveKey) + "\n<<SPLIT>>\n" + Get(SeenKey));
            if (freshCharacter)
                PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.SetString(DevQueryKey, "?server=" + server);
            PlayerPrefs.Save();
            return "backed up to " + BackupPath + "; DevQuery=" + server + (freshCharacter ? "; fresh character" : "");
        }

        /// <summary>After Play: puts the user's prefs back exactly and removes the dev server setting.</summary>
        public static string End()
        {
            PlayerPrefs.DeleteKey(DevQueryKey);
            string result = "DevQuery cleared";
            if (File.Exists(BackupPath))
            {
                string[] parts = File.ReadAllText(BackupPath).Split(new[] { "\n<<SPLIT>>\n" }, StringSplitOptions.None);
                Put(SaveKey, parts[0]);
                Put(SeenKey, parts.Length > 1 ? parts[1] : None);
                File.Delete(BackupPath);
                result += "; save restored (" + (parts[0] == None ? "none" : parts[0].Length + " chars") + ")";
            }
            else
            {
                result += "; no backup found (nothing restored)";
            }
            PlayerPrefs.Save();
            return result;
        }

        private static string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : None;

        private static void Put(string key, string value)
        {
            if (value == None)
                PlayerPrefs.DeleteKey(key);
            else
                PlayerPrefs.SetString(key, value);
        }

        /// <summary>
        /// In Play: confirms the name prompt and closes the patch notes if they're up. Call again
        /// until it reports "running" (the first confirm is sometimes swallowed while the session
        /// is still connecting; the world stays at timeScale 0 until the server grants the slot).
        /// </summary>
        public static string Ready()
        {
            if (!Application.isPlaying)
                return "not playing";
            var sb = new StringBuilder();
            Type prompt = Type.GetType("PoeClone.Network.NamePromptUI, Assembly-CSharp");
            UnityEngine.Object ui = prompt != null ? UnityEngine.Object.FindAnyObjectByType(prompt) : null;
            if (ui != null && ((Component)ui).gameObject.activeInHierarchy && Time.timeScale == 0f)
            {
                prompt.GetMethod("Confirm", Any)?.Invoke(ui, null);
                sb.Append("confirmed name; ");
            }
            Type notes = Type.GetType("PoeClone.UI.PatchNotesUI, Assembly-CSharp");
            UnityEngine.Object pn = notes != null ? UnityEngine.Object.FindAnyObjectByType(notes) : null;
            GameObject notesRoot = pn != null ? notes.GetField("root", Any)?.GetValue(pn) as GameObject : null;
            if (notesRoot != null && notesRoot.activeSelf)
            {
                notes.GetMethod("Close", Any)?.Invoke(pn, null);
                sb.Append("closed notes; ");
            }
            sb.Append(Time.timeScale > 0f ? "running" : "paused (call Ready again in a few seconds)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the player

        private static PlayerStats Stats() => UnityEngine.Object.FindAnyObjectByType<PlayerStats>();

        /// <summary>Practically unkillable, with mana to spare (to watch minions/enemies without dying).</summary>
        public static string God(float life = 100000f, float mana = 10000f)
        {
            PlayerStats ps = Stats();
            if (ps == null)
                return "no player";
            typeof(PlayerStats).GetField("maxHealth", Any)?.SetValue(ps, life);
            typeof(PlayerStats).GetField("maxMana", Any)?.SetValue(ps, mana);
            ps.Heal(life);
            ps.RestoreMana(mana);
            return "life " + ps.CurrentHealth + ", mana " + ps.CurrentMana;
        }

        /// <summary>
        /// Generates a Normal item of a base at an item level, adds the given stats
        /// ("GrantBoneGolem=5, MinionLife=40"), and equips it in its slot (any off-hand item to the off hand).
        /// </summary>
        public static string Equip(string baseId, string extraStats = "", int itemLevel = 10)
        {
            PlayerInventory inv = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            if (inv == null)
                return "no player";
            ItemData baseItem = ItemGenerator.Generate(new System.Random(1), baseId, itemLevel, ItemRarity.Normal);
            if (baseItem == null)
                return "no base " + baseId;
            var mods = new List<StatModifier>();
            foreach (string part in (extraStats ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=');
                mods.Add(new StatModifier((StatType)Enum.Parse(typeof(StatType), kv[0].Trim()),
                    float.Parse(kv[1].Trim(), System.Globalization.CultureInfo.InvariantCulture)));
            }
            mods.AddRange(baseItem.Modifiers);
            var item = new ItemData(baseItem.Id, baseItem.Name, baseItem.Type, baseItem.Width, baseItem.Height, baseItem.Tint, mods,
                baseItem.HasCape, baseItem.WeaponType, baseItem.Rarity);
            item.ArtId = baseItem.ArtId;
            item.ArtTint = baseItem.ArtTint;

            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                if (!SlotRules.Accepts(slot, item) || (item.Type == ItemType.Ring && slot == EquipSlot.Ring2))
                    continue;
                bool ok = inv.Equipment.TryEquip(slot, item, out _);
                return (ok ? "equipped " : "could NOT equip ") + item.Name + " in " + slot + "; " + Skills();
            }
            return "no slot takes " + item.Type;
        }

        /// <summary>The attack skill and the bar, with levels.</summary>
        public static string Skills()
        {
            PlayerSkills skills = UnityEngine.Object.FindAnyObjectByType<PlayerSkills>();
            if (skills == null)
                return "no skills";
            var sb = new StringBuilder("attack " + (skills.MainSkill?.ToString() ?? "weapon") + "; bar:");
            for (int k = 0; k < SkillBook.SlotCount; k++)
            {
                SkillId? id = skills.Slot(k);
                if (id != null)
                    sb.Append(' ').Append(k).Append('=').Append(id).Append(" L").Append(skills.Level(id.Value));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Uses a bar skill (slot number, or a SkillId name) through the real path, with its cooldown
        /// cleared first. aimAtNearest: on touch-style aim, pointed at the nearest enemy (the mouse
        /// can't be steered from here).
        /// </summary>
        public static string Use(string slotOrSkill, bool aimAtNearest = true)
        {
            PlayerSkills skills = UnityEngine.Object.FindAnyObjectByType<PlayerSkills>();
            if (skills == null)
                return "no skills";
            int slot;
            if (!int.TryParse(slotOrSkill, out slot))
            {
                slot = -1;
                for (int k = 0; k < SkillBook.SlotCount; k++)
                {
                    if (skills.Slot(k)?.ToString() == slotOrSkill)
                        slot = k;
                }
                if (slot < 0)
                    return slotOrSkill + " is not on the bar; " + Skills();
            }
            ((System.Collections.IDictionary)typeof(PlayerSkills).GetField("readyAt", Any).GetValue(skills)).Clear();

            FieldInfo forced = typeof(TouchMode).GetField("forced", Any);
            EnemyHealth target = Nearest(skills.transform.position);
            bool aim = aimAtNearest && target != null && forced != null && Camera.main != null;
            if (aim)
            {
                forced.SetValue(null, true);
                Vector3 a = Camera.main.WorldToScreenPoint(skills.transform.position);
                Vector3 b = Camera.main.WorldToScreenPoint(target.transform.position);
                VirtualInput.Aim = new Vector2(b.x - a.x, b.y - a.y).normalized;
            }
            bool used;
            try
            {
                used = skills.TryUse(slot);
            }
            finally
            {
                if (aim)
                {
                    forced.SetValue(null, false);
                    VirtualInput.Aim = Vector2.zero;
                }
            }
            return (used ? "used " : "did NOT use ") + skills.Slot(slot) + (aim ? " at " + target.name : "");
        }

        private static double attackUntil;

        /// <summary>
        /// Holds the attack (touch aim stick) at the nearest enemy for a few real seconds, re-aiming
        /// every editor tick, then lets go. Bow skills that are on shoot instead of the plain shot.
        /// </summary>
        public static string Attack(float seconds = 2f)
        {
            PlayerStats ps = Stats();
            if (ps == null || Nearest(ps.transform.position) == null)
                return "no player or no enemy";
            attackUntil = EditorApplication.timeSinceStartup + seconds;
            EditorApplication.update -= HoldAttack;
            EditorApplication.update += HoldAttack;
            HoldAttack();
            return "attacking for " + seconds + "s at " + Nearest(ps.transform.position).name;
        }

        private static void HoldAttack()
        {
            FieldInfo forced = typeof(TouchMode).GetField("forced", Any);
            PlayerStats ps = Stats();
            EnemyHealth target = ps != null ? Nearest(ps.transform.position) : null;
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > attackUntil || target == null || Camera.main == null)
            {
                EditorApplication.update -= HoldAttack;
                VirtualInput.AttackHeld = false;
                VirtualInput.Aim = Vector2.zero;
                if (forced != null)
                    forced.SetValue(null, false);
                return;
            }
            if (forced != null)
                forced.SetValue(null, true);
            Vector3 a = Camera.main.WorldToScreenPoint(ps.transform.position);
            Vector3 b = Camera.main.WorldToScreenPoint(target.transform.position);
            VirtualInput.Aim = new Vector2(b.x - a.x, b.y - a.y).normalized;
            VirtualInput.AttackHeld = true;
        }

        // ------------------------------------------------------------------ the world

        /// <summary>Spawns enemies of a kind (name or index) at an offset from the player.</summary>
        public static string Spawn(string kind, int level = 1, int count = 1, float dx = 6f, float dz = 0f)
        {
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            PlayerStats ps = Stats();
            if (spawner == null || ps == null)
                return "no spawner/player";
            int index = int.TryParse(kind, out int n) ? n : EnemyKinds.IndexOf(kind);
            if (index < 0)
                return "no kind " + kind;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = ps.transform.position + new Vector3(dx + 1.3f * k, 0.5f, dz);
                GameObject go = UnityEngine.Object.Instantiate(spawner.EnemyPrefab, at, Quaternion.identity);
                EnemyKinds.Apply(go, index, level);
            }
            Physics.SyncTransforms();
            return "spawned " + count + "x " + EnemyKinds.Get(index).Name + " L" + level;
        }

        /// <summary>The player, every minion and every living enemy, briefly.</summary>
        public static string Status()
        {
            var sb = new StringBuilder("t=" + Time.time.ToString("0.0") + " ts=" + Time.timeScale);
            PlayerStats ps = Stats();
            if (ps != null)
                sb.Append(" | player ").Append(ps.CurrentHealth.ToString("0")).Append('/').Append(ps.MaxHealth.ToString("0"))
                  .Append(" mana ").Append(ps.CurrentMana.ToString("0")).Append(ps.IsDead ? " DEAD" : "");
            sb.Append(" | minions:");
            foreach (Minion m in Minion.All)
                sb.Append(' ').Append(m.Kind).Append(' ').Append(m.Life.ToString("0")).Append('/').Append(m.MaxLife.ToString("0"));
            sb.Append(" | enemies:");
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead)
                    continue;
                EnemyController ai = e.GetComponent<EnemyController>();
                Minion target = ai != null ? ai.TargetMinion : null;
                float d = ps != null ? Vector3.Distance(ps.transform.position, e.transform.position) : 0f;
                if (d > 25f)
                    continue;
                sb.Append(' ').Append(EnemyKinds.Get(e.KindIndex).Name).Append(' ').Append(e.CurrentHealth.ToString("0"))
                  .Append('/').Append(e.MaxHealth.ToString("0")).Append(" ->").Append(target != null ? target.Kind.ToString() : "player")
                  .Append(Curse.TakenMultiplier(e) > 1f ? " cursed" : "").Append(e == Minion.Marked ? " marked" : "").Append(';');
            }
            return sb.ToString();
        }

        /// <summary>Kills every enemy within a radius of the player (clears the field between tests).</summary>
        public static string Clear(float radius = 30f)
        {
            PlayerStats ps = Stats();
            int killed = 0;
            foreach (EnemyHealth e in EnemyHealth.Active.ToArray())
            {
                if (e != null && !e.IsDead && ps != null && Vector3.Distance(ps.transform.position, e.transform.position) <= radius)
                {
                    e.TakeDamage(e.CurrentHealth + 1f);
                    killed++;
                }
            }
            return "killed " + killed;
        }

        // ------------------------------------------------------------------ quests

        /// <summary>
        /// Skips ahead to a quest: everything before it in its chain (and their chains) counts as
        /// handed in, and the quest itself is taken. "complete" also reaches its goal.
        /// </summary>
        public static string Quest(string id, bool complete = false)
        {
            var log = Quests.QuestLog.Instance;
            var quest = Quests.QuestBook.Get(id);
            if (log == null || quest == null)
                return "no quest log / no quest " + id;
            var done = (HashSet<string>)typeof(Quests.QuestLog).GetField("done", Any).GetValue(log);
            var active = (Dictionary<string, int>)typeof(Quests.QuestLog).GetField("active", Any).GetValue(log);
            for (string before = quest.After; before != null; )
            {
                done.Add(before);
                active.Remove(before);
                var b = Quests.QuestBook.Get(before);
                before = b != null ? b.After : null;
            }
            done.Remove(id);
            active[id] = complete ? quest.Count : 0;
            var changed = (MulticastDelegate)typeof(Quests.QuestLog).GetField("Changed", Any).GetValue(log);
            if (changed != null)
                changed.DynamicInvoke();
            return id + ": " + log.State(quest);
        }

        /// <summary>Goes to an area (by index: Greenwood 0, Haven 1, Graveyard 2, Ruins 3, Frozen 4).</summary>
        public static string Area(int area)
        {
            var areas = World.AreaManager.Instance;
            if (areas == null)
                return "no area manager";
            areas.EnterArea(area, null);
            return "entering " + World.WorldBuilder.AreaNames[area];
        }

        /// <summary>
        /// Puts the player next to something in the current area: a WorldBuilder spot ("Seer"),
        /// a quest's first unused prop ("prop:totems"), or "x,z".
        /// </summary>
        public static string Warp(string target, float back = 2.5f)
        {
            PlayerStats ps = Stats();
            if (ps == null)
                return "no player";
            Vector3 at;
            if (target.StartsWith("prop:"))
            {
                string questId = target.Substring(5);
                Quests.QuestProp found = null;
                foreach (var prop in UnityEngine.Object.FindObjectsByType<Quests.QuestProp>(FindObjectsSortMode.None))
                {
                    if (prop.QuestId == questId && (Quests.QuestLog.Instance == null || !Quests.QuestLog.Instance.PropUsed(prop.PropId)))
                    {
                        found = prop;
                        break;
                    }
                }
                if (found == null)
                    return "no unused prop for " + questId;
                at = found.transform.position;
            }
            else if (target.Contains(","))
            {
                string[] xz = target.Split(',');
                at = new Vector3(float.Parse(xz[0]), 0f, float.Parse(xz[1]));
            }
            else if (!World.WorldBuilder.Instance.Spots.TryGetValue(target, out at))
            {
                return "no spot " + target;
            }
            Vector3 p = at + Vector3.back * back;
            p.y = 1.1f;
            var cc = ps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ps.transform.position = p;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraSystem.CameraFollow>() : null;
            if (cam != null)
                cam.SnapToTarget();
            return "at " + p;
        }

        /// <summary>Talks to someone (or uses a quest prop) as if clicked in reach; returns the page.</summary>
        public static string Talk(string role)
        {
            World.NpcRole r = (World.NpcRole)Enum.Parse(typeof(World.NpcRole), role);
            World.Npc npc = null;
            if (r == World.NpcRole.QuestProp)
            {
                PlayerStats ps = Stats();
                float best = float.MaxValue;
                foreach (World.Npc n in World.Npc.All)
                {
                    float d = n.Role == r && ps != null ? Vector3.Distance(n.transform.position, ps.transform.position) : float.MaxValue;
                    if (d < best) { best = d; npc = n; }
                }
            }
            else
            {
                npc = World.Npc.Find(r);
            }
            if (npc == null)
                return "nobody: " + role;
            UI.NpcDialogues.Open(npc);
            return Page();
        }

        /// <summary>The open conversation: speaker, text and numbered options.</summary>
        public static string Page()
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<UI.DialogueUI>();
            if (!UI.DialogueUI.IsOpen || ui == null)
                return "(no dialogue)";
            var body = (UnityEngine.UI.Text)typeof(UI.DialogueUI).GetField("body", Any).GetValue(ui);
            var options = (List<UI.DialogueOption>)typeof(UI.DialogueUI).GetField("current", Any).GetValue(ui);
            var sb = new StringBuilder((UI.DialogueUI.Speaker != null ? UI.DialogueUI.Speaker.DisplayName : "?") + ": " + body.text);
            for (int k = 0; k < options.Count; k++)
                sb.Append(" [").Append(k).Append("] ").Append(options[k].Label).Append(options[k].Enabled ? "" : " (off)");
            return sb.ToString();
        }

        /// <summary>Picks a dialogue option by number; returns the next page.</summary>
        public static string Pick(int index)
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<UI.DialogueUI>();
            if (ui == null)
                return "no dialogue ui";
            typeof(UI.DialogueUI).GetMethod("Pick", Any).Invoke(ui, new object[] { index });
            return Page();
        }

        /// <summary>The quests under way, with state and progress.</summary>
        public static string QuestStatus()
        {
            var log = Quests.QuestLog.Instance;
            if (log == null)
                return "no log";
            var sb = new StringBuilder();
            foreach (var q in log.Taken())
                sb.Append(q.Id).Append(' ').Append(log.State(q)).Append(' ').Append(log.Progress(q)).Append('/').Append(q.Count).Append("; ");
            return sb.Length > 0 ? sb.ToString() : "(none taken)";
        }

        private static EnemyHealth Nearest(Vector3 from)
        {
            EnemyHealth best = null;
            float bestD = float.MaxValue;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead)
                    continue;
                float d = (e.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }
    }
}
