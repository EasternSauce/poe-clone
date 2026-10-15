using PoeClone.Inventory;

namespace PoeClone.Skills
{
    /// <summary>
    /// Owns action-bar bindings and their cooldowns. Gear eligibility, input, potion bindings,
    /// and notifications belong to PlayerSkills; this state can be changed without a scene.
    /// </summary>
    internal sealed class SkillBindings
    {
        internal struct Binding
        {
            public SkillId? Skill;
            public ItemData Item;
            public EquipSlot Source;
            public int GrantLevel;
            public float ReadyAt;
            public float Cooldown;
        }

        private readonly Binding[] entries;
        public int Count => entries.Length;
        public Binding this[int index] => entries[index];

        public SkillBindings(int count)
        {
            entries = new Binding[count];
        }

        public void Clear(int slot)
        {
            ref Binding binding = ref entries[slot];
            binding.Skill = null;
            binding.Item = null;
            binding.ReadyAt = binding.Cooldown = 0f;
        }

        public void SetSource(int slot, SkillId skill, ItemData item, EquipSlot source, int grantLevel)
        {
            ref Binding binding = ref entries[slot];
            binding.Skill = skill;
            binding.Item = item;
            binding.Source = source;
            binding.GrantLevel = grantLevel;
        }

        public void SetPlaceholder(int slot, SkillId skill)
        {
            entries[slot].Skill = skill;
            entries[slot].Item = null;
        }

        public void Assign(int slot, SkillId skill, ItemData item, EquipSlot source, int grantLevel, bool moveExisting)
        {
            float ready = entries[slot].Skill == skill ? entries[slot].ReadyAt : 0f;
            float cooldown = entries[slot].Skill == skill ? entries[slot].Cooldown : 0f;
            for (int i = 0; i < Count; i++)
                if (i != slot && entries[i].Skill == skill && entries[i].ReadyAt >= ready)
                {
                    ready = entries[i].ReadyAt;
                    cooldown = entries[i].Cooldown;
                }

            // Moving a skill cannot bypass a cooldown. Spectators may mirror duplicate slots.
            if (moveExisting)
                for (int i = 0; i < Count; i++)
                    if (i != slot && entries[i].Skill == skill) Clear(i);

            SetSource(slot, skill, item, source, grantLevel);
            entries[slot].ReadyAt = ready;
            entries[slot].Cooldown = cooldown;
        }

        public void StartCooldown(int slot, float cooldown, float now)
        {
            Binding source = entries[slot];
            for (int i = 0; i < Count; i++)
            {
                ref Binding binding = ref entries[i];
                if (binding.Skill == source.Skill && binding.Item == source.Item && binding.GrantLevel == source.GrantLevel)
                {
                    binding.Cooldown = cooldown;
                    binding.ReadyAt = now + cooldown;
                }
            }
        }
    }
}
