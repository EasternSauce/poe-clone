using System.Collections.Generic;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.Skills
{
    /// <summary>Host-owned on-kill projectiles. Shards have no weapon tree, so kills cannot recurse.</summary>
    public static class BonecrusherShards
    {
        public static void Release(EnemyHealth corpse, float speedMultiplier, int seed)
        {
            Vector3 at = corpse.transform.position;
            var targets = new List<Vector3>();
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy == null || enemy == corpse || enemy.IsDead || enemy.IsAlly || !enemy.isActiveAndEnabled) continue;
                Vector3 offset = enemy.transform.position - at;
                offset.y = 0f;
                if (offset.sqrMagnitude <= WeaponSkillRules.BonecrusherRange * WeaponSkillRules.BonecrusherRange)
                    targets.Add(enemy.transform.position);
            }
            var directions = WeaponSkillRules.BonecrusherDirections(at, targets, seed);
            var values = new float[4 + directions.Length * 2];
            values[0] = at.x; values[1] = at.y; values[2] = at.z; values[3] = speedMultiplier;
            PlaySound(at);
            for (int i = 0; i < directions.Length; i++)
            {
                PlayerArrow.LaunchBoneShard(at, directions[i], corpse.MaxHealth * WeaponSkillRules.BonecrusherLifeFraction, speedMultiplier);
                values[4 + i * 2] = directions[i].x;
                values[5 + i * 2] = directions[i].z;
            }
            Party.EffectShown?.Invoke(Party.EffectKind.BoneShards, values);
        }

        public static void PlayVisual(Vector3 at, float[] values)
        {
            PlaySound(at);
            for (int i = 0; i < WeaponSkillRules.BonecrusherShardCount; i++)
                PlayerArrow.LaunchBoneShard(at, new Vector3(values[4 + i * 2], 0f, values[5 + i * 2]), 0f, values[3], harmless: true);
        }

        private static void PlaySound(Vector3 at)
        {
            var audio = AudioManager.Instance;
            if (audio != null) audio.PlayEffect("combat.pulverize.bonecrusher", at, audio.Sfx("bonecrusher_proc"));
        }
    }
}
