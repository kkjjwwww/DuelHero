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
                target.stats.TakeDamage(effect.value);
                ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.Damage, actorId, round, cardId,
                    from: actor.movement.GridPosition, to: target.movement.GridPosition,
                    value: before - target.stats.Health, targetId: target.id));
            }
            if (hit.Count == 0)
                ActionResolved?.Invoke(new BattleActionResult(BattleActionKind.Damage, actorId, round, cardId, BattleActionOutcome.NoTarget));
        }
    }
}
