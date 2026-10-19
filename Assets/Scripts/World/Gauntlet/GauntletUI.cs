using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>The gauntlet's screens: level and build choice, a progress line while fighting, and the results.</summary>
    public sealed class GauntletUI : MonoBehaviour
    {
        private GauntletMode mode;
        private Canvas canvas;
        private RectTransform setupPanel, resultsPanel;
        private Text status, summary, results;
        private readonly List<KeyValuePair<int, Image>> levelButtons = new List<KeyValuePair<int, Image>>();
        private readonly List<KeyValuePair<GauntletBuild, Image>> buildButtons = new List<KeyValuePair<GauntletBuild, Image>>();
        private int level = GauntletBuilds.Levels[1];
        private GauntletBuild build = GauntletBuilds.All[0];

        private static readonly Color Picked = new Color(1f, 0.62f, 0.3f, 1f);

        public void Bind(GauntletMode gauntlet)
        {
            mode = gauntlet;
            canvas = UiKit.NewCanvas("GauntletCanvas", transform, 760, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            status = UiKit.NewText("Status", canvas.transform, "", 22, UiKit.TextColor, TextAnchor.UpperCenter);
            status.font = UiKit.BoldFont;
            RectTransform s = status.rectTransform;
            s.anchorMin = s.anchorMax = s.pivot = new Vector2(0.5f, 1f);
            s.anchoredPosition = new Vector2(0f, -14f);
            s.sizeDelta = new Vector2(1200f, 30f);
            UiKit.AddOutline(status, Color.black, 1.5f);

            BuildSetup();
            Refresh();
        }

        private RectTransform Panel(string name, Vector2 size)
        {
            Image bg = UiKit.NewImage(name, canvas.transform, UiKit.PanelColor);
            UiKit.Grain(bg);
            bg.raycastTarget = true;
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            UiKit.Frame(rt);
            return rt;
        }

        private Image Button(RectTransform parent, string label, Vector2 pos, Vector2 size, System.Action onClick, int fontSize = 18)
        {
            Image plate = UiKit.NewImage("Button", parent, Color.white);
            UiKit.TopLeft(plate.rectTransform, pos, size);
            Text text = UiKit.NewText("Label", plate.rectTransform, label, fontSize, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, 4f);
            plate.raycastTarget = true;
            var button = plate.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => onClick());
            UiKit.StyleButton(plate, text);
            return plate;
        }

        private static Text Label(RectTransform parent, string content, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
        {
            Text t = UiKit.NewText("Text", parent, content, fontSize, UiKit.TextColor, anchor);
            UiKit.TopLeft(t.rectTransform, pos, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        private void BuildSetup()
        {
            setupPanel = Panel("GauntletSetup", new Vector2(1120f, 800f));
            Text title = Label(setupPanel, "Class Gauntlet", new Vector2(0f, -22f), new Vector2(1120f, 50f), 38, TextAnchor.MiddleCenter);
            UiKit.Heading(title);
            Label(setupPanel, "Level", new Vector2(40f, -90f), new Vector2(200f, 30f), 22, TextAnchor.MiddleLeft);
            for (int k = 0; k < GauntletBuilds.Levels.Length; k++)
            {
                int lvl = GauntletBuilds.Levels[k];
                Image b = Button(setupPanel, "Level " + lvl, new Vector2(40f + k * 172f, -124f), new Vector2(160f, 46f), () => { level = lvl; Refresh(); });
                levelButtons.Add(new KeyValuePair<int, Image>(lvl, b));
            }

            Label(setupPanel, "Build", new Vector2(40f, -186f), new Vector2(200f, 30f), 22, TextAnchor.MiddleLeft);
            for (int k = 0; k < GauntletBuilds.All.Length; k++)
            {
                GauntletBuild b = GauntletBuilds.All[k];
                Image plate = Button(setupPanel, b.Name, new Vector2(40f + (k % 3) * 348f, -220f - (k / 3) * 56f), new Vector2(336f, 48f),
                    () => { build = b; Refresh(); }, 17);
                buildButtons.Add(new KeyValuePair<GauntletBuild, Image>(b, plate));
            }

            summary = Label(setupPanel, "", new Vector2(40f, -510f), new Vector2(1040f, 170f), 19, TextAnchor.UpperLeft);
            Button(setupPanel, "Start", new Vector2(440f, -712f), new Vector2(240f, 58f), StartRun, 24);
        }

        private void Refresh()
        {
            foreach (var pair in levelButtons) pair.Value.color = pair.Key == level ? Picked : Color.white;
            foreach (var pair in buildButtons) pair.Value.color = pair.Key == build ? Picked : Color.white;
            string uniques = build.Uniques.Length > 0 ? string.Join(", ", build.Uniques).Replace("@", " from level ").Replace("!", "*") : "none";
            summary.text = "<b>" + build.Name + "</b>   " + (build.Melee ? "melee" : "ranged") + "\n" + build.Summary +
                "\n\n<color=#" + UiKit.Hex(UiKit.DimText) + ">Uniques (at most two once the level allows; * always worn, the rest only if better than the rolled rare): " + uniques + "\n" +
                (level <= 1
                    ? "Level 1: the usual starter gear lies on the floor, no rares, and the reroll buttons are off."
                    : "Level " + level + ": the best of " + mode.Rolls + " rares in every slot, " + (level - 1) + " passives, 20 health and 20 mana potions, 100 Orbs of Regret. " +
                      "Each slot's R button in the inventory swaps in a random rare.") +
                "\nNo experience, loot or gold. Results go to GauntletResults/ in the project folder.</color>";
        }

        private void StartRun()
        {
            if (mode.Started) return;
            mode.Begin(build, level);
            setupPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (mode == null || status == null) return;
            if (!mode.Started)
            {
                status.text = "";
                return;
            }
            int seconds = Mathf.FloorToInt(mode.Elapsed);
            status.text = mode.Build.Name + "  L" + mode.Level + "   ·   " + mode.Status + "   ·   deaths " + mode.Deaths +
                "   ·   " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        public void ShowResults(string text, string path)
        {
            if (resultsPanel == null)
            {
                resultsPanel = Panel("GauntletResults", new Vector2(1180f, 900f));
                results = Label(resultsPanel, "", new Vector2(30f, -24f), new Vector2(1120f, 800f), 15, TextAnchor.UpperLeft);
                results.verticalOverflow = VerticalWrapMode.Truncate;
                results.supportRichText = false;
                Button(resultsPanel, "Close", new Vector2(470f, -834f), new Vector2(240f, 50f), () => resultsPanel.gameObject.SetActive(false), 22);
            }
            // The top of the summary: speeds, mana, skills and gear. The file has everything.
            string[] lines = text.Split('\n');
            int shown = Mathf.Min(lines.Length, 52);
            results.text = string.Join("\n", lines, 0, shown) + (shown < lines.Length ? "\n..." : "") + "\n\nSaved to " + path;
            resultsPanel.gameObject.SetActive(true);
        }
    }
}
