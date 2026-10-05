using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Quests;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Player
{
    /// <summary>
    /// Keeps the character between visits (browser storage via PlayerPrefs): loads the save once
    /// the player is let in, then saves every few seconds while playing, on changing area and when
    /// the page is hidden or closed. A loaded character with gear starts in Haven without the
    /// starter gear on the ground (one with none keeps it). Spectators never load or save. Installed by GameSessionController.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string LegacyKey = "PoeClone.Save.v1";
        private const string LegacyProfileSaveKey = "PoeClone.CharacterSave.legacy";
        private const string ProfilesKey = "PoeClone.CharacterProfiles.v1";
        private const string ActiveProfileKey = "PoeClone.ActiveCharacter.v1";
        private const string SharedStashKey = "PoeClone.SharedStash.v1";
        private static string Key => "PoeClone.CharacterSave." + ActiveProfileId;
        public static string ActiveProfileId { get; private set; } = "legacy";
        public static string ActiveCharacterName { get; private set; } = "";

        [System.Serializable] private class ProfileList { public List<Profile> profiles = new List<Profile>(); }
        [System.Serializable] public class Profile { public string id; public string name; }

        public static List<Profile> Profiles()
        {
            ProfileList list = null;
            try { if (PlayerPrefs.HasKey(ProfilesKey)) list = JsonUtility.FromJson<ProfileList>(PlayerPrefs.GetString(ProfilesKey)); } catch { }
            if (list == null) list = new ProfileList();
            if (list.profiles == null) list.profiles = new List<Profile>();
            bool hasLegacy = false;
            foreach (Profile p in list.profiles) if (p != null && p.id == "legacy") hasLegacy = true;
            // The first multi-character release copied the old save to this key. Recover its
            // profile even if the original key has since disappeared or the roster was reset.
            if (!hasLegacy && (PlayerPrefs.HasKey(LegacyKey) || PlayerPrefs.HasKey(LegacyProfileSaveKey)))
            {
                if (!PlayerPrefs.HasKey(LegacyProfileSaveKey))
                    PlayerPrefs.SetString(LegacyProfileSaveKey, PlayerPrefs.GetString(LegacyKey));
                list.profiles.Insert(0, new Profile { id = "legacy", name = PlayerPrefs.GetString("PoeClone.PlayerName", "Wanderer") });
                PlayerPrefs.SetString(ProfilesKey, JsonUtility.ToJson(list)); PlayerPrefs.Save();
            }
            return list.profiles;
        }

        public static void SelectProfile(string id)
        {
            foreach (Profile p in Profiles()) if (p.id == id) { ActiveProfileId = p.id; ActiveCharacterName = p.name; PlayerPrefs.SetString(ActiveProfileKey, id); return; }
        }

        public static void CreateProfile(string name)
        {
            var profiles = Profiles();
            string id = System.Guid.NewGuid().ToString("N");
            profiles.Add(new Profile { id = id, name = name.Trim() });
            PlayerPrefs.SetString(ProfilesKey, JsonUtility.ToJson(new ProfileList { profiles = profiles }));
            PlayerPrefs.SetString(ActiveProfileKey, id); PlayerPrefs.Save();
            ActiveProfileId = id; ActiveCharacterName = name.Trim();
        }

        public static bool DeleteProfile(string id)
        {
            var profiles = Profiles();
            int index = profiles.FindIndex(p => p != null && p.id == id);
            if (index < 0) return false;

            profiles.RemoveAt(index);
            PlayerPrefs.DeleteKey("PoeClone.CharacterSave." + id);
            if (id == "legacy")
            {
                PlayerPrefs.DeleteKey(LegacyKey);
                PlayerPrefs.DeleteKey(LegacyProfileSaveKey);
            }
            PlayerPrefs.SetString(ProfilesKey, JsonUtility.ToJson(new ProfileList { profiles = profiles }));
            if (PlayerPrefs.GetString(ActiveProfileKey, "") == id)
                PlayerPrefs.DeleteKey(ActiveProfileKey);
            if (ActiveProfileId == id)
            {
                ActiveProfileId = "";
                ActiveCharacterName = "";
            }
            PlayerPrefs.Save();
            return true;
        }

        public static int ProfileLevel(Profile p)
        {
            var d = ProfileSave(p);
            return d != null ? Mathf.Max(1, d.level) : 1;
        }

        public static SaveData ProfileSave(Profile p)
        {
            string key = "PoeClone.CharacterSave." + p.id;
            if (p.id == "legacy" && !PlayerPrefs.HasKey(key)) key = LegacyKey;
            try { return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key, "")); }
            catch { return null; }
        }
        private const float SaveEvery = 10f;

        private static SaveSystem instance;
        public static bool CharacterLoaded => instance != null && instance.loaded;

        public static void LoadSelectedProfile()
        {
            if (instance != null) instance.TryLoadSelectedProfile();
        }

        public static void SaveBeforeCharacterSwitch()
        {
            if (instance != null && instance.loaded && !instance.erased)
                instance.Save();
        }

        private bool loaded;
        private bool erased;
        private float nextSave;
        private AreaManager areas;
        private PlayerStats stats;

        public static bool HasSave => PlayerPrefs.HasKey(Key) || (ActiveProfileId == "legacy" && PlayerPrefs.HasKey(LegacyKey));

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (areas != null)
                areas.AreaChanged -= OnAreaChanged;
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Forgets the saved character and stops saving this session, so the next visit starts
        /// fresh (the current one keeps going as it is).
        /// </summary>
        public static void Erase()
        {
            PlayerPrefs.DeleteKey(Key);
            if (ActiveProfileId == "legacy") PlayerPrefs.DeleteKey(LegacyKey);
            PlayerPrefs.Save();
            if (instance != null)
                instance.erased = true;
        }

        private static bool Playing()
        {
            var session = GameSessionController.Instance;
            return session != null && session.Role == SessionRole.Player && session.PlayGranted;
        }

        private void Update()
        {
            if (erased || !Playing())
                return;

            if (!loaded && !TryLoadSelectedProfile())
                return;

            if (Time.unscaledTime >= nextSave)
            {
                nextSave = Time.unscaledTime + SaveEvery;
                Save();
            }
        }

        private bool TryLoadSelectedProfile()
        {
            if (loaded) return true;
            if (!Playing()) return false;

            if (stats == null)
            {
                var players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (players.Length > 0) stats = players[0];
            }
            if (stats == null || QuestLog.Instance == null || AreaManager.Instance == null || AreaManager.Instance.CurrentAreaIndex < 0)
                return false;

            if (areas == null)
            {
                areas = AreaManager.Instance;
                areas.AreaChanged += OnAreaChanged;
            }

            Load(stats);
            // A save can name skills from gear this character no longer wears, and a new
            // character must not inherit bindings from the scene player or another profile.
            stats.GetComponent<PlayerSkills>()?.ClearUnavailableSlots();
            loaded = true;
            nextSave = Time.unscaledTime + SaveEvery;
            GameSessionController.Instance?.NotifyCharacterLoaded();
            return true;
        }

        private void OnAreaChanged(int index)
        {
            if (loaded && !erased && Playing())
                Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && loaded && !erased && Playing())
                Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && loaded && !erased && Playing())
                Save();
        }

        /// <summary>
        /// Saves right now. Called by the web page (SendMessage to the GameSessionController
        /// object) just before it reloads for a new version of the game.
        /// </summary>
        public void SaveForUpdate()
        {
            if (loaded && !erased && Playing())
                Save();
        }

        private void OnApplicationQuit()
        {
            if (loaded && !erased && Playing())
                Save();
        }

        // ------------------------------------------------------------------ save

        private void Save()
        {
            if (stats == null)
                return;

            var data = new SaveData
            {
                level = stats.Level, experience = stats.Experience, gearSkills = true, barLayoutVersion = 1,
                passiveAllocationVersion = SaveData.CurrentPassiveAllocationVersion
            };

            PlayerInventory inventory = stats.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                data.CaptureInventory(inventory);
                var shared = new SaveData();
                shared.CaptureStash(inventory);
                PlayerPrefs.SetString(SharedStashKey, JsonUtility.ToJson(shared));
                // Stash belongs to the account; character saves contain only personal gear and bag.
                data.stash.Clear();
                data.stashTabs.Clear();
                data.stashTabNames.Clear();
            }

            PlayerPotions potions = stats.GetComponent<PlayerPotions>();
            if (potions != null)
            {
                data.healthPotions = potions.HealthPotions;
                data.manaPotions = potions.ManaPotions;
            }

            PlayerPassives passives = stats.GetComponent<PlayerPassives>();
            if (passives != null)
            {
                data.passives.AddRange(passives.Allocation.Taken);
                data.respecCharges = passives.RespecCharges;
            }

            PlayerSkills skills = stats.GetComponent<PlayerSkills>();
            if (skills != null)
            {
                for (int k = 0; k < SkillBook.SlotCount; k++)
                {
                    SkillId? id = skills.Slot(k);
                    data.skillSlots.Add(id.HasValue ? (int)id.Value : -1);
                    data.skillSourceSlots.Add(skills.SourceSlotAt(k));
                    data.skillGrantLevels.Add(skills.GrantLevelAt(k));
                }
            }

            QuestLog log = QuestLog.Instance;
            if (log != null)
            {
                var active = new Dictionary<string, int>();
                log.Export(data.questsDone, active, data.visited, data.questProps);
                data.questBook = QuestBook.Version;
                data.waystonesReset = true;
                foreach (var pair in active)
                    data.questsActive.Add(new QuestRecord { id = pair.Key, progress = pair.Value });
            }

            data.mapSeen = MinimapTerrain.Export();

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ load

        private void Load(PlayerStats stats)
        {
            if (!PlayerPrefs.HasKey(Key) && ActiveProfileId == "legacy" && PlayerPrefs.HasKey(LegacyKey))
                PlayerPrefs.SetString(Key, PlayerPrefs.GetString(LegacyKey));
            if (!PlayerPrefs.HasKey(Key))
            {
                RestoreSharedStash(stats.GetComponent<PlayerInventory>());
                return;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SaveSystem: unreadable save, starting fresh. " + e.Message);
                return;
            }
            if (data == null || data.version != SaveData.CurrentVersion)
            {
                RestoreSharedStash(stats.GetComponent<PlayerInventory>());
                return;
            }

            // A returning character brings their own gear: the starter items go. One saved before
            // picking anything up keeps them, or it would be left with nothing for good.
            bool hasGear = (data.bag != null && data.bag.Count > 0) || (data.equipped != null && data.equipped.Count > 0);
            if (hasGear)
            {
                foreach (LootDrop drop in LootDrop.All.ToArray())
                {
                    if (drop != null)
                        Destroy(drop.gameObject);
                }
            }

            stats.RestoreProgress(data.level, data.experience);

            PlayerInventory inventory = stats.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                foreach (ItemData item in data.RestoreInventory(inventory, false))
                    inventory.ThrowAway(item);
                RestoreSharedStash(inventory, data);

                // Characters from before skills came from gear learned their spells by levelling:
                // they get a staff with Fire Bolt at about the level they'd have found by now.
                if (!data.gearSkills)
                {
                    int spellLevel = Mathf.Clamp(1 + (data.level - 1) / 4, 1, SkillGrants.MaxDropLevel);
                    ItemData staff = ItemGenerator.Generate(new System.Random(), "gnarled_staff", 1, ItemRarity.Normal, StatType.GrantFireBolt);
                    var mods = new List<StatModifier>();
                    foreach (StatModifier m in staff.Modifiers)
                        mods.Add(m.Stat == StatType.GrantFireBolt ? new StatModifier(m.Stat, spellLevel) : m);
                    var gift = new ItemData(staff.Id, staff.Name, staff.Type, staff.Width, staff.Height, staff.Tint, mods,
                        false, staff.WeaponType, staff.Rarity);
                    ItemGenerator.ApplyArt(gift);
                    if (!inventory.Grid.TryAutoPlace(gift))
                        inventory.ThrowAway(gift);
                }
            }

            PlayerPotions potions = stats.GetComponent<PlayerPotions>();
            if (potions != null)
                potions.SetCounts(data.healthPotions, data.manaPotions);

            bool resetPassives = data.passiveAllocationVersion < SaveData.CurrentPassiveAllocationVersion;
            PlayerPassives passives = stats.GetComponent<PlayerPassives>();
            if (passives != null)
            {
                if (resetPassives)
                    passives.Allocation.ResetAll();
                else if (data.passives != null)
                    passives.Restore(data.passives);
                passives.SetRespecCharges(data.respecCharges);
            }

            PlayerSkills skills = stats.GetComponent<PlayerSkills>();
            if (skills != null && data.skillSlots != null)
            {
                for (int k = 0; k < data.skillSlots.Count; k++)
                {
                    // Earlier saves used Q/E/R/F, RMB/MMB/M4/M5, then 1-4.
                    int slot = data.barLayoutVersion == 0 && data.skillSlots.Count == 12
                        ? (k < 4 ? k : k == 4 ? 8 : k == 5 ? -1 : k == 6 ? 9 : k == 7 ? 10 : k - 4)
                        : k;
                    if (slot < 0 || slot >= SkillBook.SlotCount) continue;
                    skills.ClearSlot(slot);
                    if (data.skillSlots[k] < 0)
                        continue;
                    else if (data.skillSourceSlots != null && k < data.skillSourceSlots.Count &&
                             data.skillGrantLevels != null && k < data.skillGrantLevels.Count &&
                             data.skillSourceSlots[k] >= 0 && data.skillSourceSlots[k] < SlotRules.AllSlots.Length)
                        skills.Assign(slot, (SkillId)data.skillSlots[k], (EquipSlot)data.skillSourceSlots[k], data.skillGrantLevels[k]);
                    else
                        skills.Assign(slot, (SkillId)data.skillSlots[k]); // old saves: choose one current grant
                }
            }

            QuestLog log = QuestLog.Instance;
            // Quests saved under an older quest book start over (the places visited are kept).
            // Saves from before the one-time waystone reset forget the places visited (once).
            bool questsCurrent = data.questBook == QuestBook.Version;
            if (log != null)
            {
                var active = new List<KeyValuePair<string, int>>();
                if (questsCurrent && data.questsActive != null)
                {
                    foreach (QuestRecord q in data.questsActive)
                        active.Add(new KeyValuePair<string, int>(q.id, q.progress));
                }
                log.Import(questsCurrent && data.questsDone != null ? data.questsDone : new List<string>(), active,
                    data.waystonesReset && data.visited != null ? data.visited : new List<int>(), questsCurrent && data.questProps != null ? data.questProps : new List<string>());
            }
            MinimapTerrain.Import(data.mapSeen);

            // Gear and passives raised the maximums after the level was restored: start full.
            stats.Heal(stats.MaxHealth);
            stats.RestoreMana(stats.MaxMana);

            CombatText.Show(stats.transform.position + Vector3.up * 2.4f,
                questsCurrent ? "Welcome back" : "Welcome back - the story begins anew: talk to Elder Maren",
                new Color(1f, 0.85f, 0.4f), 1f);

            if (resetPassives)
            {
                // Persist the migration now so a reload cannot restore the old allocation.
                data.passives = new List<string>();
                data.passiveAllocationVersion = SaveData.CurrentPassiveAllocationVersion;
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
        }

        private static void RestoreSharedStash(PlayerInventory inventory, SaveData oldCharacter = null)
        {
            if (inventory == null) return;
            SaveData shared = null;
            if (PlayerPrefs.HasKey(SharedStashKey))
            {
                try { shared = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SharedStashKey)); }
                catch (System.Exception e) { Debug.LogWarning("Shared stash could not be read: " + e.Message); }
            }
            else
            {
                // Recover stash contents from existing character saves the first time this version runs.
                shared = oldCharacter ?? new SaveData();
                if (shared.stash == null) shared.stash = new List<PlacedRecord>();
                if (shared.stashTabs == null) shared.stashTabs = new List<StashTabRecord>();
                while (shared.stashTabs.Count < PlayerInventory.StashTabCount - 1)
                    shared.stashTabs.Add(new StashTabRecord());
                for (int tab = 0; tab < shared.stashTabs.Count; tab++)
                {
                    if (shared.stashTabs[tab] == null) shared.stashTabs[tab] = new StashTabRecord();
                    if (shared.stashTabs[tab].items == null) shared.stashTabs[tab].items = new List<PlacedRecord>();
                }
                if (shared.stashTabNames == null) shared.stashTabNames = new List<string>();
                while (shared.stashTabNames.Count < PlayerInventory.StashTabCount)
                    shared.stashTabNames.Add("");
                foreach (Profile profile in Profiles())
                {
                    if (profile.id == ActiveProfileId) continue;
                    SaveData other = ProfileSave(profile);
                    if (other == null) continue;
                    for (int tab = 0; tab < PlayerInventory.StashTabCount; tab++)
                    {
                        List<PlacedRecord> source = tab == 0 ? other.stash :
                            other.stashTabs != null && tab - 1 < other.stashTabs.Count ? other.stashTabs[tab - 1]?.items : null;
                        if (source == null) continue;
                        List<PlacedRecord> target = tab == 0 ? shared.stash : shared.stashTabs[tab - 1].items;
                        foreach (PlacedRecord item in source)
                            if (item != null) target.Add(item);
                        if (other.stashTabNames != null && tab < other.stashTabNames.Count &&
                            !string.IsNullOrEmpty(other.stashTabNames[tab]) && string.IsNullOrEmpty(shared.stashTabNames[tab]))
                            shared.stashTabNames[tab] = other.stashTabNames[tab];
                    }
                }
            }
            if (shared == null) return;
            List<ItemData> overflow = shared.RestoreStash(inventory);
            foreach (ItemData item in overflow)
            {
                bool stored = false;
                foreach (InventoryGrid tab in inventory.StashTabs)
                    if (tab.TryAutoPlace(item)) { stored = true; break; }
                if (!stored) inventory.ThrowAway(item);
            }
            if (!PlayerPrefs.HasKey(SharedStashKey))
            {
                shared.CaptureStash(inventory);
                PlayerPrefs.SetString(SharedStashKey, JsonUtility.ToJson(shared));
                PlayerPrefs.Save();
            }
        }
    }
}
