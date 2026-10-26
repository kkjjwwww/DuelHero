using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Units;

namespace DuelHero.Battle
{
    // Public values only; never retains a live UnitStats or its mutable shield state.
    public sealed class EnemyUnitSnapshot
    {
        public int Health { get; }
        public int MaxHealth { get; }
        public int Energy { get; }
        public int MaxEnergy { get; }
        public IReadOnlyList<GuardEffectSnapshot> GuardEffects { get; }

        public EnemyUnitSnapshot(int health, int maxHealth, int energy, int maxEnergy,
            IEnumerable<GuardEffectSnapshot> guardEffects)
        {
            Health = health; MaxHealth = maxHealth; Energy = energy; MaxEnergy = maxEnergy;
            GuardEffects = Array.AsReadOnly(guardEffects.ToArray());
        }
    }
}
