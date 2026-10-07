using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class CombatEffectResolver : MonoBehaviour, IBattleActionSource
    {
        [Serializable]
        private sealed class UnitBinding
        {
            public string id;
            public UnitDataKind side;
            public GridMovement movement;
            public UnitStats stats;
        }
        [SerializeField] private UnitBinding[] units = Array.Empty<UnitBinding>();
        public event Action<BattleActionResult> ActionResolved;
        public void ResolveGuard(string actorId, string cardId, CardEffectDefinition effect, int round)
        {
            if (!CanResolve(actorId)) throw new InvalidOperationException("방어 유닛 참조 또는 초기화 상태를 확인해주세요.");
            if (effect == null || effect.effectType != "guard" || effect.targetType != "자신" || effect.value < 0 || effect.durationSlots <= 0)
                throw new ArgumentException("지원하지 않는 방어 효과입니다.");
            var actor = units.Single(u => u.id == actorId);
            actor.stats.ApplyGuard(effect.value, effect.durationSlots);
            ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.GuardApplied, actorId, round, cardId,
                value: effect.value, targetId: actorId, durationSlots: effect.durationSlots));
        }
        // Call once after ALL units' effects/attacks for the shared slot have resolved.
        // Do not call separately for each actor when enemy execution is added.
        public void EndSlot(int round)
        {
            var seen = new HashSet<UnitStats>();
            foreach (var unit in units)
            {
                if (unit == null || unit.stats == null || !unit.stats.IsInitialized || !seen.Add(unit.stats)) continue;
                int before = unit.stats.GuardReduction;
                unit.stats.EndSlot();
                if (before > unit.stats.GuardReduction)
                    ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.GuardExpired, unit.id, round,
                        value: before - unit.stats.GuardReduction, targetId: unit.id));
            }
        }
        public bool CanResolve(string actorId)
        {
            return isActiveAndEnabled && units.Count(u => u != null && u.id == actorId) == 1 &&
                units.All(u => u != null && !string.IsNullOrEmpty(u.id) && u.movement != null && u.stats != null && u.stats.IsInitialized);
        }
        public void ResolveDamage(string actorId, string cardId, CardEffectDefinition effect, int round)
        {
            if (!CanResolve(actorId)) throw new InvalidOperationException("공격 유닛 참조 또는 초기화 상태를 확인해주세요.");
            if (effect.effectType != "damage" || effect.targetType != "적" || effect.value < 0) throw new ArgumentException("지원하지 않는 공격 효과입니다.");
            var actor = units.Single(u => u.id == actorId);
            var range = AttackRangeCalculator.Calculate(actor.movement.GridPosition, effect.rangeOffsets);
            var hit = new HashSet<UnitStats>();
            foreach (var target in units)
            {
                if (target.side == actor.side || target.stats == actor.stats || !target.stats.gameObject.activeInHierarchy ||
                    target.stats.Health <= 0 || !range.Contains(target.movement.GridPosition) || !hit.Add(target.stats)) continue;
                int before = target.stats.Health;
                int reduced = Math.Min(effect.value, target.stats.GuardReduction);
                target.stats.TakeDamage(effect.value);
                ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.Damage, actorId, round, cardId,
                    from: actor.movement.GridPosition, to: target.movement.GridPosition,
                    value: before - target.stats.Health, targetId: target.id, reducedDamage: reduced));
            }
            if (hit.Count == 0)
                ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.Damage, actorId, round, cardId, BattleActionOutcome.NoTarget));
        }
    }
}
