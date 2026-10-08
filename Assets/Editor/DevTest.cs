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
using PoeClone.World;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// One-line helpers for play-testing through the Unity MCP bridge's execute_code, so a test
    /// session doesn't paste the same twenty lines of setup C# again and again. Every method returns
    /// a short string to read back. Typical session (see .claude/skills/unity-playtest):
    /// <code>
    /// return PoeClone.EditorTools.DevTest.QuickStart();     // edit mode: play, skip UI, god mode, sandbox
    /// (stop Play to restore preferences automatically)
    ///
    /// Or, for startup UI testing, use the manual sequence:
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
        private const string ProfilesKey = "PoeClone.CharacterProfiles.v1";
        private const string ActiveProfileKey = "PoeClone.ActiveCharacter.v1";
        private const string DevQueryKey = "PoeClone.DevQuery";
        private const string SeenKey = "PoeClone.PatchNotesSeen";
        private static readonly string BackupPath = Path.Combine("Library", "DevTestPrefsBackup.txt");
        private const string None = "<<NONE>>";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static bool quickSession;
        private static bool quickSandbox;
        private static bool quickTouch;
        private static bool quickWorldLayouts;
        private static bool worldLayoutStartupReady;
        private static string quickWeaponBaseId;
        private static double nextReadyAt;
        private static double quickDeadline;
        private static string quickState = "idle";

        [InitializeOnLoadMethod]
        private static void InstallQuickSessionCleanup()
        {
            EditorApplication.playModeStateChanged -= OnQuickPlayModeChanged;
            EditorApplication.playModeStateChanged += OnQuickPlayModeChanged;
        }

        private static void OnQuickPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !quickSession) return;
            EditorApplication.update -= AdvanceQuickSession;
            quickSession = false;
            quickState = End();
            Debug.Log("DevTest QuickStart: " + quickState);
        }

        /// <summary>
        /// One-call Editor Play setup. Requires the local session server on port 8099. Selects the
        /// temporary DevTest character, dismisses startup UI, gives god mode, then moves to a quiet
        /// flat floor by default. Stop Play to restore the user's character and preferences.
        /// </summary>
        public static string QuickStart(bool sandbox = true, string weaponBaseId = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "stop Play before QuickStart";
            string begin = Begin();
            quickSession = true;
            quickSandbox = sandbox;
            quickTouch = false;
            quickWorldLayouts = false;
            worldLayoutStartupReady = false;
            quickWeaponBaseId = weaponBaseId;
            quickState = "starting Play";
            quickDeadline = EditorApplication.timeSinceStartup + 45;
            nextReadyAt = 0;
            InstallQuickSessionCleanup();
            EditorApplication.update -= AdvanceQuickSession;
            EditorApplication.update += AdvanceQuickSession;
            EditorApplication.isPlaying = true;
            return begin + "; automatic startup pending (QuickStatus for progress)";
        }

        /// <summary>Standalone loot debug run: no game world, server or character startup.</summary>
        public static string QuickStartLootSimulator() => LootSimulatorSession.Start();

        [MenuItem("PoeClone/Test/Loot Simulator")]
        private static void LootSimulatorMenu() => Debug.Log(QuickStartLootSimulator());

        [MenuItem("PoeClone/Test/Quick Start (Sandbox)")]
        private static void QuickStartMenu() => Debug.Log("DevTest QuickStart: " + QuickStart());

        [MenuItem("PoeClone/Test/Bow Grip Demo")]
        private static void BowGripDemoMenu() => Debug.Log("DevTest Bow Grip Demo: " + QuickStart(weaponBaseId: "short_bow"));

        [MenuItem("PoeClone/World/Rendered World Layouts")]
        private static void WorldLayoutsMenu() => Debug.Log(QuickStartWorldLayouts());

        public static string QuickStartWorldLayouts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play before opening Rendered World Layouts";
            // Start the local server explicitly on the same port used by the temporary session.
            bool listening = false;
            foreach (var endpoint in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                if (endpoint.Port == 8099) { listening = true; break; }
            if (!listening)
            {
                var start = new System.Diagnostics.ProcessStartInfo("node", "server.js")
                {
                    WorkingDirectory = Path.GetFullPath("server"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                start.EnvironmentVariables["PORT"] = "8099";
                System.Diagnostics.Process.Start(start);
                // Wait for the listener in the editor update callback before QuickStart.
                EditorApplication.update -= WaitForWorldLayoutServer;
                EditorApplication.update += WaitForWorldLayoutServer;
                worldLayoutServerDeadline = EditorApplication.timeSinceStartup + 15;
                return "Starting local server on 8099; rendered world startup pending";
            }
            if (!WorldLayoutPreviewSession.Prepare()) return "Could not prepare rendered world preview scene";
            string result = QuickStart(sandbox: false);
            quickWorldLayouts = true;
            return result;
        }

        private static double worldLayoutServerDeadline;
        private static void WaitForWorldLayoutServer()
        {
            foreach (var endpoint in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                if (endpoint.Port == 8099)
                {
                    EditorApplication.update -= WaitForWorldLayoutServer;
                    Debug.Log(QuickStartWorldLayouts());
                    return;
                }
            if (EditorApplication.timeSinceStartup > worldLayoutServerDeadline)
            {
                EditorApplication.update -= WaitForWorldLayoutServer;
                Debug.LogError("Rendered World Layouts: local server did not start on port 8099.");
            }
        }

        /// <summary>Starts the retained Editor arena; options are local diagnostics, never public URLs.</summary>
        public static string QuickStartMinimal(bool touch = false, string options = "")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "stop Play before QuickStartMinimal";
            string result = QuickStart(sandbox: false);
            string query = PlayerPrefs.GetString(DevQueryKey) + "&minimal=1";
            if (touch) query += "&touch=1";
            if (!string.IsNullOrWhiteSpace(options)) query += "&" + options.TrimStart('?', '&');
            PlayerPrefs.SetString(DevQueryKey, query);
            PlayerPrefs.Save();
            quickTouch = touch;
            return result + "; Editor minimal arena requested";
        }

        [MenuItem("PoeClone/Test/Minimal Combat Arena")]
        private static void MinimalCombatMenu() => Debug.Log("DevTest Minimal Combat: " + QuickStartMinimal());

        private static void AdvanceQuickSession()
        {
            if (!quickSession || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextReadyAt) return;
            if (now > quickDeadline)
            {
                quickState = "timed out waiting for local server; stop Play to restore prefs";
                Debug.LogWarning("DevTest QuickStart: " + quickState);
                EditorApplication.update -= AdvanceQuickSession;
                return;
            }
            nextReadyAt = now + 2;
            string ready = Ready();
            quickState = ready;
            if (Time.timeScale <= 0f) return;
            string setup = God();
            if (!string.IsNullOrEmpty(quickWeaponBaseId)) setup += " || " + Equip(quickWeaponBaseId);
            if (quickSandbox) setup += " || " + Sandbox();
            // QuickStart is intended for a desktop editor demo even if its host page carries ?touch=1.
            TouchMode.SetForced(quickTouch);
            QualitySettings.SetQualityLevel(1, true);
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            quickState = ready + " || " + setup;
            if (quickWorldLayouts)
            {
                // Let delayed startup overlays appear, then advance Ready again before inspecting.
                if (!worldLayoutStartupReady)
                { worldLayoutStartupReady = true; return; }
                Ready();
                if (PoeClone.UI.PatchNotesUI.IsShowing || WorldBuilder.Instance == null) return;
                var mode = PoeClone.CameraSystem.WorldLayoutMode.Open();
                if (mode == null || !mode.IsActive) return;
                WorldLayoutPreviewSession.Reveal();
                quickState += " || rendered world layouts ready";
            }
            EditorApplication.update -= AdvanceQuickSession;
            Debug.Log("DevTest QuickStart: " + quickState);
        }

        /// <summary>Last QuickStart result; useful for a single MCP check after Play begins.</summary>
        public static string QuickStatus() => quickState;

        /// <summary>Move to a quiet, flat Play-only floor for repeatable combat and visual checks.</summary>
        public static string Sandbox()
        {
            if (!Application.isPlaying || Stats() == null) return "call Sandbox in Play";
            GameObject floor = GameObject.Find("DevTestSandboxFloor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "DevTestSandboxFloor";
                floor.transform.position = new Vector3(500f, -0.5f, 500f);
                floor.transform.localScale = new Vector3(120f, 1f, 120f);
            }
            return Warp("500,500", 0f) + "; sandbox ready";
        }

        /// <summary>Check passive review using the same click callback as the node UI, in a temporary QuickStart session.</summary>
        public static string PassiveReview(bool verify = false)
        {
            if (!Application.isPlaying || Stats() == null) return "call in a QuickStart Play session";
            Stats().RestoreProgress(12, 0);
            var player = Stats().GetComponent<PlayerPassives>();
            var ui = UnityEngine.Object.FindAnyObjectByType<PoeClone.UI.PassiveTreeUI>();
            if (ui == null) return "passive UI missing";
            PoeClone.UI.PassiveTreeUI.SetOpen(true);
            if (!verify) return "passive tree open; 11 points to preview";
            var click = typeof(PoeClone.UI.PassiveTreeUI).GetMethod("OnClick", Any);
            Action<string> take = id => click.Invoke(ui, new object[] { PassiveTree.Get(id), UnityEngine.EventSystems.PointerEventData.InputButton.Left });
            Action<bool, string> require = (ok, message) => { if (!ok) throw new Exception(message); };
            take("m1"); take("m2"); take("m_life");
            require(!player.Allocation.Has("m1") && ui.HasPendingChanges, "draft changed live allocation");
            PoeClone.UI.PassiveTreeUI.SetOpen(false);
            require(PoeClone.UI.PassiveTreeUI.IsOpen, "close bypassed review");
            ui.CancelChanges();
            require(!PoeClone.UI.PassiveTreeUI.IsOpen && player.Allocation.Spent == 0, "cancel failed");
            PoeClone.UI.PassiveTreeUI.SetOpen(true);
            take("m1"); take("m2"); take("m_life_heart");
            var draft = (PassiveAllocation)typeof(PoeClone.UI.PassiveTreeUI).GetField("draft", Any).GetValue(ui);
            require(!draft.Has("m_life_heart"), "life notable skipped prerequisite");
            take("m_life"); take("m_life_heart");
            require(draft.Has("m_life_heart"), "life notable not reachable after prerequisite");
            ui.ConfirmChanges();
            require(!PoeClone.UI.PassiveTreeUI.IsOpen && player.Allocation.Spent == 4, "confirmation failed");
            PoeClone.UI.PassiveTreeUI.SetOpen(true);
            draft = (PassiveAllocation)typeof(PoeClone.UI.PassiveTreeUI).GetField("draft", Any).GetValue(ui);
            int charges = player.RespecCharges;
            draft.ResetAll();
            typeof(PoeClone.UI.PassiveTreeUI).GetField("resetPending", Any).SetValue(ui, true);
            ui.CancelChanges();
            require(player.Allocation.Spent == 4 && player.RespecCharges == charges, "cancelled reset consumed points or charge");
            PoeClone.UI.PassiveTreeUI.SetOpen(true);
            draft = (PassiveAllocation)typeof(PoeClone.UI.PassiveTreeUI).GetField("draft", Any).GetValue(ui);
            draft.ResetAll();
            typeof(PoeClone.UI.PassiveTreeUI).GetField("resetPending", Any).SetValue(ui, true);
            take("g1");
            ui.ConfirmChanges();
            require(player.Allocation.Spent == 1 && player.Allocation.Has("g1") && player.RespecCharges == charges - 1, "confirmed reset failed");
            PoeClone.UI.PassiveTreeUI.SetOpen(true);
            return "PASS: preview, close guard, Cancel, Confirm, life prerequisite, cancelled reset, confirmed reset";
        }

        // ------------------------------------------------------------------ session

        /// <summary>
        /// Before Play: backs up the user's save and patch-notes-seen prefs (once: a second Begin
        /// keeps the first backup), clears the save for a fresh character, and points the game at
        /// the local session server.
        /// </summary>
        public static string Begin(string server = "ws://localhost:8099", bool freshCharacter = true)
        {
            bool firstBegin = !File.Exists(BackupPath);
            if (firstBegin)
                File.WriteAllText(BackupPath, string.Join("\n<<SPLIT>>\n", new[] { Get(SaveKey), Get(SeenKey), Get(ProfilesKey), Get(ActiveProfileKey) }));
            if (freshCharacter && firstBegin)
            {
                SaveSystem.CreateProfile("DevTest");
                PlayerPrefs.DeleteKey("PoeClone.CharacterSave." + SaveSystem.ActiveProfileId);
                File.AppendAllText(BackupPath, "\n<<SPLIT>>\n" + SaveSystem.ActiveProfileId);
            }
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
                if (parts.Length > 2) Put(ProfilesKey, parts[2]);
                if (parts.Length > 3)
                {
                    Put(ActiveProfileKey, parts[3]);
                    if (parts.Length > 4) PlayerPrefs.DeleteKey("PoeClone.CharacterSave." + parts[4]);
                    if (parts[3] != None) SaveSystem.SelectProfile(parts[3]);
                }
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
                if (prompt.GetField("afterCharacter", Any)?.GetValue(ui) != null)
                {
                    prompt.GetMethod("FinishCharacters", Any)?.Invoke(ui, null);
                    sb.Append("selected character; ");
                }
                else
                {
                    prompt.GetMethod("Confirm", Any)?.Invoke(ui, null);
                    sb.Append("confirmed name; ");
                }
            }
            Type notes = Type.GetType("PoeClone.UI.PatchNotesUI, Assembly-CSharp");
            UnityEngine.Object pn = notes != null ? UnityEngine.Object.FindAnyObjectByType(notes) : null;
            GameObject notesRoot = pn != null ? notes.GetField("root", Any)?.GetValue(pn) as GameObject : null;
            if (notesRoot != null && notesRoot.activeSelf)
            {
                notes.GetMethod("Close", Any)?.Invoke(pn, null);
                sb.Append("closed notes; ");
            }
            if (Time.timeScale > 0f && Stats() != null)
                sb.Append(EnsureDash()).Append("; ");
            sb.Append(Time.timeScale > 0f ? "running" : "paused (call Ready again in a few seconds)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the player

        private static PlayerStats Stats() => UnityEngine.Object.FindAnyObjectByType<PlayerStats>();

        /// <summary>Make Dash available in every DevTest session, including after a loadout replaces gear.</summary>
        public static string EnsureDash()
        {
            PlayerSkills skills = UnityEngine.Object.FindAnyObjectByType<PlayerSkills>();
            PlayerStats player = Stats();
            if (skills == null || player == null)
                return "no player skills";
            if (skills.Level(SkillId.Dash) > 0)
                return "Dash ready";
            return Equip("leather_boots", "GrantDash=1", Mathf.Max(10, player.Level));
        }

        /// <summary>Practically unkillable, with mana to spare (to watch minions/enemies without dying).</summary>
        public static string God(float life = 100000f, float mana = 10000f)
        {
            PlayerStats ps = Stats();
            if (ps == null)
                return "no player";
            EnsureDash();
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

        /// <summary>Drops a pick-up-able item of every weapon type near the player.</summary>
        public static string WeaponLineup()
        {
            if (!Application.isPlaying)
                return "call WeaponLineup in Play";
            PlayerStats player = Stats();
            if (player == null)
                return "no player";

            string[] ids = { "rusty_sword", "hand_axe", "iron_mace", "steel_dagger", "bastard_sword",
                "woodsplitter", "great_mallet", "bone_sceptre", "short_bow", "gnarled_staff" };
            var rng = new System.Random(271828);
            for (int i = 0; i < ids.Length; i++)
            {
                ItemData item = ItemGenerator.Generate(rng, ids[i], 10, ItemRarity.Normal);
                if (item == null)
                    return "no weapon base " + ids[i];
                float angle = i * Mathf.PI * 2f / ids.Length;
                Vector3 at = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.45f;
                at = LootDrop.FreeSpotNear(at, 0f);
                LootDrop.Spawn(item, LootDrop.GroundBelow(at), interactive: true, id: 0);
            }
            Physics.SyncTransforms();
            return "dropped 8 melee weapons plus bow and staff as pick-up-able items near player";
        }

        /// <summary>Fills a fresh demo character's bag with varied gear and weapon categories.</summary>
        public static string InventoryVariety()
        {
            if (!Application.isPlaying)
                return "call InventoryVariety in Play";
            PlayerInventory inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            if (inventory == null)
                return "no player inventory";

            string[] ids = { "iron_helmet", "studded_vest", "leather_gloves", "leather_boots", "rope_belt",
                "jade_amulet", "iron_ring", "wooden_shield", "grimoire", "leather_quiver",
                "rusty_sword", "hand_axe", "iron_mace", "short_bow", "gnarled_staff" };
            var rng = new System.Random(731);
            var added = new List<string>();
            var full = new List<string>();
            foreach (string id in ids)
            {
                ItemData item = ItemGenerator.Generate(rng, id, 10, ItemRarity.Normal);
                if (item != null && inventory.Grid.TryAutoPlace(item))
                    added.Add(item.Name);
                else
                    full.Add(id);
            }
            return added.Count + " items in bag: " + string.Join(", ", added.ToArray())
                + (full.Count > 0 ? "; no space for: " + string.Join(", ", full.ToArray()) : "");
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

        /// <summary>Checks that both regular and skill arrow launch paths apply the 1.5 range multiplier.</summary>
        public static string CheckBowRange()
        {
            PlayerStats ps = Stats();
            if (ps == null || !Application.isPlaying) return "call CheckBowRange in Play";
            PlayerArrow a = PlayerArrow.Launch(ps.transform, 10f, 0f, Vector3.forward);
            PlayerArrow b = PlayerArrow.LaunchArrow(ps.transform, 10f, 0f, Vector3.forward, null, true);
            float regular = a.TravelRemaining, skill = b.TravelRemaining;
            UnityEngine.Object.Destroy(a.gameObject); UnityEngine.Object.Destroy(b.gameObject);
            return "basic=" + regular.ToString("0.0") + " skill=" + skill.ToString("0.0")
                + (Mathf.Abs(regular - 15f) < 0.01f && Mathf.Abs(skill - 15f) < 0.01f ? " PASS" : " FAIL");
        }

        /// <summary>Uses Dash with opposing movement input and checks its committed direction against desktop cursor aim.</summary>
        public static string CheckPcDashDirection()
        {
            PlayerSkills skills = UnityEngine.Object.FindAnyObjectByType<PlayerSkills>();
            PlayerController controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (skills == null || controller == null || UnityEngine.InputSystem.Mouse.current == null) return "no player or mouse";
            FieldInfo forced = typeof(TouchMode).GetField("forced", Any);
            bool priorForced = forced != null && (bool)forced.GetValue(null);
            Vector2 priorMove = VirtualInput.Move;
            try
            {
                if (forced != null) forced.SetValue(null, false);
                VirtualInput.Move = Vector2.zero;
                Vector3 expected = (Vector3)typeof(PlayerSkills).GetMethod("AimDirection", Any).Invoke(skills, null);
                if (expected.sqrMagnitude < 0.01f) return "cursor has no world aim; move mouse over game view";
                // The resulting dash velocity is the committed direction and ignores any later input.
                int dashSlot = -1;
                for (int k = 0; k < SkillBook.SlotCount; k++) if (skills.Slot(k) == SkillId.Dash) dashSlot = k;
                if (dashSlot < 0) return "Dash is not on the skill bar";
                ((System.Collections.IDictionary)typeof(PlayerSkills).GetField("readyAt", Any).GetValue(skills)).Clear();
                if (!skills.TryUse(dashSlot)) return "Dash could not be used";
                Vector3 actual = (Vector3)typeof(PlayerController).GetField("dashVelocity", Any).GetValue(controller);
                float dot = Vector3.Dot(actual.normalized, expected.normalized);
                return "cursor dot=" + dot.ToString("0.000") + (dot > 0.99f ? " PASS" : " FAIL");
            }
            finally
            {
                if (forced != null) forced.SetValue(null, priorForced);
                VirtualInput.Move = priorMove;
            }
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

        /// <summary>
        /// The act boss (The Shepherd), set up for a test in one call: spawned <paramref name="distance"/>
        /// ahead of the player and facing them, at the player's level unless <paramref name="level"/>
        /// is given. Clears other enemies nearby first. Once the boss has its arena this warps there.
        /// </summary>
        public static string Boss(float distance = 7f, int level = 0)
        {
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            PlayerStats ps = Stats();
            if (spawner == null || ps == null)
                return "no spawner/player";
            Clear();
            // A Shepherd left over from an earlier test (dead ones lie around under the same name).
            foreach (EnemyHealth old in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                if (old.name == "The Shepherd")
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            int index = EnemyKinds.IndexOf("The Shepherd");
            EnemyKind kind = EnemyKinds.Get(index);
            if (level <= 0)
                level = Mathf.Max(1, ps.Level);
            Vector3 forward = ps.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 at = ps.transform.position + forward * distance;
            at.y = ps.transform.position.y + 0.5f;
            GameObject go = UnityEngine.Object.Instantiate(spawner.EnemyPrefab, at, Quaternion.LookRotation(-forward));
            go.name = kind.Name;
            EnemyKinds.Apply(go, index, level);
            go.AddComponent<BossAbilities>().Configure(kind, level, spawner.EnemyPrefab);
            Physics.SyncTransforms();
            return "spawned " + kind.Name + " L" + level + " at " + at.ToString("0.0");
        }

        /// <summary>Stops (or restarts) the boss's own AI, so it stands still for looking at or for a clip.</summary>
        public static string BossHold(bool hold = true)
        {
            GameObject b = GameObject.Find("The Shepherd");
            if (b == null)
                return "no boss (DevTest.Boss first)";
            foreach (MonoBehaviour m in b.GetComponents<MonoBehaviour>())
            {
                if (m is EnemyController || m is EnemyCombat || m is BossAbilities || m is ShepherdFight)
                    m.enabled = !hold;
            }
            return hold ? "boss held" : "boss released";
        }

        /// <summary>Plays the boss's change into phase 2 (the graft), as at two thirds of his life.</summary>
        public static string BossTransition()
        {
            GameObject b = GameObject.Find("The Shepherd");
            ShepherdFight fight = b != null ? b.GetComponent<ShepherdFight>() : null;
            if (fight == null)
                return "no boss (DevTest.Boss first)";
            if (fight.Phase >= 2)
                return "already in phase 2";
            fight.Manual = false;
            fight.BeginPhase2();
            return "grafting";
        }

        /// <summary>Repeats the reveal then all phase-three clips on a clear test floor for user review.</summary>
        public static string BossReview3(bool skyOnly = false)
        {
            if (!Application.isPlaying || Stats() == null) return "call in a running Play session";
            GameObject old = GameObject.Find("SaintReviewFloor");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SaintReviewFloor";
            floor.transform.position = new Vector3(500f, -0.5f, 500f);
            floor.transform.localScale = new Vector3(120f, 1f, 120f);
            Warp("500,500", 0f);
            Stats().transform.rotation = Quaternion.identity;
            God();
            Time.timeScale = 1f;
            float cycleAt = -100f, frameAt = 0f;
            float replayAt = 0f;
            int stage = 0;
            string lastClip = "";
            EditorApplication.CallbackFunction review = null;
            review = () =>
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= review;
                    End();
                    return;
                }
                float elapsed = Time.time - cycleAt;
                if (elapsed > 24f && (!skyOnly || stage < 3))
                {
                    Boss(7f);
                    BossHold();
                    var fight = GameObject.Find("The Shepherd").GetComponent<ShepherdFight>();
                    fight.Manual = true;
                    fight.enabled = true; // Allow Start before the instant phase-two setup.
                    cycleAt = Time.time;
                    stage = 0;
                    lastClip = "";
                    return;
                }
                if (stage == 0 && elapsed > 0.35f) { BossPhase(2); BossHold(); stage = 1; }
                if (stage == 1 && elapsed > 0.65f) { BossReveal3(); stage = 2; }
                if (stage == 2 && elapsed > 4.3f) { BossLook3View(); BossAnim3(skyOnly ? "SkyBite" : "demo"); stage = 3; }
                if (stage != 3) return;
                var boss = GameObject.Find("The Shepherd");
                var animator = boss != null ? boss.GetComponentInChildren<CarrionSaintAnimator>() : null;
                if (animator == null) return;
                string current = animator.Current ?? "";
                if (skyOnly && current == "")
                {
                    if (replayAt == 0f) replayAt = Time.time + 0.85f;
                    if (Time.time >= replayAt) { BossAnim3("SkyBite"); replayAt = 0f; lastClip = ""; }
                    return; // Hold the wide view between sky bites.
                }
                if (current != lastClip) { frameAt = Time.time + (lastClip == "SkyBite" && current == "" ? 0.55f : 0.06f); lastClip = current; }
                if (frameAt > 0f && Time.time >= frameAt)
                {
                    BossLook3View(current == "SkyBite", true);
                    frameAt = 0f;
                }
            };
            EditorApplication.update += review;
            return skyOnly ? "isolated sky-bite review; Stop Play restores your save"
                : "review repeats fake death/reveal and phase-three animations every 24s; Stop Play restores your save";
        }

        /// <summary>Plays the phase-three fake death/reveal and holds the result for animation review.</summary>
        public static string BossReveal3()
        {
            GameObject boss = GameObject.Find("The Shepherd");
            var fight = boss != null ? boss.GetComponent<ShepherdFight>() : null;
            if (fight == null) return "no boss";
            if (fight.Phase != 2) return "use BossPhase(2) first";
            Time.timeScale = 1f;
            fight.Manual = true;
            fight.BeginPhase3();
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraSystem.CameraFollow>();
                if (follow != null) follow.enabled = false;
                Quaternion angle = boss.transform.rotation * Quaternion.Euler(24f, 145f, 0f);
                cam.transform.SetPositionAndRotation(boss.transform.position + Vector3.up * 0.8f + angle * Vector3.back * 27f, angle);
            }
            return "phase-three reveal started; result held immune for review";
        }

        public static string BossAnim3(string clip = "demo")
        {
            GameObject boss = GameObject.Find("The Shepherd");
            var animator = boss != null ? boss.GetComponentInChildren<CarrionSaintAnimator>() : null;
            if (animator == null) return "use BossReveal3 or BossPhase(3) first";
            Time.timeScale = 1f;
            if (clip == "stop") { animator.Demo = false; animator.Stop(); return "phase-three demo stopped"; }
            if (clip == "demo") { animator.Demo = true; return "phase-three animation demo looping"; }
            animator.Demo = false;
            return animator.Play(clip) ? "playing " + clip : "no phase-three clip " + clip;
        }

        public static string BossState3()
        {
            GameObject boss = GameObject.Find("The Shepherd");
            if (boss == null) return "no boss";
            var health = boss.GetComponent<EnemyHealth>();
            var reveal = boss.GetComponent<CarrionSaintReveal>();
            var animator = boss.GetComponentInChildren<CarrionSaintAnimator>();
            return health.DisplayName + " life=" + health.CurrentHealth + "/" + health.MaxHealth
                + " dead=" + health.IsDead + " immune=" + health.Immune + " bar hidden=" + health.HideBossBar
                + " stage=" + (reveal != null ? reveal.Stage : "preview")
                + " clip=" + (animator != null ? animator.Current : "none");
        }

        /// <summary>Switches the boss to phase 2, or holds the visual-only phase-3 preview.</summary>
        public static string BossPhase(int phase)
        {
            GameObject b = GameObject.Find("The Shepherd");
            if (b == null)
                return "no boss (DevTest.Boss first)";
            if (phase == 3)
            {
                BossHold();
                b.GetComponent<EnemyHealth>().Immune = true;
                CarrionSaintLook.Build(b.transform);
                return BossLook3View();
            }
            if (phase != 2)
                return "no phase " + phase + " yet";
            ShepherdFight fight = b.GetComponent<ShepherdFight>();
            if (fight != null)
                fight.EnterPhase2();
            else
                ShepherdLook.ToPhase2(b.transform);
            Physics.SyncTransforms();
            return "boss in phase " + phase;
        }

        /// <summary>Frames the phase-3 body or its colossal attack snakes. Visual review, no combat.</summary>
        public static string BossLook3View(bool serpents = false, bool smooth = false)
        {
            GameObject boss = GameObject.Find("The Shepherd");
            Transform rig = boss != null ? boss.transform.Find("Model/" + CarrionSaintLook.RigName) : null;
            if (rig == null) return "use BossPhase(3) first";
            CarrionSaintLook.ShowSerpents(boss.transform, serpents);
            foreach (var loot in UnityEngine.Object.FindObjectsByType<World.LootDrop>(FindObjectsSortMode.None))
                loot.gameObject.SetActive(false);
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return "no camera";
            var follow = cam.GetComponent<PoeClone.CameraSystem.CameraFollow>();
            if (follow != null) follow.enabled = false;
            // Include both the creature and player as a scale reference.
            Bounds bounds = new Bounds(rig.position + Vector3.up * 3f, Vector3.zero);
            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>())
                if (r.enabled && !r.name.StartsWith("Outline_")) bounds.Encapsulate(r.bounds);
            PlayerStats player = Stats();
            if (player != null) bounds.Encapsulate(player.transform.position);
            float halfAngle = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
            float fit = bounds.extents.magnitude / Mathf.Sin(halfAngle) * 1.15f;
            Quaternion angle = boss.transform.rotation * Quaternion.Euler(28f, 155f, 0f);
            Vector3 destination = bounds.center + angle * Vector3.back * fit;
            if (!smooth) cam.transform.SetPositionAndRotation(destination, angle);
            else
            {
                Vector3 from = cam.transform.position;
                Quaternion rotation = cam.transform.rotation;
                float started = Time.unscaledTime;
                EditorApplication.CallbackFunction frame = null;
                frame = () =>
                {
                    if (!EditorApplication.isPlaying || cam == null) { EditorApplication.update -= frame; return; }
                    float f = Mathf.Clamp01((Time.unscaledTime - started) / 0.5f);
                    float ease = Mathf.SmoothStep(0f, 1f, f);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(from, destination, ease), Quaternion.Slerp(rotation, angle, ease));
                    if (f >= 1f) EditorApplication.update -= frame;
                };
                EditorApplication.update += frame;
            }
            return serpents ? "phase-3 serpent scale preview (7.2m thick, 48m long each)" : "phase-3 body preview; combat held";
        }

        private static string bossMotionResult;

        /// <summary>Repeatable root-motion/damage check on an isolated floor. Read BossMotionResult after 2s.
        /// Scenarios: clear, wall, inside, outside, beyond, bite. No production arena changes.</summary>
        public static string BossMotionCheck(string scenario = "clear")
        {
            PlayerStats ps = Stats();
            if (ps == null) return "no player";
            GameObject old = GameObject.Find("BossMotionTest");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var stage = new GameObject("BossMotionTest");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(stage.transform);
            floor.transform.position = new Vector3(500f, -0.5f, 500f);
            floor.transform.localScale = new Vector3(80f, 1f, 80f);
            Warp("500,500", 0f);
            ps.transform.rotation = Quaternion.identity;
            Boss(10f);
            BossHold();
            GameObject boss = GameObject.Find("The Shepherd");
            Vector3 start = new Vector3(500f, 1.6f, 510f);
            var fight = boss.GetComponent<ShepherdFight>();
            fight.Manual = true;
            fight.enabled = true;
            var cc = boss.GetComponent<CharacterController>();
            cc.enabled = false;
            boss.transform.position = start;
            cc.enabled = true;
            if (scenario == "wall")
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(stage.transform);
                wall.transform.position = new Vector3(500f, 4f, 505f);
                wall.transform.localScale = new Vector3(15f, 8f, 0.5f);
            }
            God();
            Physics.SyncTransforms();
            bossMotionResult = "pending " + scenario;
            // Let Start initialize the newly spawned rig, then force its real move.
            float beginAt = Time.time + 0.15f;
            float finishAt = beginAt + 1.5f;
            bool started = false;
            float life = ps.CurrentHealth;
            bool statsEnabled = ps.enabled;
            string warning = "";
            EditorApplication.CallbackFunction measure = null;
            measure = () =>
            {
                if (!EditorApplication.isPlaying || boss == null)
                {
                    EditorApplication.update -= measure;
                    if (ps != null) ps.enabled = statsEnabled;
                    return;
                }
                if (!started && Time.time >= beginAt)
                {
                    started = true;
                    var anim = boss.GetComponentInChildren<ShepherdAnimator>();
                    anim.Target = ps.transform;
                    anim.MinGap = 0.5f * boss.transform.localScale.x + 2f;
                    if (scenario == "bite") BossPhase(2);
                    boss.GetComponent<ShepherdFight>().Manual = true;
                    BossMove(scenario == "bite" ? "Bite" : "CobraLunge");
                    // Direction is committed. Move the player to the chosen point before the hit.
                    float lateral = scenario == "inside" ? 2.1f : scenario == "outside" ? 3.2f : 10f;
                    float along = scenario == "beyond" ? 14.5f : 10f;
                    if (scenario == "beyond" || scenario == "bite") lateral = 0f;
                    if (scenario == "bite") along = 3.2f;
                    var pc = ps.GetComponent<CharacterController>();
                    pc.enabled = false;
                    ps.transform.position = new Vector3(500f + lateral, 1.1f, 510f - along);
                    pc.enabled = true;
                    Physics.SyncTransforms();
                    var strip = GameObject.Find("LineTelegraph");
                    if (strip != null) warning = strip.transform.GetChild(0).localScale.ToString("0.00");
                    life = ps.CurrentHealth;
                    ps.enabled = false; // Keep the large God-mode regen from erasing a test hit.
                }
                if (started && Time.time >= finishAt)
                {
                    Vector3 delta = boss.transform.position - start;
                    delta.y = 0f;
                    Vector3 gap = ps.transform.position - boss.transform.position;
                    gap.y = 0f;
                    bossMotionResult = scenario + ": travel=" + delta.magnitude.ToString("0.00")
                        + " gap=" + gap.magnitude.ToString("0.00") + " damage=" + (life - ps.CurrentHealth).ToString("0.00")
                        + " warning=" + warning;
                    EditorApplication.update -= measure;
                    ps.enabled = statsEnabled;
                }
            };
            EditorApplication.update += measure;
            return bossMotionResult;
        }

        public static string BossMotionResult() => bossMotionResult ?? "no check yet";

        /// <summary>Holds the boss and plays one of its clips (ShepherdAnimator.Clips: Sweep, Jab, Slam, HookPull, CobraLunge, SerpentCall).</summary>
        public static string BossAnim(string clip, float speed = 1f)
        {
            GameObject b = GameObject.Find("The Shepherd");
            ShepherdAnimator anim = b != null ? b.GetComponentInChildren<ShepherdAnimator>() : null;
            if (anim == null)
                return "no boss (DevTest.Boss first)";
            ShepherdAnimator.Clip c = ShepherdAnimator.Find(clip);
            if (c == null)
                return "no clip " + clip;
            BossHold();
            anim.Demo = false;
            anim.Play(c, speed);
            return "playing " + c.Name + " (" + c.Duration.ToString("0.00") + "s)";
        }

        /// <summary>
        /// Makes the boss do one of its moves now, with its warnings and damage. From then on he
        /// only does forced moves (he still walks) until BossMove("auto") hands him back his own choices.
        /// <paramref name="freezeAt"/> &gt; 0 pauses the game that many seconds in, for a screenshot
        /// (Time.timeScale = 0: BossMove("resume") or any later BossMove lets it run again).
        /// </summary>
        public static string BossMove(string move, float freezeAt = 0f)
        {
            GameObject b = GameObject.Find("The Shepherd");
            ShepherdFight fight = b != null ? b.GetComponent<ShepherdFight>() : null;
            if (fight == null)
                return "no boss (DevTest.Boss first)";
            Time.timeScale = 1f;
            if (move == "resume")
                return "resumed";
            if (move == "auto")
            {
                fight.Manual = false;
                return "boss picks his own moves again";
            }
            fight.Manual = true;
            if (!fight.Force(move))
                return "no move " + move;
            if (freezeAt > 0f)
            {
                float at = Time.time + freezeAt;
                EditorApplication.CallbackFunction pause = null;
                pause = () =>
                {
                    if (!EditorApplication.isPlaying || Time.time >= at)
                    {
                        EditorApplication.update -= pause;
                        if (EditorApplication.isPlaying)
                            Time.timeScale = 0f;
                    }
                };
                EditorApplication.update += pause;
            }
            return "doing " + move + (freezeAt > 0f ? ", pausing " + freezeAt + "s in" : "");
        }

        /// <summary>Holds the boss frozen <paramref name="at"/> seconds into a clip, for judging a pose.</summary>
        public static string BossPose(string clip, float at)
        {
            GameObject b = GameObject.Find("The Shepherd");
            ShepherdAnimator anim = b != null ? b.GetComponentInChildren<ShepherdAnimator>() : null;
            ShepherdAnimator.Clip c = ShepherdAnimator.Find(clip);
            if (anim == null || c == null)
                return "no boss or clip";
            BossHold();
            anim.Demo = false;
            anim.Freeze(c, at);
            return "frozen " + c.Name + " @" + at;
        }

        /// <summary>
        /// Judging poses in one picture: a row of frozen Shepherds, one per "Clip@seconds" in
        /// <paramref name="spec"/> (comma separated), 3.4 m apart along +X starting at the player
        /// plus (<paramref name="dx"/>, <paramref name="dz"/>), all facing +X (seen side-on from -Z).
        /// Returns a camera position and target for a positioned screenshot. Remove with Clear().
        /// </summary>
        public static string BossLineup(string spec, float dx = -8f, float dz = -14f)
        {
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            PlayerStats ps = Stats();
            if (spawner == null || ps == null)
                return "no spawner/player";
            int index = EnemyKinds.IndexOf("The Shepherd");
            string[] items = spec.Split(',');
            Vector3 start = ps.transform.position + new Vector3(dx, 0.5f, dz);
            for (int i = 0; i < items.Length; i++)
            {
                string[] parts = items[i].Trim().Split('@');
                ShepherdAnimator.Clip c = ShepherdAnimator.Find(parts[0]);
                if (c == null)
                    return "no clip " + parts[0];
                float at = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : c.Duration * 0.5f;
                GameObject go = UnityEngine.Object.Instantiate(spawner.EnemyPrefab, start + Vector3.right * (3.4f * i), Quaternion.LookRotation(Vector3.right));
                go.name = "Lineup " + c.Name;
                EnemyKinds.Apply(go, index, 1);
                foreach (MonoBehaviour m in go.GetComponents<MonoBehaviour>())
                {
                    if (m is EnemyController || m is EnemyCombat)
                        m.enabled = false;
                }
                go.GetComponentInChildren<ShepherdAnimator>().Freeze(c, at);
            }
            Physics.SyncTransforms();
            Vector3 mid = start + Vector3.right * (3.4f * (items.Length - 1) * 0.5f);
            Vector3 cam = mid + new Vector3(0f, 2.5f, -4f - 1.9f * items.Length);
            return "lineup of " + items.Length + "; camera " + cam.ToString("0.0") + " -> " + (mid + Vector3.up * 0.6f).ToString("0.0");
        }

        /// <summary>Holds the boss and loops all its clips, a second apart, each name shown overhead. Off with on=false.</summary>
        public static string BossDemo(bool on = true)
        {
            GameObject b = GameObject.Find("The Shepherd");
            ShepherdAnimator anim = b != null ? b.GetComponentInChildren<ShepherdAnimator>() : null;
            if (anim == null)
                return "no boss (DevTest.Boss first)";
            BossHold(on);
            anim.Demo = on;
            return on ? "demo looping " + ShepherdAnimator.Clips.Length + " clips" : "demo off";
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
                    e.Immune = false;
                    e.Floor = 0f;
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
            return "entering " + areas.areas[area].areaName;
        }

        /// <summary>First-release boss check: unlock the storyline gate and enter the real arena.</summary>
        public static string BossArena()
        {
            Quest("stag", true);
            Quest("shepherd");
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory.PlayerInventory>();
            if (inventory != null)
            {
                inventory.Grid.TryAutoPlace(Inventory.UniqueItems.Current("Shepherd's Fang"));
                inventory.Grid.TryAutoPlace(Inventory.UniqueItems.Current("Widow's Choir"));
                inventory.Grid.TryAutoPlace(Inventory.ItemData.ReawakeningItem());
            }
            return God() + " || " + Area(World.WorldBuilder.ActArena);
        }

        public static string BossArenaHit()
        {
            var arena = World.ActBossArena.Instance;
            if (arena == null || arena.Boss == null) return "no arena boss";
            arena.Boss.TakeDamage(arena.Boss.MaxHealth * 10f);
            return BossArenaStatus();
        }

        public static string BossArenaStatus()
        {
            var arena = World.ActBossArena.Instance;
            if (arena == null) return "no arena";
            var health = arena.Boss;
            if (health == null) return "boss absent; respawn=" + arena.RespawnRemaining;
            var fight = health.GetComponent<Enemies.ShepherdFight>();
            var chase = UnityEngine.Object.FindAnyObjectByType<Enemies.SerpentPursuit>();
            return "area=" + World.AreaManager.Instance.CurrentAreaIndex + " door=" + World.ActBossArena.DoorOpen
                + " phase=" + (fight != null ? fight.Phase : 0) + " name=" + health.DisplayName
                + " life=" + health.CurrentHealth + "/" + health.MaxHealth + " dead=" + health.IsDead + " immune=" + health.Immune
                + " route=" + (chase != null ? chase.RoutePoints : 0) + " respawn=" + arena.RespawnRemaining;
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
