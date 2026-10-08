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
        public bool TryGetUnit(string actorId, out GridMovement movement, out UnitStats stats)
        {
            var unit = units.FirstOrDefault(u => u != null && u.id == actorId);
            movement = unit?.movement; stats = unit?.stats;
            return movement != null && stats != null && stats.IsInitialized;
        }
        public sealed class AttackRequest
        {
            public readonly string ActorId, CardId;
            public readonly CardEffectDefinition Effect;
            public readonly bool MirrorX;
            public AttackRequest(string actorId, string cardId, CardEffectDefinition effect, bool mirrorX = false)
            { ActorId = actorId; CardId = cardId; Effect = effect; MirrorX = mirrorX; }
        }
        public sealed class DamageBatch
        {
            internal readonly List<PendingDamage> Hits = new();
            internal readonly List<BattleActionResult> Misses = new();
            internal bool Applied;
        }
        internal sealed class PendingDamage
        {
            internal UnitStats Target;
            internal int Amount, Reduced, Round;
            internal string ActorId, CardId, TargetId;
            internal Vector2Int From, To;
        }
        public static int DamageAfterGuard(int damage, int guard)
        {
            if (damage < 0 || guard < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            return Math.Max(0, damage - guard);
        }
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
            ApplyDamageBatch(CalculateDamageBatch(new[] { new AttackRequest(actorId, cardId, effect) }, round));
        }
        public DamageBatch CalculateDamageBatch(IEnumerable<AttackRequest> attacks, int round)
        {
            var batch = new DamageBatch();
            foreach (var attack in attacks)
            {
                if (!CanResolve(attack.ActorId)) throw new InvalidOperationException("공격 유닛 참조 또는 초기화 상태를 확인해주세요.");
                var effect = attack.Effect;
                if (effect == null || effect.effectType != "damage" || effect.targetType != "적" || effect.value < 0 || effect.rangeOffsets == null)
                    throw new ArgumentException("지원하지 않는 공격 효과입니다.");
                var actor = units.Single(u => u.id == attack.ActorId);
                if (actor.stats.Health <= 0 || !actor.stats.gameObject.activeInHierarchy) continue;
                var offsets = attack.MirrorX ? effect.rangeOffsets.Select(p => new Vector2Int(-p.x, p.y)).ToArray() : effect.rangeOffsets;
                var range = AttackRangeCalculator.Calculate(actor.movement.GridPosition, offsets);
                var hit = new HashSet<UnitStats>();
                foreach (var target in units)
                {
                    if (target.side == actor.side || target.stats == actor.stats || !target.stats.gameObject.activeInHierarchy ||
                        target.stats.Health <= 0 || !range.Contains(target.movement.GridPosition) || !hit.Add(target.stats)) continue;
                    int amount = DamageAfterGuard(effect.value, target.stats.GuardReduction);
                    batch.Hits.Add(new PendingDamage { Target = target.stats, Amount = amount, Reduced = effect.value - amount,
                        ActorId = attack.ActorId, CardId = attack.CardId, TargetId = target.id, Round = round,
                        From = actor.movement.GridPosition, To = target.movement.GridPosition });
                }
                if (hit.Count == 0) batch.Misses.Add(new BattleActionResult(BattleActionKind.Damage, attack.ActorId, round, attack.CardId, BattleActionOutcome.NoTarget));
            }
            return batch;
        }
        public void ApplyDamageBatch(DamageBatch batch)
        {
            if (batch == null || batch.Applied) throw new InvalidOperationException("피해 결과는 한 번만 적용할 수 있습니다.");
            batch.Applied = true;
            var results = new List<BattleActionResult>();
            foreach (var hit in batch.Hits)
            {
                int before = hit.Target.Health;
                hit.Target.ApplyResolvedDamage(hit.Amount);
                results.Add(new BattleActionResult(BattleActionKind.Damage, hit.ActorId, hit.Round, hit.CardId,
                    from: hit.From, to: hit.To, value: before - hit.Target.Health, targetId: hit.TargetId, reducedDamage: hit.Reduced));
            }
            foreach (var result in results.Concat(batch.Misses)) ActionResolved?.Invoke(result);
        }
    }
}
