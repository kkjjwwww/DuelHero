using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Battle
{
    public readonly struct EnemyActionSimulation
    {
        public int Slot { get; }
        public EnemyPlannedAction Action { get; }
        public Vector2Int PositionBefore { get; }
        public Vector2Int PositionAfter { get; }
        public int EnergyBefore { get; }
        public int EnergyAfterCost { get; }
        public int EnergyAfter { get; }
        public int RawHitDamage { get; }
        public int AbsorbedDamage { get; }
        public int EffectiveDamage { get; }
        public int TargetHealthBefore { get; }
        public int TargetHealthAfter { get; }
        public int GuardBefore { get; }
        public int GuardDuringSlot { get; }
        public int GuardAfterSlot { get; }
        public int TargetGuardBefore { get; }
        public int TargetGuardAfterSlot { get; }

        internal EnemyActionSimulation(int slot, EnemyPlannedAction action, Vector2Int before, Vector2Int after,
            int energyBefore, int afterCost, int energyAfter, int raw, int absorbed, int effective,
            int healthBefore, int healthAfter, int guardBefore, int guardDuring, int guardAfter,
            int targetGuardBefore, int targetGuardAfter)
        {
            Slot = slot; Action = action; PositionBefore = before; PositionAfter = after;
            EnergyBefore = energyBefore; EnergyAfterCost = afterCost; EnergyAfter = energyAfter;
            RawHitDamage = raw; AbsorbedDamage = absorbed; EffectiveDamage = effective;
            TargetHealthBefore = healthBefore; TargetHealthAfter = healthAfter;
            GuardBefore = guardBefore; GuardDuringSlot = guardDuring; GuardAfterSlot = guardAfter;
            TargetGuardBefore = targetGuardBefore; TargetGuardAfterSlot = targetGuardAfter;
        }
    }

    public sealed class EnemyPlanSimulation
    {
        public IReadOnlyList<EnemyActionSimulation> Slots { get; }
        public int ExecutedSlots => Slots.Count;
        public int TotalEffectiveDamage { get; }
        public int TargetHealthAfter { get; }
        public Vector2Int PositionAfter { get; }
        public int EnergyAfter { get; }

        internal EnemyPlanSimulation(List<EnemyActionSimulation> slots, int totalDamage, int targetHealth,
            Vector2Int position, int energy)
        {
            Slots = Array.AsReadOnly(slots.ToArray());
            TotalEffectiveDamage = totalDamage; TargetHealthAfter = targetHealth;
            PositionAfter = position; EnergyAfter = energy;
        }
    }
}
