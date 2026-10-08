using System;
using System.Collections.Generic;
using PoeClone.Inventory;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    public sealed class ItemModifierEditor : EditorWindow
    {
        // HideAndDontSave includes NotEditable, which disables SerializedProperty controls.
        private const HideFlags EditableDraftFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        [SerializeField] private ItemModifierCatalog draft;
        [SerializeField] private string savedJson;
        [SerializeField] private int selected;
        private SerializedObject serialized;
        private string search = "";
        private Vector2 listScroll, detailScroll;

        [MenuItem("PoeClone/Item Modifier Editor", priority = 32)]
        public static void Open()
        {
            var window = GetWindow<ItemModifierEditor>("Item Modifiers");
            window.minSize = new Vector2(900, 550);
            window.Show();
        }

        public static ItemModifierCatalog EnsureAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ItemModifierCatalog>(ItemModifierCatalog.AssetPath);
            if (asset == null) throw new InvalidOperationException("Missing balance asset: " + ItemModifierCatalog.AssetPath);
            return asset;
        }

        private void OnEnable()
        {
            saveChangesMessage = "Save item modifier balance changes?";
            Undo.undoRedoPerformed += OnUndo;
            if (draft == null) Load();
            else
            {
                // Repair existing drafts after reload without discarding the user's edits.
                draft.hideFlags = EditableDraftFlags;
                serialized = new SerializedObject(draft);
            }
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
        private void OnDestroy() { if (draft != null) DestroyImmediate(draft); }
        private void OnUndo() { UpdateDirty(); Repaint(); }
        private void UpdateDirty() => hasUnsavedChanges = draft != null && JsonUtility.ToJson(draft) != savedJson;

        private void Load()
        {
            if (draft != null) DestroyImmediate(draft);
            draft = Instantiate(EnsureAsset());
            draft.hideFlags = EditableDraftFlags;
            serialized = new SerializedObject(draft);
            savedJson = JsonUtility.ToJson(draft);
            UpdateDirty();
        }

        public override void SaveChanges()
        {
            serialized.ApplyModifiedProperties();
            var errors = draft.Validate();
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Invalid modifier balance", string.Join("\n", errors), "OK");
                return;
            }
            var asset = EnsureAsset();
            if (JsonUtility.ToJson(asset) != savedJson && !EditorUtility.DisplayDialog("Balance asset changed",
                "The balance asset changed since this draft was loaded. Overwrite it with this draft?", "Overwrite", "Cancel")) return;
            Undo.RecordObject(asset, "Save item modifier balance");
            EditorUtility.CopySerialized(draft, asset);
            asset.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            savedJson = JsonUtility.ToJson(draft);
            base.SaveChanges();
            UpdateDirty();
        }

        public override void DiscardChanges() { Load(); base.DiscardChanges(); }

        private void OnGUI()
        {
            // Unity's editor styles are shared; restore them after drawing this window.
            using (new LargerFontScope()) DrawWindow();
        }

        private sealed class LargerFontScope : IDisposable
        {
            private readonly List<(GUIStyle style, int fontSize, float fixedHeight)> originals =
                new List<(GUIStyle, int, float)>();

            public LargerFontScope()
            {
                var styles = new[] { GUI.skin.label, GUI.skin.button, GUI.skin.toggle, GUI.skin.textField,
                    EditorStyles.label, EditorStyles.boldLabel, EditorStyles.miniLabel, EditorStyles.helpBox,
                    EditorStyles.textField, EditorStyles.numberField, EditorStyles.popup, EditorStyles.toggle,
                    EditorStyles.toolbar, EditorStyles.toolbarButton };
                var seen = new HashSet<GUIStyle>();
                foreach (var style in styles)
                {
                    if (!seen.Add(style)) continue;
                    originals.Add((style, style.fontSize, style.fixedHeight));
                    style.fontSize = (style.fontSize > 0 ? style.fontSize : 12) + 2;
                    if (style.fixedHeight > 0) style.fixedHeight += 4;
                }
            }

            public void Dispose()
            {
                foreach (var original in originals)
                {
                    original.style.fontSize = original.fontSize;
                    original.style.fixedHeight = original.fixedHeight;
                }
            }
        }

        private void DrawWindow()
        {
            if (draft == null || serialized == null) return;
            serialized.Update();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Item Modifier Balance", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Save Balance", EditorStyles.toolbarButton)) SaveChanges();
                }
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton) && (!hasUnsavedChanges ||
                    EditorUtility.DisplayDialog("Reload balance", "Discard unsaved edits and reload the saved balance?", "Reload", "Cancel"))) Load();
                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton)) Selection.activeObject = EnsureAsset();
            }
            EditorGUILayout.HelpBox("Edits are a draft until Save Balance. Tiers are listed weakest to strongest; T1 is the last row. Roll bounds are inclusive integers. Minimum item level is a drop gate, separate from equip requirements.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    var mods = serialized.FindProperty("Modifiers");
                    if (selected == -1)
                    {
                        GUILayout.Label("Built-in attack skill levels", EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("Shared by the guaranteed staff/grimoire attack skill and other callers of RollSkillLevel. Optional skill affixes each have their own tiers.", MessageType.Info);
                        DrawTiers(serialized.FindProperty("MainSkillTiers"));
                    }
                    else if (selected >= 0 && selected < mods.arraySize) DrawModifier(mods.GetArrayElementAtIndex(selected));
                    EditorGUILayout.EndScrollView();
                }
            }
            serialized.ApplyModifiedProperties();
            UpdateDirty();
            var errors = draft.Validate();
            if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors.GetRange(0, Math.Min(4, errors.Count))), MessageType.Error);
            GUILayout.Label(hasUnsavedChanges ? "Unsaved balance changes" : ItemModifierCatalog.AssetPath, EditorStyles.miniLabel);
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(310)))
            {
                search = EditorGUILayout.TextField("Search", search, GUILayout.Height(22));
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                if (GUILayout.Toggle(selected == -1, "Built-in attack skill levels", "Button")) selected = -1;
                var mods = serialized.FindProperty("Modifiers");
                for (int i = 0; i < mods.arraySize; i++)
                {
                    var mod = mods.GetArrayElementAtIndex(i);
                    string label = mod.FindPropertyRelative("Label").stringValue;
                    var stat = mod.FindPropertyRelative("Stat");
                    string statName = stat.enumDisplayNames[stat.enumValueIndex];
                    if ((label + " " + statName).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (GUILayout.Toggle(selected == i, label + (mod.FindPropertyRelative("Enabled").boolValue ? "" : " (disabled)"), "Button")) selected = i;
                }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Add Modifier"))
                {
                    int index = mods.arraySize++;
                    var mod = mods.GetArrayElementAtIndex(index);
                    mod.FindPropertyRelative("Label").stringValue = "New Modifier";
                    mod.FindPropertyRelative("Stat").enumValueIndex = 0;
                    mod.FindPropertyRelative("Enabled").boolValue = true;
                    mod.FindPropertyRelative("Weight").floatValue = 1;
                    mod.FindPropertyRelative("AnyWeapon").boolValue = true;
                    mod.FindPropertyRelative("On").arraySize = 0;
                    mod.FindPropertyRelative("Weapons").arraySize = 0;
                    var tiers = mod.FindPropertyRelative("Tiers");
                    tiers.arraySize = 0;
                    AddTier(tiers);
                    selected = index;
                }
            }
        }

        private void DrawModifier(SerializedProperty mod)
        {
            EditorGUILayout.PropertyField(mod.FindPropertyRelative("Label"), GUILayout.Height(22));
            EditorGUILayout.PropertyField(mod.FindPropertyRelative("Stat"), GUILayout.Height(22));
            EditorGUILayout.PropertyField(mod.FindPropertyRelative("Enabled"), GUILayout.Height(22));
            EditorGUILayout.PropertyField(mod.FindPropertyRelative("Weight"), new GUIContent("Modifier weight", "Relative chance against other eligible modifiers, multiplied by the item's existing attribute leaning."), GUILayout.Height(22));
            GUILayout.Space(8);
            GUILayout.Label("Allowed item types", EditorStyles.boldLabel);
            DrawTypes<ItemType>(mod.FindPropertyRelative("On"));
            EditorGUILayout.PropertyField(mod.FindPropertyRelative("AnyWeapon"), new GUIContent("All weapon types"), GUILayout.Height(22));
            if (!mod.FindPropertyRelative("AnyWeapon").boolValue) DrawTypes<WeaponType>(mod.FindPropertyRelative("Weapons"));
            EditorGUILayout.HelpBox("Weapon restrictions apply only to Weapon items. No checked item types or a zero modifier weight prevents this modifier from rolling.", MessageType.None);
            DrawTiers(mod.FindPropertyRelative("Tiers"));
            if (GUILayout.Button("Duplicate Modifier"))
            {
                var mods = serialized.FindProperty("Modifiers");
                mods.InsertArrayElementAtIndex(selected);
                selected++;
                mods.GetArrayElementAtIndex(selected).FindPropertyRelative("Label").stringValue += " copy";
            }
            if (GUILayout.Button("Delete Modifier") && EditorUtility.DisplayDialog("Delete modifier", "Remove this modifier from the draft?", "Delete", "Cancel"))
            {
                serialized.FindProperty("Modifiers").DeleteArrayElementAtIndex(selected);
                selected = 0;
            }
        }

        private static void DrawTypes<T>(SerializedProperty array) where T : Enum
        {
            var values = Enum.GetValues(typeof(T));
            for (int row = 0; row < values.Length; row += 3)
            using (new EditorGUILayout.HorizontalScope())
            for (int column = row; column < Math.Min(row + 3, values.Length); column++)
            {
                int value = Convert.ToInt32(values.GetValue(column));
                int found = -1;
                for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).intValue == value) found = i;
                bool on = GUILayout.Toggle(found >= 0, ObjectNames.NicifyVariableName(values.GetValue(column).ToString()), GUILayout.MinWidth(115));
                if (on && found < 0) { int i = array.arraySize++; array.GetArrayElementAtIndex(i).intValue = value; }
                else if (!on && found >= 0) array.DeleteArrayElementAtIndex(found);
            }
        }

        private static void DrawTiers(SerializedProperty tiers)
        {
            GUILayout.Space(10);
            GUILayout.Label("Tier ranges and eligibility", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Tier", GUILayout.Width(38));
                GUILayout.Label("Min item lvl", GUILayout.MinWidth(75));
                GUILayout.Label("Minimum roll", GUILayout.MinWidth(75));
                GUILayout.Label("Maximum roll", GUILayout.MinWidth(75));
                GUILayout.Label("Tier weight", GUILayout.MinWidth(75));
                GUILayout.Space(78);
            }
            for (int i = 0; i < tiers.arraySize; i++)
            {
                var tier = tiers.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("T" + (tiers.arraySize - i), GUILayout.Width(38));
                    EditorGUILayout.PropertyField(tier.FindPropertyRelative("RequiredItemLevel"), GUIContent.none, GUILayout.MinWidth(75), GUILayout.Height(22));
                    EditorGUILayout.PropertyField(tier.FindPropertyRelative("Min"), GUIContent.none, GUILayout.MinWidth(75), GUILayout.Height(22));
                    EditorGUILayout.PropertyField(tier.FindPropertyRelative("Max"), GUIContent.none, GUILayout.MinWidth(75), GUILayout.Height(22));
                    EditorGUILayout.PropertyField(tier.FindPropertyRelative("Weight"), GUIContent.none, GUILayout.MinWidth(75), GUILayout.Height(22));
                    if (GUILayout.Button("↑", GUILayout.Width(23)) && i > 0) tiers.MoveArrayElement(i, i - 1);
                    if (GUILayout.Button("↓", GUILayout.Width(23)) && i + 1 < tiers.arraySize) tiers.MoveArrayElement(i, i + 1);
                    if (GUILayout.Button("×", GUILayout.Width(23))) { tiers.DeleteArrayElementAtIndex(i); break; }
                }
            }
            if (GUILayout.Button("Add Tier")) AddTier(tiers);
            EditorGUILayout.HelpBox("Only tiers whose minimum item level is met can roll. Weights are relative among eligible tiers; zero disables a tier. If none qualify, the modifier is excluded from the roll.", MessageType.None);
        }

        private static void AddTier(SerializedProperty tiers)
        {
            int index = tiers.arraySize++;
            var tier = tiers.GetArrayElementAtIndex(index);
            if (index == 0)
            {
                tier.FindPropertyRelative("RequiredItemLevel").intValue = 1;
                tier.FindPropertyRelative("Min").intValue = 1;
                tier.FindPropertyRelative("Max").intValue = 1;
                tier.FindPropertyRelative("Weight").floatValue = 1;
            }
        }
    }
}
