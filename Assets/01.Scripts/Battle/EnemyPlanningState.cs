using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class EnemyPlanningState
    {
        public Vector2Int Position { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }
        public GridMap Map { get; private set; }
        internal int TargetHealth { get; private set; }
        internal int TotalEffectiveDamage { get; private set; }
        private GuardState actorGuard, targetGuard;
        internal EnemyPlanningState() { }
        public EnemyPlanningState(Vector2Int position, int energy, int maxEnergy, GridMap map)
        { Position = position; Energy = energy; MaxEnergy = maxEnergy; Map = map; }
        public EnemyPlanningState Predict(CardDefinition card, Vector2Int direction)
            => Predict(new EnemyPreparedAction(card, direction));
        public EnemyPlanningState Predict(EnemyPreparedAction action)
        {
            var next = new EnemyPlanningState(Position, Energy, MaxEnergy, Map);
            next.Apply(action); return next;
        }
        public void Apply(CardDefinition card, Vector2Int direction)
            => Apply(new EnemyPreparedAction(card, direction));
        public void Apply(EnemyPreparedAction action)
        {
            if (action.EnergyCost < 0 || action.EnergyCost > Energy) throw new InvalidOperationException("적 AI 카드 비용이 부족합니다.");
            Energy -= action.EnergyCost;
            Position = action.MoveFrom(Position, Map);
            for (int i = 0; i < action.Effects.Length; i++)
            {
                var effect = action.Effects[i];
                if (effect.effectType == "energyRestore") Energy += Math.Min(effect.value, MaxEnergy - Energy);
            }
        }

        internal void Reset(EnemyDecisionContext context)
        {
            Position = context.State.Position; Energy = context.State.Energy;
            MaxEnergy = context.State.MaxEnergy; Map = context.State.Map;
            TargetHealth = context.TargetUnit.Health; TotalEffectiveDamage = 0;
            actorGuard ??= new GuardState(); targetGuard ??= new GuardState();
            actorGuard.Reset(context.Actor.GuardEffects); targetGuard.Reset(context.TargetUnit.GuardEffects);
        }

        internal void CopyValuesFrom(EnemyPlanningState source)
        {
            Position = source.Position; Energy = source.Energy; MaxEnergy = source.MaxEnergy; Map = source.Map;
            TargetHealth = source.TargetHealth; TotalEffectiveDamage = source.TotalEffectiveDamage;
        }

        // Value-only candidate preview. With the currently supported effects, all damage targets
        // the same stationary unit after preparation. Its aggregate shield gives the exact
        // current-slot HP loss; only the chosen action advances the shield layers.
        internal int PreviewAction(EnemyDecisionContext context, EnemyPreparedAction action, EnemyPlanningState source)
        {
            Apply(action);
            int raw = action.RawDamage(Position, context.Target);
            int damage = Math.Min(TargetHealth, Math.Max(0, raw - source.targetGuard.Reduction));
            TargetHealth -= damage; TotalEffectiveDamage += damage;
            return raw;
        }

        // One shared transition for search and the detailed final-plan report. The result is
        // a value type: search does not allocate a per-slot record or mutate the input context.
        internal EnemyActionSimulation Step(EnemyDecisionContext context, EnemyPlannedAction action, int slot)
        {
            var prepared = context.GetPreparedAction(action);
            var positionBefore = Position;
            int energyBefore = Energy, guardBefore = actorGuard.Reduction;
            int healthBefore = TargetHealth, targetGuardBefore = targetGuard.Reduction;
            Apply(prepared);
            for (int i = 0; i < prepared.Effects.Length; i++)
            {
                var effect = prepared.Effects[i];
                if (effect.effectType == "guard") actorGuard.Apply(effect.value, effect.durationSlots);
            }
            int raw = 0, absorbed = 0, effective = 0;
            for (int i = 0; i < prepared.Effects.Length; i++)
            {
                var effect = prepared.Effects[i];
                if (effect.effectType != "damage" || !prepared.Hits(effect, Position, context.Target)) continue;
                raw += effect.value;
                int damage = targetGuard.ReduceDamage(effect.value);
                absorbed += effect.value - damage;
                int dealt = Math.Min(TargetHealth, damage);
                effective += dealt; TargetHealth -= dealt;
            }
            int guardDuring = actorGuard.Reduction;
            actorGuard.EndSlot(); targetGuard.EndSlot(); TotalEffectiveDamage += effective;
            return new EnemyActionSimulation(slot, action, positionBefore, Position,
                energyBefore, energyBefore - prepared.EnergyCost, Energy,
                raw, absorbed, effective, healthBefore, TargetHealth, guardBefore, guardDuring, actorGuard.Reduction,
                targetGuardBefore, targetGuard.Reduction);
        }

        // Baseline model: the target stays at its public position and takes no new actions.
        // No incoming damage or player reservations are predicted in stage 1.
        public static EnemyPlanSimulation SimulatePlan(EnemyDecisionContext context, IReadOnlyList<EnemyPlannedAction> plan)
        {
            if (plan.Count != 3) throw new ArgumentException("적 계획은 세 슬롯이어야 합니다.", nameof(plan));
            for (int i = 0; i < plan.Count; i++)
            {
                var action = plan[i];
                if (action.Card == null || (!WaitCard.IsWait(action.Card.Definition) &&
                    (!context.Cards.Contains(action.Card) || !action.Card.CanReserve(context.Round))))
                    throw new ArgumentException("계획의 카드 소유·중복·쿨다운을 확인해주세요.", nameof(plan));
                for (int previous = 0; previous < i; previous++)
                    if (!WaitCard.IsWait(action.Card.Definition) && plan[previous].Card == action.Card)
                        throw new ArgumentException("같은 카드 인스턴스를 중복 사용할 수 없습니다.", nameof(plan));
            }
            var state = new EnemyPlanningState();
            state.Reset(context);
            var slots = new List<EnemyActionSimulation>(3);
            for (int slot = 0; slot < plan.Count && context.Actor.Health > 0 && state.TargetHealth > 0; slot++)
                slots.Add(state.Step(context, plan[slot], slot));
            return new EnemyPlanSimulation(slots, state.TotalEffectiveDamage, state.TargetHealth, state.Position, state.Energy);
        }

    }
}

