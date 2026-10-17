using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>A stationary, attackable spell proxy. Minion supplies targeting, life bars and replication.</summary>
    public sealed class SpellEffigy : MonoBehaviour
    {
        private Transform owner;
        private PlayerSkills skills;
        private PlayerInventory inventory;
        private PlayerStats stats;
        private Minion life;
        private ItemData weapon;
        private float expiresAt, readyAt;
        public SkillId Skill { get; private set; }
        public int Level { get; private set; }
        public WeaponSkillBonuses Tree { get; private set; }
        public int CastUses { get; set; }

        public void Initialize(Transform caster, Minion health, SkillId skill, int level)
        {
            owner = caster;
            skills = caster.GetComponent<PlayerSkills>();
            inventory = caster.GetComponent<PlayerInventory>();
            stats = caster.GetComponent<PlayerStats>();
            life = health;
            Skill = skill;
            Level = level;
            weapon = inventory.Equipment.Get(EquipSlot.MainHand);
            Tree = skills.BonusesFor(skill);
            expiresAt = Time.time + EffigyRules.Lifetime;
            readyAt = Time.time + 0.25f;
        }

        private void Update()
        {
            if (life == null || life.IsDead) return;
            if (owner == null || skills == null || stats == null || stats.IsDead || Time.time >= expiresAt ||
                inventory.Stats.Total(StatType.SpellboundEffigies) <= 0f || inventory.Equipment.Get(EquipSlot.MainHand) != weapon ||
                skills.Level(Skill) <= 0 || Vector3.Distance(owner.position, transform.position) > 32f ||
                World.AreaManager.Instance != null && World.AreaManager.Instance.IsSwitching)
            {
                life.Crumble();
                return;
            }
            int owned = 0;
            Minion oldest = null;
            foreach (var minion in Minion.All)
                if (minion != null && !minion.IsDead && minion.Kind == MinionKind.Effigy && minion.Owner == owner)
                {
                    owned++;
                    if (oldest == null) oldest = minion;
                }
            if (owned > EffigyRules.Limit(inventory.Stats) && oldest == life)
            {
                life.Crumble();
                return;
            }
            life.RegenerateEffigy(inventory.Stats.Total(StatType.EffigyRegen) * Time.deltaTime);
            if (Time.time < readyAt) return;
            float range = skills.EffigyReach(Skill, Tree) * Mathf.Max(0.1f, 1f + inventory.Stats.Total(StatType.EffigyRange) / 100f);
            EnemyHealth target = null;
            float nearest = range * range;
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy == null || enemy.IsDead || enemy.IsAlly || !enemy.gameObject.activeInHierarchy) continue;
                Vector3 offset = enemy.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < nearest) { target = enemy; nearest = offset.sqrMagnitude; }
            }
            if (target == null) return;
            Vector3 facing = target.transform.position - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            readyAt = Time.time + skills.EffigyInterval(Skill, Tree);
            skills.CastFromEffigy(this, target);
        }

        public static void BuildVisual(Transform model, Color? colour = null)
        {
            if (model.Find("EffigyVisual") != null) return;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            foreach (Transform child in model) child.gameObject.SetActive(false);
            var root = new GameObject("EffigyVisual").transform;
            root.SetParent(model, false);
            Color stone = new Color(0.25f, 0.28f, 0.34f);
            Color glow = colour ?? new Color(0.4f, 0.8f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var baseRing = RuntimePrimitives.Create(PrimitiveType.Cylinder, root, stone);
                baseRing.transform.localPosition = Vector3.up * (0.1f + i * 0.18f);
                baseRing.transform.localScale = new Vector3(1.1f - i * 0.2f, 0.09f, 1.1f - i * 0.2f);
            }
            var pillar = RuntimePrimitives.Create(PrimitiveType.Cube, root, stone * 1.25f);
            pillar.transform.localPosition = Vector3.up * 0.95f;
            pillar.transform.localScale = new Vector3(0.36f, 1f, 0.36f);
            pillar.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var crystal = RuntimePrimitives.Create(PrimitiveType.Sphere, root, glow);
            crystal.transform.localPosition = Vector3.up * 1.65f;
            crystal.transform.localScale = new Vector3(0.4f, 0.65f, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                var rune = RuntimePrimitives.Create(PrimitiveType.Cube, root, glow);
                rune.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.42f, 1.25f, Mathf.Sin(angle) * 0.42f);
                rune.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
            }
        }
    }
}
