using System;
using System.Collections.Generic;

namespace DuelHero.Units
{
    // Each application expires independently; consume the earliest-expiring shield first.
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
            effects.Sort((a, b) => a.RemainingSlots.CompareTo(b.RemainingSlots));
        }
        public int ReduceDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            foreach (var effect in effects)
            {
                int absorbed = Math.Min(amount, effect.Amount);
                effect.Amount -= absorbed;
                amount -= absorbed;
                if (amount == 0) break;
            }
            effects.RemoveAll(effect => effect.Amount == 0);
            return amount;
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
