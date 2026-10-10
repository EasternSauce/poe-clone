using System.Collections.Generic;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.World;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Debug list of every unique: level, drop weight and chance, where it comes from, and its rolls.</summary>
    public sealed class UniqueItemBrowser : EditorWindow
    {
        private enum SortMode { LevelThenChance, Chance, Name }

        private const int MaxAreaLevel = 40;
        private static readonly Color Orange = new Color(0.9f, 0.55f, 0.2f);

        [SerializeField] private int areaLevel = MaxAreaLevel;
        [SerializeField] private SortMode sort = SortMode.LevelThenChance;
        [SerializeField] private string search = "";
        private readonly HashSet<int> expanded = new HashSet<int>();
        private Vector2 scroll;
        private GUIStyle nameStyle;

        [MenuItem("PoeClone/Unique Item Browser", priority = 33)]
        public static void Open()
        {
            var window = GetWindow<UniqueItemBrowser>("Unique Items");
            window.minSize = new Vector2(820, 400);
            window.Show();
        }

        private static int LevelOf(int k) => UniqueItems.RequiredLevelFor(UniqueItems.NameOf(k));

        /// <summary>Chance of being the unique picked when one drops: weighted share, or an even split of the Shepherd's.</summary>
        private static float ChanceOf(int k, int level) => UniqueItems.IsShepherdOnly(k)
            ? 1f / UniqueItems.ShepherdRewardCount
            : UniqueItems.ShareAtAreaLevel(k, level);

        private static string Sources(int k)
        {
            if (UniqueItems.IsShepherdOnly(k))
                return "Only The Shepherd (act boss), one of his " + UniqueItems.ShepherdRewardCount + " rewards";
            string text = "Monsters + bosses, area lvl " + UniqueItems.RequiredLevelFor(UniqueItems.NameOf(k)) + "+";
            if (LevelOf(k) <= NpcDialogues.SmithMaxLevel)
                text += "; Smith commission";
            return text;
        }

        private List<int> Rows()
        {
            var rows = new List<int>();
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                if (!string.IsNullOrEmpty(search)
                    && UniqueItems.NameOf(k).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && UniqueItems.BaseIdOf(k).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                rows.Add(k);
            }
            rows.Sort((a, b) =>
            {
                int c = 0;
                if (sort == SortMode.LevelThenChance) c = LevelOf(a).CompareTo(LevelOf(b));
                if (c == 0 && sort != SortMode.Name)
                {
                    // Uniques that can't drop at the chosen area level fall back to their own level's chance.
                    c = ChanceOf(b, Mathf.Max(areaLevel, LevelOf(b))).CompareTo(ChanceOf(a, Mathf.Max(areaLevel, LevelOf(a))));
                    if (c == 0) c = UniqueItems.DropWeight(b).CompareTo(UniqueItems.DropWeight(a));
                }
                return c != 0 ? c : string.CompareOrdinal(UniqueItems.NameOf(a), UniqueItems.NameOf(b));
            });
            return rows;
        }

        private void OnGUI()
        {
            nameStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Orange } };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                sort = (SortMode)EditorGUILayout.EnumPopup(sort, EditorStyles.toolbarPopup, GUILayout.Width(130));
                GUILayout.Label("Area level", GUILayout.Width(62));
                areaLevel = EditorGUILayout.IntSlider(areaLevel, 1, MaxAreaLevel, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }

            EditorGUILayout.HelpBox(
                "Any monster gear drop is a unique " + (LootDrop.UniqueChance * 100).ToString("0.#") + "% of the time (x toughness, up to 4x). " +
                "Every boss also drops one guaranteed unique (The Shepherd drops only his own). " +
                "Smith commissions are unique " + (NpcDialogues.SmithUniqueChance * 100).ToString("0") + "% of the time, using character level capped at " + NpcDialogues.SmithMaxLevel + ".\n" +
                "'@ area' is the chance this is the unique picked when an ordinary unique drops at the chosen area level; " +
                "'@ own' is that chance at the unique's own required level.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Lvl", GUILayout.Width(40));
                GUILayout.Label("Name", GUILayout.Width(230));
                GUILayout.Label("Base", GUILayout.Width(140));
                GUILayout.Label("Weight", GUILayout.Width(50));
                GUILayout.Label("@ area " + areaLevel, GUILayout.Width(70));
                GUILayout.Label("@ own", GUILayout.Width(55));
                GUILayout.Label("Sources");
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (int k in Rows())
                DrawRow(k);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(int k)
        {
            bool shepherd = UniqueItems.IsShepherdOnly(k);
            int level = LevelOf(k);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = expanded.Contains(k);
                Rect fold = GUILayoutUtility.GetRect(40, EditorGUIUtility.singleLineHeight, GUILayout.Width(40));
                bool now = EditorGUI.Foldout(fold, open, level.ToString(), true);
                if (now != open) { if (now) expanded.Add(k); else expanded.Remove(k); }
                GUILayout.Label(UniqueItems.NameOf(k), nameStyle, GUILayout.Width(230));
                GUILayout.Label(ItemGenerator.BaseNameOf(UniqueItems.BaseIdOf(k)) ?? UniqueItems.BaseIdOf(k), GUILayout.Width(140));
                GUILayout.Label(shepherd ? "-" : UniqueItems.DropWeight(k).ToString(), GUILayout.Width(50));
                GUILayout.Label(Percent(ChanceOf(k, areaLevel)), GUILayout.Width(70));
                GUILayout.Label(Percent(ChanceOf(k, level)), GUILayout.Width(55));
                GUILayout.Label(Sources(k), shepherd ? EditorStyles.boldLabel : EditorStyles.label);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("Drop", "Drop a fresh roll at the player (Play mode)"), GUILayout.Width(45)))
                        DropAtPlayer(k);
                }
            }

            if (!expanded.Contains(k)) return;
            using (new EditorGUI.IndentLevelScope(2))
            {
                foreach (string line in UniqueItems.ModifierRanges(k))
                    EditorGUILayout.LabelField(line);
                string flavour = UniqueItems.FlavourFor(UniqueItems.Current(UniqueItems.NameOf(k)));
                if (!string.IsNullOrEmpty(flavour))
                    EditorGUILayout.LabelField(flavour, EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);
        }

        private static string Percent(float chance) => chance <= 0f ? "-" : (chance * 100f).ToString("0.0") + "%";

        private static void DropAtPlayer(int k)
        {
            var player = Object.FindAnyObjectByType<PlayerStats>();
            if (player == null)
            {
                Debug.LogWarning("Unique Item Browser: no player in the scene.");
                return;
            }
            LootDrop.Drop(UniqueItems.Create(k), player.transform.position, itemLevel: LevelOf(k));
        }
    }
}
