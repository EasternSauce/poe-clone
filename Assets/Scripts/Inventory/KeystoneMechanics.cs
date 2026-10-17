using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    public enum EquilibriumElement { None, Fire, Cold, Lightning }

    /// <summary>Each deferred hit keeps its own deadline; later hits never extend old debt.</summary>
    public sealed class DamageDebt
    {
        public const float Duration = 4f;
        private struct Installment
        {
            public float Remaining;
            public float Seconds;
        }
        private readonly List<Installment> installments = new List<Installment>();
        public float Total { get; private set; }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            installments.Add(new Installment { Remaining = amount, Seconds = Duration });
            Total += amount;
        }

        public float Tick(float seconds)
        {
            if (seconds <= 0f) return 0f;
            float damage = 0f;
            for (int i = installments.Count - 1; i >= 0; i--)
            {
                var installment = installments[i];
                float due = installment.Remaining * Math.Min(1f, seconds / installment.Seconds);
                damage += due;
                installment.Remaining -= due;
                installment.Seconds -= seconds;
                if (installment.Seconds <= 0f || installment.Remaining <= 0f) installments.RemoveAt(i);
                else installments[i] = installment;
            }
            Total = Math.Max(0f, Total - damage);
            if (installments.Count == 0) Total = 0f;
            return damage;
        }

        /// <summary>Recovery pays the oldest damage first; returns life recovery left over.</summary>
        public float Recover(float amount)
        {
            amount = Math.Max(0f, amount);
            while (amount > 0f && installments.Count > 0)
            {
                var installment = installments[0];
                float paid = Math.Min(amount, installment.Remaining);
                amount -= paid;
                Total = Math.Max(0f, Total - paid);
                installment.Remaining -= paid;
                if (installment.Remaining <= 0f) installments.RemoveAt(0);
                else installments[0] = installment;
            }
            if (installments.Count == 0) Total = 0f;
            return amount;
        }

        public void Clear()
        {
            installments.Clear();
            Total = 0f;
        }
    }

    /// <summary>One attacker's elemental sequence on one enemy. Only successful hits record an element.</summary>
    public sealed class ElementalEquilibriumState
    {
        public const float Duration = 4f;
        public const float ResistanceShift = 25f;
        private EquilibriumElement lastElement;
        private float until = float.NegativeInfinity;

        public static bool IsElemental(EquilibriumElement type) =>
            type == EquilibriumElement.Fire || type == EquilibriumElement.Cold || type == EquilibriumElement.Lightning;

        public float Penetration(EquilibriumElement type, float now) =>
            now < until && IsElemental(type) ? (type == lastElement ? -ResistanceShift : ResistanceShift) : 0f;

        public void Record(EquilibriumElement type, float now)
        {
            if (!IsElemental(type)) return;
            lastElement = type;
            until = now + Duration;
        }
    }
}
