using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>Browser for distinct live builds at shared level milestones.</summary>
    public sealed class BalanceArenaUI : MonoBehaviour
    {
        private GauntletMode mode;
        private RectTransform panel, canvasRect;
        private RectTransform newBuildButton, settingsButton;
        private readonly List<RectTransform> layoutItems = new List<RectTransform>();
        private readonly List<Vector2> layoutPositions = new List<Vector2>();
        private readonly List<Vector2> layoutSizes = new List<Vector2>();
        private Vector2 lastCanvasSize;
        private float lastHudBottom;
        private float timeScaleBeforePicker;
        private bool audioPausedBeforePicker;
        private bool pickerPaused;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        private Text status, levelLabel, skillLabel, detail, routeCountLabel, levelGroupLabel;
        private ScrollRect routeScroll;
        private Image randomLevelButton, randomBuildButton, lowerLevelButton;
        private readonly List<Image> routeButtons = new List<Image>();
        private readonly List<Image> routeHighlights = new List<Image>();
        private readonly List<Text> routeLabels = new List<Text>();
        private readonly List<Text> categoryHeaders = new List<Text>();
        private string skill, route;
        private int level = 1;
        private bool randomLevel, randomBuild;
        private List<BalanceArenaBuilds.Entry> choices;
        private string[] skills;
        private List<BalanceArenaBuilds.Entry> visibleChoices;
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
            newBuildButton = Button(canvas.transform, "Arena builds", Vector2.zero, new Vector2(150, 42),
                TogglePicker).rectTransform;

            settingsButton = Button(canvas.transform, "Settings", new Vector2(180, -16), new Vector2(150, 42),
                () => {
                    ClosePicker();
                    FindAnyObjectByType<EscapeMenuUI>()?.OpenSettings();
                }).rectTransform;

            Image background = UiKit.NewImage("ArenaSetup", canvas.transform, UiKit.PanelColor);
            panel = background.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(1120, 860);
            background.raycastTarget = true;
            UiKit.Grain(background);
            UiKit.Frame(panel);
            Text title = Label(panel, "Balance Test Arena", new Vector2(30, -20), new Vector2(1060, 50), 36);
            UiKit.Heading(title);
            Label(panel, "Click a build to start the arena immediately", new Vector2(30, -78), new Vector2(1060, 32), 20);

            lowerLevelButton = Button(panel, "−", new Vector2(40, -130), new Vector2(65, 48), () => ChangeLevel(-1));
            levelLabel = Label(panel, "", new Vector2(115, -130), new Vector2(200, 48), 24);
            Button(panel, "+", new Vector2(325, -130), new Vector2(65, 48), () => ChangeLevel(1));
            randomLevelButton = Button(panel, "Random level", new Vector2(430, -130), new Vector2(280, 48), () => {
                randomLevel = !randomLevel; Refresh();
            });
            randomBuildButton = Button(panel, "Random build", new Vector2(750, -130), new Vector2(330, 48), () => {
                randomBuild = !randomBuild; Refresh();
            });
            Button(panel, "Previous skill", new Vector2(40, -200), new Vector2(220, 48), () => ChangeSkill(-1));
            skillLabel = Label(panel, "", new Vector2(275, -200), new Vector2(570, 48), 24);
            Button(panel, "Next skill", new Vector2(860, -200), new Vector2(220, 48), () => ChangeSkill(1));
            levelGroupLabel = Label(panel, "", new Vector2(40, -256), new Vector2(1040, 28), 18);
            BuildRouteList();
            routeCountLabel = Label(panel, "", new Vector2(40, -604), new Vector2(1040, 42), 18);
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
            UiKit.CloseButton(panel, ClosePicker);
            Refresh();
            Layout();
            panel.gameObject.SetActive(false);
        }

        public void TogglePicker()
        {
            if (IsOpen) { ClosePicker(); return; }
            if (Time.timeScale <= 0f || PatchNotesUI.IsShowing) return;
            panel.gameObject.SetActive(true);
            Layout();
            PlayerHUD.SetHiddenBy(this, true);
            timeScaleBeforePicker = Time.timeScale;
            audioPausedBeforePicker = AudioListener.pause;
            pickerPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void ClosePicker()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            PlayerHUD.SetHiddenBy(this, false);
            if (!pickerPaused) return;
            pickerPaused = false;
            Time.timeScale = timeScaleBeforePicker;
            AudioListener.pause = audioPausedBeforePicker;
        }

        private void OnDestroy() => ClosePicker();

        private void BuildRouteList()
        {
            var area = UiKit.NewRect("BuildRoutes", panel);
            UiKit.TopLeft(area, new Vector2(40, -290), new Vector2(1040, 304));
            Image viewport = UiKit.NewImage("Viewport", area, Color.clear);
            UiKit.Stretch(viewport.rectTransform, 0);
            viewport.rectTransform.offsetMax = new Vector2(-20, 0);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.NewRect("Routes", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            routeScroll = area.gameObject.AddComponent<ScrollRect>();
            routeScroll.viewport = viewport.rectTransform;
            routeScroll.content = content;
            routeScroll.horizontal = false;
            routeScroll.movementType = ScrollRect.MovementType.Clamped;
            routeScroll.scrollSensitivity = 50f / 3f;

            Image track = UiKit.NewImage("Scrollbar", area, new Color(.04f, .04f, .05f));
            track.raycastTarget = true;
            track.rectTransform.anchorMin = new Vector2(1, 0);
            track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.pivot = new Vector2(1, .5f);
            track.rectTransform.sizeDelta = new Vector2(13, 0);
            var sliding = UiKit.NewRect("SlidingArea", track.transform);
            UiKit.Stretch(sliding, 2);
            Image handle = UiKit.NewImage("Handle", sliding, UiKit.BorderColor);
            handle.raycastTarget = true;
            UiKit.Stretch(handle.rectTransform, 0);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            routeScroll.verticalScrollbar = scrollbar;
        }

        private static void RoutePosition(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -y);
            rect.sizeDelta = new Vector2(0, height);
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
                        at.x = 24f; dimensions.x = w - 48f;
                    }
                    else if (y == 662f) { at = new Vector2(24f, -746f); dimensions = new Vector2(w - 48f, 240f); }
                    else if (y == 780f) { at = new Vector2(24f, at.x == 200f ? -1010f : -1080f); dimensions.x = w - 48f; }
                }
                else if (-at.y == 200f)
                {
                    string value = at.x == 40f ? "Previous skill" : at.x == 860f ? "Next skill" : null;
                    if (value != null) layoutItems[i].GetComponentInChildren<Text>().text = value;
                }
                UiKit.TopLeft(layoutItems[i], at, dimensions);
            }
            lastHudBottom = PlayerHUD.BottomPixels;
            float occupied = TouchMode.Active ? Mathf.Max(lastHudBottom, TouchControlsUI.ShortcutBottomPixels) : lastHudBottom;
            float top = occupied / canvasRect.GetComponent<Canvas>().scaleFactor + 12f;
            UiKit.TopLeft(newBuildButton, new Vector2(16f, -top), new Vector2(150f, 42f));
            UiKit.TopLeft(settingsButton, new Vector2(180f, -top), new Vector2(150f, 42f));
            status.rectTransform.anchoredPosition = new Vector2(0f, -top - 54f);
            status.rectTransform.sizeDelta = new Vector2(size.x - 32f, 65f);
            LayoutRoutes();
        }

        private void LayoutRoutes()
        {
            if (visibleChoices == null) return;
            float width = Mathf.Max(1f, routeScroll.viewport.rect.width - 8f);
            float y = 0;
            int category = 0;
            for (int i = 0; i < visibleChoices.Count; i++)
            {
                if (i == 0 || visibleChoices[i].level != visibleChoices[i - 1].level)
                {
                    if (i > 0) y += 8f;
                    RoutePosition(categoryHeaders[category++].rectTransform, y, 28f);
                    y += 32f;
                }
                Text label = routeLabels[i];
                float height = Mathf.Max(46f, label.cachedTextGeneratorForLayout.GetPreferredHeight(
                    label.text, label.GetGenerationSettings(new Vector2(width, 0f))) / label.pixelsPerUnit + 12f);
                RoutePosition(routeButtons[i].rectTransform, y, height);
                y += height + 4f;
            }
            routeScroll.content.sizeDelta = new Vector2(0f, y + 8f);
        }

        private void ChangeLevel(int delta)
        {
            int index = System.Array.IndexOf(BalanceArenaBuilds.PickerLevels, level);
            level = BalanceArenaBuilds.PickerLevels[Mathf.Clamp(index + delta, 0, BalanceArenaBuilds.PickerLevels.Length - 1)];
            randomLevel = false; Refresh();
        }
        private void ChangeSkill(int delta)
        {
            int current = System.Array.IndexOf(skills, skill);
            skill = skills[(current + delta + skills.Length) % skills.Length];
            route = null; randomBuild = false; Refresh();
            routeScroll.StopMovement();
            routeScroll.verticalNormalizedPosition = 1;
        }
        private void Refresh()
        {
            skills = BalanceArenaBuilds.Data.builds.Select(b => b.skill).Distinct().ToArray();
            if (!skills.Contains(skill)) skill = skills[0];
            choices = BalanceArenaBuilds.PickerChoices(skill).ToList();
            if (!choices.Any(b => b.id == route && b.level == level)) route = choices.First(b => b.level == level).id;
            lowerLevelButton.color = !randomLevel && level == 1 ? new Color(.45f, .45f, .45f) : Color.white;
            visibleChoices = new List<BalanceArenaBuilds.Entry>();
            var leaders = new Dictionary<int, string>();
            int category = 0;
            foreach (var group in choices.GroupBy(b => b.level).OrderBy(g => g.Key))
            {
                var ranked = group.OrderByDescending(b => BalanceArenaBuilds.EstimatedDps(b))
                    .ThenBy(b => b.name, System.StringComparer.Ordinal).ThenBy(b => b.id, System.StringComparer.Ordinal).ToList();
                leaders[group.Key] = ranked[0].id;
                if (category >= categoryHeaders.Count)
                    categoryHeaders.Add(Label(routeScroll.content, "", Vector2.zero, Vector2.zero, 18));
                Text header = categoryHeaders[category++];
                header.gameObject.SetActive(true);
                header.text = "LEVEL " + group.Key + " · " + ranked.Count + (ranked.Count == 1 ? " build" : " builds");
                header.raycastTarget = false;
                foreach (var choice in ranked)
                {
                    int index = visibleChoices.Count;
                    visibleChoices.Add(choice);
                    if (index >= routeButtons.Count)
                    {
                        Image button = Button(routeScroll.content, "", Vector2.zero, Vector2.zero, () => {
                            route = visibleChoices[index].id;
                            randomBuild = randomLevel = false;
                            level = visibleChoices[index].level;
                            StartRun();
                        });
                        routeButtons.Add(button);
                        Image highlight = UiKit.Rim(button.rectTransform, outset: -1f, tint: HighestDps);
                        highlight.name = "TopDpsFrame";
                        routeHighlights.Add(highlight);
                        Text label = button.GetComponentInChildren<Text>();
                        routeLabels.Add(label);
                    }
                }
            }
            for (int i = category; i < categoryHeaders.Count; i++) categoryHeaders[i].gameObject.SetActive(false);
            levelGroupLabel.text = "Levels 1, 10, 16, 24, 30, 34 · Highest DPS first at each level · Green frame = best at that level";
            levelLabel.text = randomLevel ? "Random milestone" : "Level " + level;
            skillLabel.text = skill + "  (" + (System.Array.IndexOf(skills, skill) + 1) + "/" + skills.Length + ")";
            randomLevelButton.color = randomLevel ? Selected : Color.white;
            randomBuildButton.color = randomBuild ? Selected : Color.white;
            for (int i = 0; i < routeButtons.Count; i++)
            {
                routeButtons[i].gameObject.SetActive(i < visibleChoices.Count);
                if (i >= visibleChoices.Count) continue;
                var choice = visibleChoices[i];
                float dps = BalanceArenaBuilds.EstimatedDps(choice);
                float bossDps = BalanceArenaBuilds.EstimatedDps(choice, boss: true);
                bool highest = choice.id == leaders[choice.level];
                routeHighlights[i].gameObject.SetActive(highest);
                routeLabels[i].text = choice.name + " · " + BalanceArenaBuilds.RouteEffects(choice) +
                    "\n≈ " + dps.ToString("N0") + " effective DPS" +
                    (Mathf.Approximately(dps, bossDps) ? "" : " · " + bossDps.ToString("N0") + " boss DPS") +
                    " at Lv " + choice.level + (highest ? " · BEST AT THIS LEVEL" : "");
                routeLabels[i].color = highest ? HighestDps : UiKit.TextColor;
                routeButtons[i].color = !randomBuild && choice.id == route && choice.level == level ? Selected :
                    highest ? new Color(.7f, 1f, .8f) : Color.white;
            }
            routeCountLabel.text = choices.Count + " builds in " + category + " level groups · Scroll or drag to browse all builds";
            detail.text = (randomBuild ? "Random build from all available skills and routes." : "Selected skill: " + skill) +
                "\nEach row starts at its listed level. Random levels use the same six milestones." +
                "\nDPS: successful full combos, full army + repeatable buffs; executes where applicable. Excludes opening/kill buffs and downtime." +
                "\nShared gear pools and passives per skill and level. Best drop per slot. 20 waves of " + GauntletMode.ArenaMinMonsters + "–" + GauntletMode.ArenaMaxMonsters +
                " enemies and occasional bosses. No XP, loot or saves.";
            LayoutRoutes();
        }
        private void StartRun()
        {
            string pickedSkill = randomBuild ? skills[Random.Range(0, skills.Length)] : skill;
            var pool = BalanceArenaBuilds.PickerChoices(pickedSkill)
                .Where(b => (randomLevel || b.level == level) && (randomBuild || randomLevel || b.id == route)).ToArray();
            // Choose a level first so random-level runs do not favour high levels with more routes.
            int[] levels = pool.Select(b => b.level).Distinct().ToArray();
            int pickedLevel = levels[Random.Range(0, levels.Length)];
            pool = pool.Where(b => b.level == pickedLevel).ToArray();
            ClosePicker();
            mode.BeginArena(pool[Random.Range(0, pool.Length)]);
        }
        private void Update()
        {
            if (canvasRect != null && (canvasRect.rect.size != lastCanvasSize || PlayerHUD.BottomPixels != lastHudBottom)) Layout();
            if (mode == null || status == null) return;
            if (!mode.Started)
            {
                status.text = "Level 1 · Explore the arena\nEsc or Arena builds to choose a build";
                return;
            }
            status.text = mode.Build.Name + " · Level " + mode.Level + "\n" + mode.Status + " · Deaths " + mode.Deaths;
            if (mode.Finished) status.text += " · Open Arena builds to play again";
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
            UiKit.StyleButton(image, text);
            UiKit.OnClick(image, click);
            return image;
        }
    }
}
