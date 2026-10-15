using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>Browser entry to the actual sampled balance builds, filtered by tested level.</summary>
    public sealed class BalanceArenaUI : MonoBehaviour
    {
        private GauntletMode mode;
        private RectTransform panel;
        private Text status, levelLabel, skillLabel, detail, pageLabel;
        private Image randomLevelButton, randomBuildButton;
        private readonly List<Image> routeButtons = new List<Image>();
        private readonly List<Text> routeLabels = new List<Text>();
        private string skill, route;
        private int level = 14, page;
        private bool randomLevel, randomBuild;
        private List<BalanceArenaBuilds.Entry> choices;
        private string[] skills;
        private const int PageSize = 6;
        private static readonly Color Selected = new Color(1f, .62f, .3f);

        public void Bind(GauntletMode gauntlet)
        {
            mode = gauntlet;
            Canvas canvas = UiKit.NewCanvas("BalanceArena", transform, 760, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = group.blocksRaycasts = true;
            status = UiKit.NewText("Status", canvas.transform, "", 19, UiKit.TextColor, TextAnchor.UpperCenter);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(.5f, 1f);
            status.rectTransform.anchoredPosition = new Vector2(0, -12);
            status.rectTransform.sizeDelta = new Vector2(1000, 65);
            UiKit.AddOutline(status, Color.black, 1.5f);
            Button(canvas.transform, "New build", new Vector2(18, -16), new Vector2(150, 42),
                MinimalCombatMode.RestartArena);

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

            Button(panel, "−", new Vector2(40, -130), new Vector2(65, 48), () => ChangeLevel(-1));
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
            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                Image b = Button(panel, "", new Vector2(40, -270 - i * 54), new Vector2(1040, 46), () => {
                    route = choices[page * PageSize + index].id; randomBuild = false; Refresh();
                });
                routeButtons.Add(b);
                routeLabels.Add(b.GetComponentInChildren<Text>());
            }
            Button(panel, "Previous routes", new Vector2(40, -604), new Vector2(240, 42), () => { page--; Refresh(); });
            pageLabel = Label(panel, "", new Vector2(290, -604), new Vector2(540, 42), 18);
            Button(panel, "Next routes", new Vector2(840, -604), new Vector2(240, 42), () => { page++; Refresh(); });
            detail = Label(panel, "", new Vector2(40, -662), new Vector2(1040, 100), 18);
            Button(panel, "Enter arena", new Vector2(200, -780), new Vector2(330, 54), StartRun, 24);
            Button(panel, "Random level + build", new Vector2(590, -780), new Vector2(330, 54), () => {
                randomLevel = randomBuild = true; StartRun();
            }, 24);
            Refresh();
        }

        private void ChangeLevel(int delta)
        {
            level = Mathf.Clamp(level + delta, 1, 34);
            randomLevel = false; page = 0; Refresh();
        }
        private IEnumerable<BalanceArenaBuilds.Entry> Available => BalanceArenaBuilds.Data.builds.Where(b => randomLevel || b.level == level);
        private void ChangeSkill(int delta)
        {
            int current = System.Array.IndexOf(skills, skill);
            skill = skills[(current + delta + skills.Length) % skills.Length];
            route = null; randomBuild = false; page = 0; Refresh();
        }
        private void Refresh()
        {
            skills = Available.Select(b => b.skill).Distinct().ToArray();
            if (!skills.Contains(skill)) skill = skills[0];
            choices = Available.Where(b => b.skill == skill).GroupBy(b => b.id).Select(g => g.First()).ToList();
            if (!choices.Any(b => b.id == route)) route = choices[0].id;
            int pages = Mathf.Max(1, (choices.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            levelLabel.text = randomLevel ? "Level 1–34" : "Level " + level;
            skillLabel.text = skill + "  (" + (System.Array.IndexOf(skills, skill) + 1) + "/" + skills.Length + ")";
            randomLevelButton.color = randomLevel ? Selected : Color.white;
            randomBuildButton.color = randomBuild ? Selected : Color.white;
            for (int i = 0; i < PageSize; i++)
            {
                int at = page * PageSize + i;
                routeButtons[i].gameObject.SetActive(at < choices.Count);
                if (at >= choices.Count) continue;
                routeLabels[i].text = choices[at].name;
                routeButtons[i].color = !randomBuild && choices[at].id == route ? Selected : Color.white;
            }
            pageLabel.text = "Routes " + (page + 1) + " / " + pages;
            detail.text = (randomBuild ? "Random build from all available skills and routes." : "Selected: " + skill + " · " + choices.First(b => b.id == route).name) +
                "\nRecorded best-of-20 rare gear, character passives and weapon tree; no uniques. Random level uses a tested level for the chosen route." +
                "\nFight 20 waves of 1–5 enemies with occasional bosses, all at your level. Enemies stay aggressive. No XP, loot or character saves.";
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
            UiKit.StyleButton(image, text);
            UiKit.OnClick(image, click);
            return image;
        }
    }
}
