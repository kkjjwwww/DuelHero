using System;
using System.Collections.Generic;

namespace DuelHero.Units
{
    // Each application expires independently; taking damage does not consume guard.
    public sealed class GuardState
    {
        private sealed class Effect
        {
            public int Amount;
            public int RemainingSlots;
        }
        private readonly List<Effect> effects = new();
        public int Reduction
        {
            get
            {
                long total = 0;
                foreach (var effect in effects) total += effect.Amount;
                return (int)Math.Min(int.MaxValue, total);
            }
        }
        public void Apply(int amount, int durationSlots)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (durationSlots <= 0) throw new ArgumentOutOfRangeException(nameof(durationSlots));
            effects.Add(new Effect { Amount = amount, RemainingSlots = durationSlots });
        }
        public int ReduceDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return Math.Max(0, amount - Reduction);
        }
        public bool EndSlot()
        {
            bool changed = effects.Count > 0;
            for (int i = effects.Count - 1; i >= 0; i--)
                if (--effects[i].RemainingSlots == 0) effects.RemoveAt(i);
            return changed;
        }
    }
}
