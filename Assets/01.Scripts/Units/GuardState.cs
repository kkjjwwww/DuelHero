using System;
using System.Collections.Generic;

namespace DuelHero.Units
{
    public readonly struct GuardEffectSnapshot
    {
        public int Amount { get; }
        public int RemainingSlots { get; }
        public GuardEffectSnapshot(int amount, int remainingSlots)
        { Amount = amount; RemainingSlots = remainingSlots; }
    }

    // Each application expires independently; consume the earliest-expiring shield first.
    public sealed class GuardState
    {
        private struct Effect
        {
            public int Amount;
            public int RemainingSlots;
        }
        private readonly List<Effect> effects = new();
        public GuardState() { }
        // Planning buffers retain their capacity; branches never share mutable shield layers.
        internal void CopyFrom(GuardState source)
        {
            effects.Clear();
            effects.AddRange(source.effects);
        }
        internal void Reset(IReadOnlyList<GuardEffectSnapshot> snapshot)
        {
            effects.Clear();
            for (int i = 0; i < snapshot.Count; i++) Apply(snapshot[i].Amount, snapshot[i].RemainingSlots);
        }
        public IReadOnlyList<GuardEffectSnapshot> Capture()
        {
            var snapshot = new GuardEffectSnapshot[effects.Count];
            for (int i = 0; i < effects.Count; i++)
                snapshot[i] = new GuardEffectSnapshot(effects[i].Amount, effects[i].RemainingSlots);
            return Array.AsReadOnly(snapshot);
        }
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
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                int absorbed = Math.Min(amount, effect.Amount);
                effect.Amount -= absorbed;
                effects[i] = effect;
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
            {
                var effect = effects[i];
                if (--effect.RemainingSlots == 0) effects.RemoveAt(i);
                else effects[i] = effect;
            }
            return changed;
        }
    }
}
