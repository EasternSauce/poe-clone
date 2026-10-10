using UnityEngine;
using UnityEngine.UI;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// A wide health bar across the top of the screen, with the boss's name, while a living boss
    /// is near the player (spectators too: it reads the replicated health). Installed by
    /// GameSessionController.
    /// </summary>
    public class BossBarUI : MonoBehaviour
    {
        private const float ShowRange = 15f;
        private const float Width = 760f;

        private GameObject root;
        private Image fill;
        private Text title;
        private EnemyHealth boss;
        private float nextSearch;
        public bool IsShowing => root != null && root.activeSelf;

        private void Awake()
        {
            Canvas canvas = UiKit.NewCanvas("BossBarCanvas", transform, 45, out _);

            Image back = UiKit.NewImage("BossBar", canvas.transform, new Color(0.03f, 0.025f, 0.02f, 0.85f));
            RectTransform rt = back.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -54f);
            rt.sizeDelta = new Vector2(Width, 26f);
            root = back.gameObject;

            Image shade = UiKit.NewImage("Shadow", rt, new Color(0f, 0f, 0f, 0.5f));
            shade.sprite = UiKit.Glow;
            UiKit.Stretch(shade.rectTransform, -60f);
            shade.transform.SetAsFirstSibling();

            fill = UiKit.NewImage("Fill", rt, new Color(0.78f, 0.12f, 0.09f, 1f));
            fill.sprite = UiKit.BarFillSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            UiKit.Stretch(fill.rectTransform, 2f);
            UiKit.Rim(rt, 1f);

            // A gold stud capping each end.
            for (int side = 0; side < 2; side++)
            {
                Image stud = UiKit.NewImage("Stud", rt, UiKit.Gold);
                stud.sprite = UiKit.Diamond;
                RectTransform sr = stud.rectTransform;
                sr.anchorMin = sr.anchorMax = new Vector2(side, 0.5f);
                sr.sizeDelta = new Vector2(20f, 34f);
                UiKit.AddOutline(stud, new Color(0f, 0f, 0f, 0.8f), 1f);
            }

            title = UiKit.NewText("Name", rt, "", 24, UiKit.Gold, TextAnchor.LowerCenter);
            title.font = UiKit.TitleFont;
            UiKit.AddOutline(title, Color.black, 1.5f);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.anchoredPosition = new Vector2(0f, 2f);
            tr.sizeDelta = new Vector2(0f, 30f);

            root.SetActive(false);
        }

        private void Update()
        {
            Camera cam = Camera.main;
            Transform focus = cam != null ? FindFocus() : null;

            if (Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                boss = focus != null ? NearestBoss(focus.position) : null;
            }

            bool actArena = AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == WorldBuilder.ActArena;
            bool show = boss != null && !boss.IsDead && !boss.HideBossBar && focus != null &&
                        (actArena && EnemyKinds.Get(boss.KindIndex).Boss == BossStyle.Shepherd ||
                         (boss.transform.position - focus.position).sqrMagnitude <= ShowRange * ShowRange);
            root.SetActive(show);
            if (!show)
                return;

            title.text = boss.DisplayName;
            fill.fillAmount = boss.MaxHealth > 0f ? boss.CurrentHealth / boss.MaxHealth : 0f;
        }

        private Transform player;

        private Transform FindFocus()
        {
            if (player == null)
            {
                var stats = FindAnyObjectByType<Player.PlayerStats>();
                player = stats != null ? stats.transform : null;
            }
            return player;
        }

        private static EnemyHealth NearestBoss(Vector3 from)
        {
            EnemyHealth best = null;
            bool actArena = AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == WorldBuilder.ActArena;
            EnemyHealth arenaBoss = actArena && ActBossArena.Instance != null ? ActBossArena.Instance.Boss : null;
            if (arenaBoss != null && !arenaBoss.IsDead) return arenaBoss;
            float bestDistance = actArena ? float.MaxValue : ShowRange * ShowRange;
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>())
            {
                if (enemy.IsDead || !EnemyKinds.Get(enemy.KindIndex).IsBoss)
                    continue;
                if (actArena && EnemyKinds.Get(enemy.KindIndex).Boss != BossStyle.Shepherd)
                    continue;
                float d = (enemy.transform.position - from).sqrMagnitude;
                if (d <= bestDistance)
                {
                    best = enemy;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
