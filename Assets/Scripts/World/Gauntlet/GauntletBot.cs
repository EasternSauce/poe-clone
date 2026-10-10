using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>
    /// Plays the automated gauntlet through the touch inputs (the aim stick and skill presses):
    /// walks into range of the nearest enemy, holds the attack on it, uses bar skills when they're
    /// ready, keeps summons up (only into room under each kind's limit), and drinks potions when low. Out of potions, it gets 20 more
    /// (counted as refills in the results).
    /// </summary>
    public sealed class GauntletBot : MonoBehaviour
    {
        private GauntletMode mode;
        private GauntletBuild build;
        private PlayerStats player;
        private PlayerSkills skills;
        private PlayerPotions potions;
        private PlayerInventory inventory;
        private bool running;
        private float nextSkillAt;
        private Vector3 lastPosition;
        private float stuckFor;

        public void Bind(GauntletMode gauntlet, GauntletBuild playing)
        {
            mode = gauntlet;
            build = playing;
            player = FindAnyObjectByType<PlayerStats>();
            skills = player.GetComponent<PlayerSkills>();
            potions = player.GetComponent<PlayerPotions>();
            inventory = player.GetComponent<PlayerInventory>();
            TouchMode.SetForced(true);
            running = true;
            // Faster than real time: the numbers are in game seconds either way.
            if (float.TryParse(MinimalCombatMode.Option("speed"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float speed) && speed > 0f)
                Time.timeScale = speed;
            ToggleBowSkill();
        }

        public void Stop()
        {
            running = false;
            VirtualInput.Clear();
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (running) Stop();
        }

        private void Update()
        {
            if (!running || player == null || player.IsDead || Time.timeScale <= 0f) return;
            Potions();

            EnemyHealth target = Nearest();
            if (target == null)
            {
                VirtualInput.AttackHeld = false;
                VirtualInput.Aim = Vector2.zero;
                VirtualInput.Move = Vector2.zero;
                return;
            }

            Vector3 to = target.transform.position - player.transform.position;
            to.y = 0f;
            // Big bodies (bosses) keep the player further from their centre.
            float reach = (build.Melee ? 1.3f : 7f) + 1.1f * EnemyKinds.Get(target.KindIndex).Scale;
            Vector2 stick = Stick(to);
            // Walking but not getting anywhere (pressed against the enemy): swing from here.
            float moved = (player.transform.position - lastPosition).magnitude;
            lastPosition = player.transform.position;
            stuckFor = VirtualInput.Move.sqrMagnitude > 0f && moved < 0.01f ? stuckFor + Time.deltaTime : 0f;
            if (to.magnitude > reach && stuckFor < 0.4f)
            {
                VirtualInput.Move = stick;
                VirtualInput.AttackHeld = false;
                VirtualInput.Aim = Vector2.zero;
                return;
            }
            VirtualInput.Move = Vector2.zero;
            VirtualInput.Aim = stick;
            VirtualInput.AttackHeld = true;
            UseSkills(to.magnitude);
        }

        // A world direction as the screen-relative stick input PlayerController reads back.
        private static Vector2 Stick(Vector3 world)
        {
            if (world.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector3 forward = Vector3.forward;
            if (Camera.main != null)
            {
                forward = Camera.main.transform.forward;
                forward.y = 0f;
                forward.Normalize();
            }
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 d = world.normalized;
            return new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, forward)).normalized;
        }

        private EnemyHealth Nearest()
        {
            EnemyHealth best = null;
            float bestSq = float.MaxValue;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead) continue;
                float sq = (e.transform.position - player.transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        private void Potions()
        {
            // A mana potion restores half the pool: drink once it won't be wasted.
            if (!player.UsesBloodMagic && player.CurrentMana < player.MaxMana * 0.45f)
            {
                if (inventory.ManaPotions == 0)
                {
                    inventory.SetPotions(inventory.HealthPotions, PlayerInventory.MaxPotions);
                    mode.Recorder.Refilled(false);
                }
                potions.DrinkMana();
            }
            if (player.CurrentHealth < player.MaxHealth * 0.4f)
            {
                if (inventory.HealthPotions == 0)
                {
                    inventory.SetPotions(PlayerInventory.MaxPotions, inventory.ManaPotions);
                    mode.Recorder.Refilled(true);
                }
                potions.DrinkHealth();
            }
        }

        private void ToggleBowSkill()
        {
            if (build.BowSkill == null && !HasBowSkillOnBar()) return;
            int fallback = -1;
            for (int k = 0; k < SkillBook.SlotCount; k++)
            {
                SkillId? id = skills.Slot(k);
                if (id == null || !SkillBook.Get(id.Value).Bow) continue;
                if (id == build.BowSkill) { skills.TryUse(k); return; }
                if (fallback < 0) fallback = k;
            }
            if (fallback >= 0) skills.TryUse(fallback);
        }

        private bool HasBowSkillOnBar()
        {
            for (int k = 0; k < SkillBook.SlotCount; k++)
                if (skills.Slot(k) != null && SkillBook.Get(skills.Slot(k).Value).Bow) return true;
            return false;
        }

        private void UseSkills(float distance)
        {
            if (Time.time < nextSkillAt) return;
            for (int k = 0; k < SkillBook.SlotCount; k++)
            {
                SkillId? id = skills.Slot(k);
                if (id == null) continue;
                SkillDefinition skill = SkillBook.Get(id.Value);
                if (skill.Bow || id == SkillId.Dash || id == SkillId.Teleport) continue;
                if (skills.CooldownLeftAt(k) > 0f || !skills.CanAffordAt(k)) continue;
                if (id == SkillId.FrostNova && distance > 4f) continue;
                // Like a player keeping an army up: recast a summon only when its kind has room
                // (wolves, the golem and vipers once theirs are gone or short).
                // One permanent kind only: don't swap a living army for another.
                if (skill.Summon && Minion.IsPermanent(PlayerSkills.MinionKindOf(id.Value)) && LivingPermanentOtherThan(PlayerSkills.MinionKindOf(id.Value))) continue;
                if (skill.Summon && Living(PlayerSkills.MinionKindOf(id.Value)) >= Minion.KindCap(PlayerSkills.MinionKindOf(id.Value), skills.Level(id.Value), inventory.Stats)) continue;
                if (skills.TryUse(k))
                {
                    // One skill at a time, like a player: the next waits for this one's cast.
                    nextSkillAt = Time.time + 0.35f;
                    return;
                }
            }
        }

        private static bool LivingPermanentOtherThan(MinionKind kind)
        {
            foreach (Minion m in Minion.All)
                if (m != null && !m.IsDead && m.Kind != kind && Minion.IsPermanent(m.Kind)) return true;
            return false;
        }

        private static int Living(MinionKind kind)
        {
            int n = 0;
            foreach (Minion m in Minion.All)
                if (m != null && !m.IsDead && m.Kind == kind) n++;
            return n;
        }
    }
}
