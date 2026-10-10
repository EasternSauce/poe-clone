using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Co-op: the partner's character in this game. The partner plays it on their own machine
    /// (stats, gear, skills, damage and saves all live there); here it's posed from their
    /// snapshots - moving, swinging, casting, flinching and dying as they do - and it's solid,
    /// so the two characters bump into each other. On the host, enemies can go for it
    /// (<see cref="Party.Partner"/>); their attacks on it are sent to the partner to resolve.
    /// It has no health component, so nothing either player does can hurt it.
    /// </summary>
    public class CoopPartner : MonoBehaviour
    {
        // Co-op snapshots come 20 times a second; buffer less than a spectator does and let the
        // timeline's extrapolation cover a late one.
        private const float MinDelay = 0.05f;
        private const float SmoothingRate = 25f;
        private const float SmoothingMaxError = 1.5f;
        private static readonly Color LabelColor = new Color(0.55f, 0.85f, 1f);

        private readonly SnapshotTimeline timeline = new SnapshotTimeline(MinDelay);
        private readonly List<StateSnapshot> due = new List<StateSnapshot>();
        private readonly EntityState scratch = new EntityState();
        private readonly EquipmentSet equipment = new EquipmentSet();
        private readonly string[] appliedEquipment = new string[SlotRules.AllSlots.Length];
        private readonly Dictionary<string, ItemData> displayItems = new Dictionary<string, ItemData>();

        private GameObject body;
        private CharacterAttackAnimator attack;
        private CharacterWalkAnimator walk;
        private Stagger stagger;
        private WorldLabel label;
        private RectTransform healthFill;
        private string partnerName;
        private readonly PlayerHUD.PartnerStatus status = new PlayerHUD.PartnerStatus();

        private EntityState applied;
        private int castsApplied = -1;
        private int extraArrows;
        private int partnerArea = -1;
        private bool loading;
        private bool hasPose;
        private SpectatorReplica minionReplica;

        /// <summary>The partner's latest snapshot (where they are, which area), or null before the first.</summary>
        public StateSnapshot Newest => timeline.Newest;

        public bool PartnerDead => applied != null && applied.d != 0;

        /// <summary>Builds the partner's body from this game's own player model.</summary>
        public void Begin(Transform localPlayer, string name)
        {
            partnerName = string.IsNullOrEmpty(name) ? "Partner" : name;
            body = new GameObject("CoopPartner " + partnerName);
            body.SetActive(false);
            body.layer = 2; // Ignore Raycast: arrows, spells and click targeting go through it

            var cc = localPlayer.GetComponent<CharacterController>();
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = cc != null ? cc.radius : 0.4f;
            capsule.height = cc != null ? cc.height : 2f;
            capsule.center = cc != null ? cc.center : Vector3.zero;
            body.AddComponent<Rigidbody>().isKinematic = true; // a moving collider
            stagger = body.AddComponent<Stagger>();

            // Instantiated into the inactive body, so none of the copied scripts wake up before
            // the ones a puppet doesn't need are stripped (like the character select portraits).
            Transform model = localPlayer.Find("Model");
            if (model != null)
            {
                GameObject copy = Instantiate(model.gameObject, body.transform);
                copy.name = "Model";
                copy.transform.localPosition = model.localPosition;
                copy.transform.localRotation = model.localRotation;
                copy.transform.localScale = model.localScale;
                foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb is EquipmentVisuals || mb is BaseGear || mb is EquipmentVisualInstance ||
                        mb is CharacterWalkAnimator || mb is CharacterAttackAnimator)
                        continue;
                    DestroyImmediate(mb);
                }
                foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
                    DestroyImmediate(c);
                attack = copy.GetComponent<CharacterAttackAnimator>();
                walk = copy.GetComponent<CharacterWalkAnimator>();
            }

            label = WorldLabel.Create(body.transform, partnerName, LabelColor, 2.75f, 26);
            healthFill = BuildHealthBar(label.transform);
            status.Name = partnerName;
            PlayerHUD.Partner = status;
            body.SetActive(true);
            body.GetComponentInChildren<EquipmentVisuals>()?.Bind(equipment);
            if (attack != null)
                attack.StrikeFrame += OnStrike;
            SetShown(false);
            minionReplica = gameObject.AddComponent<SpectatorReplica>();
            minionReplica.EnterCoopMinions();
        }

        // A slim health bar under the floating name, turning with it to face the camera.
        private static RectTransform BuildHealthBar(Transform labelCanvas)
        {
            Image back = UiKit.NewImage("HealthBar", labelCanvas, new Color(0f, 0f, 0f, 0.75f));
            RectTransform rect = back.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 14f);
            rect.anchoredPosition = new Vector2(0f, -34f);
            Image fill = UiKit.NewImage("Fill", rect, new Color(0.8f, 0.16f, 0.16f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            return fillRect;
        }

        public void Feed(StateSnapshot s)
        {
            timeline.Add(s, Time.realtimeSinceStartupAsDouble);
            minionReplica?.HandleCoopState(s);
        }

        private void OnDestroy()
        {
            if (minionReplica != null)
            {
                minionReplica.ClearCoopEnemies();
                Destroy(minionReplica);
            }
            if (PlayerHUD.Partner == status)
                PlayerHUD.Partner = null;
            if (body != null)
                Destroy(body);
            if (Party.Partner != null && body != null && Party.Partner == body.transform)
                Party.Partner = null;
        }

        private void Update()
        {
            if (body == null)
                return;
            if (!timeline.HasData)
            {
                SetShown(false);
                return;
            }

            timeline.Advance(Time.realtimeSinceStartupAsDouble);
            due.Clear();
            timeline.CollectDue(due);
            for (int k = 0; k < due.Count; k++)
                ApplyDiscrete(due[k]);

            if (!timeline.TryGetFrame(out StateSnapshot from, out StateSnapshot to, out float alpha))
                return;
            if (applied == null)
                ApplyDiscrete(from);

            var areas = AreaManager.Instance;
            bool here = areas != null && areas.CurrentAreaIndex == partnerArea && !loading;
            SetShown(here);
            if (here)
                Pose(from, to, alpha);

            Party.Partner = here ? body.transform : null;
            Party.PartnerAlive = !PartnerDead;
            status.Here = here;
            status.Dead = PartnerDead;
        }

        private void SetShown(bool shown)
        {
            if (body.activeSelf == shown)
                return;
            body.SetActive(shown);
            hasPose = false;
        }

        private void ApplyDiscrete(StateSnapshot s)
        {
            if (partnerArea != s.area)
            {
                partnerArea = s.area;
                AreaDefinition[] areas = AreaManager.Instance != null ? AreaManager.Instance.areas : null;
                status.Area = areas != null && partnerArea >= 0 && partnerArea < areas.Length ? areas[partnerArea].areaName : null;
            }
            loading = s.fade != 0;
            ApplyEquipment(s.eq);
            if (s.hud != null)
            {
                extraArrows = s.hud.arw;
                status.Level = s.hud.lv;
                status.Health = s.hud.hp;
                status.MaxHealth = s.hud.mhp;
                status.Mana = s.hud.mp;
                status.MaxMana = s.hud.mmp;
                if (healthFill != null)
                    healthFill.anchorMax = new Vector2(s.hud.mhp > 0f ? Mathf.Clamp01(s.hud.hp / s.hud.mhp) : 0f, 1f);
            }
            ApplyEvents(s.p);
            ApplyCasts(s.sc, s.p);
        }

        private void ApplyEvents(EntityState p)
        {
            if (p == null)
                return;
            EntityState prev = applied;
            applied = p.Clone();
            Transform t = body.transform;
            if (prev == null)
            {
                if (p.d != 0)
                    CharacterDeathAnimator.PlayOn(t);
                return;
            }

            if (p.d != 0 && prev.d == 0)
                CharacterDeathAnimator.PlayOn(t);
            else if (p.d == 0 && prev.d != 0)
            {
                CharacterDeathAnimator.ResetOn(t);
                hasPose = false; // revived at a spawn point: snap there
            }

            if (p.d == 0 && p.atk > prev.atk && attack != null && body.activeInHierarchy)
            {
                attack.PlayReplicated(p.ap);
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.playerSwing) : null, t.position);
            }

            if (p.d == 0 && p.stg > prev.stg && body.activeInHierarchy)
            {
                stagger.Trigger();
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.playerHurt) : null, t.position);
            }
        }

        // The partner's skills, drawn on their character here: the look only, their hits reach the
        // host's enemies as claims.
        private void ApplyCasts(SkillCastState[] casts, EntityState p)
        {
            if (casts == null)
                return;
            int newest = castsApplied;
            foreach (SkillCastState c in casts)
            {
                if (c != null && c.n > newest)
                    newest = c.n;
            }
            if (castsApplied < 0 || (p != null && p.d != 0) || !body.activeInHierarchy)
            {
                castsApplied = Mathf.Max(0, Mathf.Max(castsApplied, newest));
                return;
            }

            foreach (SkillCastState c in casts)
            {
                if (c == null || c.n <= castsApplied)
                    continue;
                Vector3[] points = null;
                if (c.pts != null && c.pts.Length >= 6)
                {
                    points = new Vector3[c.pts.Length / 3];
                    for (int k = 0; k < points.Length; k++)
                        points[k] = new Vector3(c.pts[k * 3], c.pts[k * 3 + 1], c.pts[k * 3 + 2]);
                }
                Skills.PlayerSkills.PlayVisual(new Skills.PlayerSkills.CastRecord
                {
                    Number = c.n,
                    Skill = (Skills.SkillId)c.s,
                    Level = c.lv,
                    At = new Vector3(c.x, c.y, c.z),
                    Facing = new Vector3(c.dx, 0f, c.dz),
                    Size = c.sz,
                    Count = c.c,
                    Points = points
                }, body.transform);
            }
            castsApplied = newest;
        }

        // A bow shot looses harmless arrows the way the partner's did.
        private void OnStrike()
        {
            if (attack == null || !CharacterAttackAnimator.IsRangedProfile(attack.ProfileId))
                return;
            Transform shooter = body.transform;
            float range = CharacterAttackAnimator.AttackRange(WeaponType.Bow);
            foreach (Vector3 direction in HitEffects.Spread(shooter.forward, 1 + extraArrows, PlayerCombat.ArrowSpreadDegrees))
                PlayerArrow.LaunchVisual(shooter, range, direction);
        }

        // Two passes, like the spectator's: empty every changed slot, then fill them, so a swap is
        // never refused halfway by the hand rules.
        private void ApplyEquipment(string[] eq)
        {
            if (eq == null)
                return;
            EquipSlot[] slots = SlotRules.AllSlots;
            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                if (appliedEquipment[k] != (eq[k] ?? string.Empty))
                    equipment.Unequip(slots[k]);
            }
            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                string id = eq[k] ?? string.Empty;
                if (appliedEquipment[k] == id)
                    continue;
                appliedEquipment[k] = id;
                if (id.Length == 0)
                    continue;
                if (!displayItems.TryGetValue(id, out ItemData item))
                    displayItems[id] = item = ItemGenerator.Display(id, null, ItemRarity.Normal);
                if (item != null)
                    equipment.Restore(slots[k], item, out _);
            }
        }

        private void Pose(StateSnapshot from, StateSnapshot to, float alpha)
        {
            SnapshotTimeline.Interpolate(from.p, to.p, alpha, scratch);
            Vector3 target = new Vector3(scratch.x, scratch.y, scratch.z);
            Quaternion facing = Quaternion.Euler(0f, scratch.r, 0f);
            Transform t = body.transform;

            Vector3 error = target - t.position;
            if (!hasPose || error.sqrMagnitude > SmoothingMaxError * SmoothingMaxError)
            {
                t.SetPositionAndRotation(target, facing);
                if (walk != null && walk.enabled)
                    walk.ResetAnimatorState();
                hasPose = true;
                return;
            }

            // Small corrections (a guess past the newest snapshot that turned out wrong) are eased
            // in over a few frames instead of snapping.
            float blend = 1f - Mathf.Exp(-SmoothingRate * Time.deltaTime);
            t.SetPositionAndRotation(t.position + error * blend, Quaternion.Slerp(t.rotation, facing, blend));
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
        }

        private static void PlaySfx(AudioClip clip, Vector3 position)
        {
            if (clip != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(clip, position);
        }
    }
}
