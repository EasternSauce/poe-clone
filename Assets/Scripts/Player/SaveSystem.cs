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
        private const string Key = "PoeClone.Save.v1";
        private const float SaveEvery = 10f;

        private static SaveSystem instance;

        private bool loaded;
        private bool erased;
        private float nextSave;
        private AreaManager areas;
        private PlayerStats stats;

        public static bool HasSave => PlayerPrefs.HasKey(Key);

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

            if (stats == null)
                stats = FindAnyObjectByType<PlayerStats>();
            if (stats == null || QuestLog.Instance == null || AreaManager.Instance == null || AreaManager.Instance.CurrentAreaIndex < 0)
                return;

            if (areas == null)
            {
                areas = AreaManager.Instance;
                areas.AreaChanged += OnAreaChanged;
            }

            if (!loaded)
            {
                loaded = true;
                Load(stats);
                nextSave = Time.unscaledTime + SaveEvery;
                return;
            }

            if (Time.unscaledTime >= nextSave)
            {
                nextSave = Time.unscaledTime + SaveEvery;
                Save();
            }
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

            var data = new SaveData { level = stats.Level, experience = stats.Experience, gearSkills = true };

            PlayerInventory inventory = stats.GetComponent<PlayerInventory>();
            if (inventory != null)
                data.CaptureInventory(inventory);

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
                }
            }

            QuestLog log = QuestLog.Instance;
            if (log != null)
            {
                var active = new Dictionary<string, int>();
                log.Export(data.questsDone, active, data.visited, data.questProps);
                data.questBook = QuestBook.Version;
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
            if (!PlayerPrefs.HasKey(Key))
                return;

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
                return;

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
                foreach (ItemData item in data.RestoreInventory(inventory))
                    inventory.ThrowAway(item);

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

            PlayerPassives passives = stats.GetComponent<PlayerPassives>();
            if (passives != null && data.passives != null)
            {
                passives.Restore(data.passives);
                passives.SetRespecCharges(data.respecCharges);
            }

            PlayerSkills skills = stats.GetComponent<PlayerSkills>();
            if (skills != null && data.skillSlots != null)
            {
                for (int k = 0; k < data.skillSlots.Count && k < SkillBook.SlotCount; k++)
                {
                    if (data.skillSlots[k] < 0)
                        skills.ClearSlot(k);
                    else
                        skills.Assign(k, (SkillId)data.skillSlots[k]);
                }
            }

            QuestLog log = QuestLog.Instance;
            // Quests saved under an older quest book start over (the places visited are kept).
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
                    data.visited ?? new List<int>(), questsCurrent && data.questProps != null ? data.questProps : new List<string>());
            }
            MinimapTerrain.Import(data.mapSeen);

            // Gear and passives raised the maximums after the level was restored: start full.
            stats.Heal(stats.MaxHealth);
            stats.RestoreMana(stats.MaxMana);

            CombatText.Show(stats.transform.position + Vector3.up * 2.4f,
                questsCurrent ? "Welcome back" : "Welcome back - the story begins anew: talk to Elder Maren",
                new Color(1f, 0.85f, 0.4f), 1f);
        }
    }
}
