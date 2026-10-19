using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>Browser entry to the actual sampled balance builds, with route-specific minimum levels.</summary>
    public sealed class BalanceArenaUI : MonoBehaviour
    {
        private GauntletMode mode;
        private RectTransform panel, canvasRect;
        private RectTransform newBuildButton, settingsButton;
        private readonly List<RectTransform> layoutItems = new List<RectTransform>();
        private readonly List<Vector2> layoutPositions = new List<Vector2>();
        private readonly List<Vector2> layoutSizes = new List<Vector2>();
        private Vector2 lastCanvasSize;
        private Text status, levelLabel, skillLabel, detail, pageLabel, levelGroupLabel;
        private Image randomLevelButton, randomBuildButton, lowerLevelButton;
        private readonly List<Image> routeButtons = new List<Image>();
        private readonly List<Text> routeLabels = new List<Text>();
        private string skill, route;
        private int level = 21, page;
        private bool randomLevel, randomBuild;
        private List<BalanceArenaBuilds.Entry> choices;
        private string[] skills;
        private List<BalanceArenaBuilds.Entry> visibleChoices;
        private const int PageSize = 6;
        private static readonly Color HighestDps = new Color(.45f, 1f, .65f);
        private static readonly Color Selected = new Color(1f, .62f, .3f);

        public void Bind(GauntletMode gauntlet)
        {
            mode = gauntlet;
            Canvas canvas = UiKit.NewCanvas("BalanceArena", transform, 760, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)canvas.transform;
            group.interactable = group.blocksRaycasts = true;
            status = UiKit.NewText("Status", canvas.transform, "", 19, UiKit.TextColor, TextAnchor.UpperCenter);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(.5f, 1f);
            status.rectTransform.anchoredPosition = new Vector2(0, -12);
            status.rectTransform.sizeDelta = new Vector2(1000, 65);
            UiKit.AddOutline(status, Color.black, 1.5f);
            newBuildButton = Button(canvas.transform, "New build", new Vector2(18, -16), new Vector2(150, 42),
                MinimalCombatMode.RestartArena).rectTransform;

            settingsButton = Button(canvas.transform, "Settings", new Vector2(180, -16), new Vector2(150, 42),
                () => FindAnyObjectByType<PoeClone.UI.EscapeMenuUI>()?.OpenSettings()).rectTransform;

            Image background = UiKit.NewImage("ArenaSetup", canvas.transform, UiKit.PanelColor);
            panel = background.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(1120, 860);
            background.raycastTarget = true;
            UiKit.Grain(background);
            UiKit.Frame(panel);
            Text title = Label(panel, "Balance Test Arena", new Vector2(30, -20), new Vector2(1060, 50), 36);
            UiKit.Heading(title);
            Label(panel, "Play the sampled builds from the boss balance survey", new Vector2(30, -78), new Vector2(1060, 32), 20);

            lowerLevelButton = Button(panel, "−", new Vector2(40, -130), new Vector2(65, 48), () => ChangeLevel(-1));
            levelLabel = Label(panel, "", new Vector2(115, -130), new Vector2(200, 48), 24);
            Button(panel, "+", new Vector2(325, -130), new Vector2(65, 48), () => ChangeLevel(1));
            randomLevelButton = Button(panel, "Random level", new Vector2(430, -130), new Vector2(280, 48), () => {
                randomLevel = !randomLevel; page = 0; Refresh();
            });
            randomBuildButton = Button(panel, "Random build", new Vector2(750, -130), new Vector2(330, 48), () => {
                randomBuild = !randomBuild; Refresh();
            });
            Button(panel, "Previous skill", new Vector2(40, -200), new Vector2(220, 48), () => ChangeSkill(-1));
            skillLabel = Label(panel, "", new Vector2(275, -200), new Vector2(570, 48), 24);
            Button(panel, "Next skill", new Vector2(860, -200), new Vector2(220, 48), () => ChangeSkill(1));
            levelGroupLabel = Label(panel, "", new Vector2(40, -256), new Vector2(1040, 28), 18);
            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                Image b = Button(panel, "", new Vector2(40, -290 - i * 50), new Vector2(1040, 46), () => {
                    route = visibleChoices[index].id; randomBuild = false; Refresh();
                });
                routeButtons.Add(b);
                Text label = b.GetComponentInChildren<Text>();
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 19;
                routeLabels.Add(label);
            }
            Button(panel, "Previous routes", new Vector2(40, -604), new Vector2(240, 42), () => { page--; Refresh(); });
            pageLabel = Label(panel, "", new Vector2(290, -604), new Vector2(540, 42), 18);
            Button(panel, "Next routes", new Vector2(840, -604), new Vector2(240, 42), () => { page++; Refresh(); });
            detail = Label(panel, "", new Vector2(40, -662), new Vector2(1040, 100), 18);
            Button(panel, "Enter arena", new Vector2(200, -780), new Vector2(330, 54), StartRun, 24);
            Button(panel, "Random level + build", new Vector2(590, -780), new Vector2(330, 54), () => {
                randomLevel = randomBuild = true; StartRun();
            }, 24);
            foreach (RectTransform child in panel)
            {
                // Frame and grain decorations use stretched anchors and need no manual layout.
                if (child.anchorMin != child.anchorMax) continue;
                layoutItems.Add(child);
                layoutPositions.Add(child.anchoredPosition);
                layoutSizes.Add(child.sizeDelta);
            }
            Refresh();
            Layout();
        }

        private void Layout()
        {
            Vector2 size = canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            lastCanvasSize = size;
            bool narrow = size.x < 1160f;
            float width = Mathf.Min(1120f, size.x - 32f);
            float height = narrow ? 1160f : 860f;
            // Fit short windows too, while retaining the portrait arrangement on phones.
            float scale = Mathf.Min(1f, (size.y - 32f) / height);
            panel.sizeDelta = new Vector2(width / scale, height);
            panel.localScale = Vector3.one * scale;
            float w = panel.sizeDelta.x;
            for (int i = 0; i < layoutItems.Count; i++)
            {
                Vector2 at = layoutPositions[i], dimensions = layoutSizes[i];
                if (narrow)
                {
                    float y = -at.y;
                    if (y == 20f || y == 78f) { at.x = 24f; dimensions.x = w - 48f; }
                    else if (y == 130f)
                    {
                        if (at.x == 40f) { at.x = 24f; dimensions.x = 60f; }
                        else if (at.x == 115f) { at.x = 94f; dimensions.x = w - 188f; }
                        else if (at.x == 325f) { at.x = w - 84f; dimensions.x = 60f; }
                        else { at = new Vector2(at.x == 430f ? 24f : w / 2f + 6f, -194f); dimensions.x = w / 2f - 30f; }
                    }
                    else if (y == 200f)
                    {
                        at.y = -264f;
                        if (at.x == 40f || at.x == 860f)
                        {
                            at.x = at.x == 40f ? 24f : w - 84f; dimensions.x = 60f;
                            layoutItems[i].GetComponentInChildren<Text>().text = layoutPositions[i].x == 40f ? "‹" : "›";
                        }
                        else { at.x = 94f; dimensions.x = w - 188f; }
                    }
                    else if (y >= 256f && y <= 540f) { at = new Vector2(24f, at.y - 80f); dimensions.x = w - 48f; }
                    else if (y == 604f)
                    {
                        at.y = -684f;
                        if (at.x == 290f) { at.x = 94f; dimensions.x = w - 188f; }
                        else
                        {
                            at.x = at.x == 40f ? 24f : w - 84f; dimensions.x = 60f;
                            layoutItems[i].GetComponentInChildren<Text>().text = layoutPositions[i].x == 40f ? "‹" : "›";
                        }
                    }
                    else if (y == 662f) { at = new Vector2(24f, -746f); dimensions = new Vector2(w - 48f, 240f); }
                    else if (y == 780f) { at = new Vector2(24f, at.x == 200f ? -1010f : -1080f); dimensions.x = w - 48f; }
                }
                else if (-at.y == 200f || -at.y == 604f)
                {
                    string value = at.x == 40f ? (-at.y == 200f ? "Previous skill" : "Previous routes") :
                        at.x == 860f ? "Next skill" : at.x == 840f ? "Next routes" : null;
                    if (value != null) layoutItems[i].GetComponentInChildren<Text>().text = value;
                }
                UiKit.TopLeft(layoutItems[i], at, dimensions);
            }
            bool portrait = size.x < size.y;
            UiKit.TopLeft(newBuildButton, new Vector2(16f, -12f), new Vector2(150f, 42f));
            UiKit.TopLeft(settingsButton, new Vector2(portrait ? size.x - 166f : 180f, -12f), new Vector2(150f, 42f));
            status.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -64f : -12f);
            status.rectTransform.sizeDelta = new Vector2(portrait ? size.x - 32f : Mathf.Max(200f, size.x - 700f), portrait ? 110f : 65f);
        }

        private void ChangeLevel(int delta)
        {
            level = Mathf.Clamp(level + delta, MinimumLevel, 34);
            randomLevel = false; page = 0; Refresh();
        }
        private int MinimumLevel => randomBuild ? 1 : choices.First(b => b.id == route).level;
        private IEnumerable<BalanceArenaBuilds.Entry> Available => BalanceArenaBuilds.Data.builds.Where(b => randomLevel || b.level == level);
        private void ChangeSkill(int delta)
        {
            int current = System.Array.IndexOf(skills, skill);
            skill = skills[(current + delta + skills.Length) % skills.Length];
            route = null; randomBuild = false; page = 0; Refresh();
        }
        private void Refresh()
        {
            skills = BalanceArenaBuilds.Data.builds.Select(b => b.skill).Distinct().ToArray();
            if (!skills.Contains(skill)) skill = skills[0];
            choices = BalanceArenaBuilds.Data.builds.Where(b => b.skill == skill)
                .GroupBy(b => b.id).Select(g => g.OrderBy(b => b.level).First()).ToList();
            if (!choices.Any(b => b.id == route)) route = choices[0].id;
            level = Mathf.Max(level, MinimumLevel);
            lowerLevelButton.color = !randomLevel && level <= MinimumLevel ? new Color(.45f, .45f, .45f) : Color.white;
            // Keep each minimum-level group on its own pages so its heading and DPS leader
            // remain unambiguous, even when the group spans several pages.
            var routePages = new List<List<BalanceArenaBuilds.Entry>>();
            var leaders = new Dictionary<int, float>();
            foreach (var group in choices.GroupBy(b => b.level).OrderBy(g => g.Key))
            {
                var ranked = group.OrderByDescending(b => BalanceArenaBuilds.EstimatedDps(Preview(b)))
                    .ThenBy(b => b.name, System.StringComparer.Ordinal).ThenBy(b => b.id, System.StringComparer.Ordinal).ToList();
                leaders[group.Key] = BalanceArenaBuilds.EstimatedDps(Preview(ranked[0]));
                for (int offset = 0; offset < ranked.Count; offset += PageSize)
                    routePages.Add(ranked.Skip(offset).Take(PageSize).ToList());
            }
            int pages = routePages.Count;
            page = Mathf.Clamp(page, 0, pages - 1);
            visibleChoices = routePages[page];
            levelGroupLabel.text = "Available from Lv " + visibleChoices[0].level + " · Highest DPS first";
            levelLabel.text = randomLevel ? "Level " + MinimumLevel + "–34" : "Level " + level;
            skillLabel.text = skill + "  (" + (System.Array.IndexOf(skills, skill) + 1) + "/" + skills.Length + ")";
            randomLevelButton.color = randomLevel ? Selected : Color.white;
            randomBuildButton.color = randomBuild ? Selected : Color.white;
            for (int i = 0; i < PageSize; i++)
            {
                routeButtons[i].gameObject.SetActive(i < visibleChoices.Count);
                if (i >= visibleChoices.Count) continue;
                var choice = visibleChoices[i];
                var preview = Preview(choice);
                float dps = BalanceArenaBuilds.EstimatedDps(preview);
                bool highest = dps == leaders[choice.level];
                routeLabels[i].text = choice.name + "  · Lv " + choice.level + "+" +
                    "\n≈ " + dps.ToString("N0") + " DPS at Lv " + preview.level + (highest ? " · TOP DPS" : "");
                routeLabels[i].color = highest ? HighestDps : UiKit.TextColor;
                routeButtons[i].color = !randomBuild && choice.id == route ? Selected : Color.white;
            }
            pageLabel.text = "Routes " + (page + 1) + " / " + pages;
            detail.text = (randomBuild ? "Random build from all available skills and routes." : "Selected: " + skill + " · " + choices.First(b => b.id == route).name) +
                (randomBuild ? "\nRandom builds use routes available at the chosen level." : "\nMinimum level " + MinimumLevel + ". Change build to lower the level further.") +
                "\nWorst 30% ground-loot gear. DPS: single target, full army; excludes movement/mana downtime." +
                "\n20 waves of " + GauntletMode.ArenaMinMonsters + "–" + GauntletMode.ArenaMaxMonsters +
                " enemies and occasional bosses. No XP, loot or saves.";
        }
        private BalanceArenaBuilds.Entry Preview(BalanceArenaBuilds.Entry choice)
        {
            int previewLevel = randomLevel ? choice.level : Mathf.Max(level, choice.level);
            return BalanceArenaBuilds.Data.builds.First(b => b.id == choice.id && b.level == previewLevel);
        }
        private void StartRun()
        {
            if (mode.Started) return;
            var pool = Available.Where(b => randomBuild || b.id == route).ToArray();
            // Choose a level first so random-level runs do not favour high levels with more routes.
            int[] levels = pool.Select(b => b.level).Distinct().ToArray();
            int pickedLevel = levels[Random.Range(0, levels.Length)];
            pool = pool.Where(b => b.level == pickedLevel).ToArray();
            mode.BeginArena(pool[Random.Range(0, pool.Length)]);
            panel.gameObject.SetActive(false);
        }
        private void Update()
        {
            if (canvasRect != null && canvasRect.rect.size != lastCanvasSize) Layout();
            if (mode == null || status == null || !mode.Started) return;
            status.text = mode.Build.Name + " · Level " + mode.Level + "\n" + mode.Status + " · Deaths " + mode.Deaths;
            if (mode.Finished) status.text += " · Use New build to play again";
        }
        private static Text Label(Transform parent, string value, Vector2 at, Vector2 size, int fontSize)
        {
            Text text = UiKit.NewText("Label", parent, value, fontSize, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.TopLeft(text.rectTransform, at, size);
            return text;
        }
        private static Image Button(Transform parent, string value, Vector2 at, Vector2 size, System.Action click, int fontSize = 19)
        {
            Image image = UiKit.NewImage("Button", parent, Color.white);
            UiKit.TopLeft(image.rectTransform, at, size);
            Text text = UiKit.NewText("Label", image.transform, value, fontSize, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, 4);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            UiKit.StyleButton(image, text);
            return image;
        }
    }
}
