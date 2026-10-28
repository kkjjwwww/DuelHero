using System.Collections.Generic;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    public readonly struct EnemyActionScore
    {
        public float Damage { get; }
        public float Opportunity { get; }
        public float Energy { get; }
        public float CostPenalty { get; }
        public float Total => Damage + Opportunity + Energy - CostPenalty;
        internal EnemyActionScore(float damage, float opportunity, float energy, float costPenalty)
        { Damage = damage; Opportunity = opportunity; Energy = energy; CostPenalty = costPenalty; }
    }

    // Latest decision only. Created only when explicitly recording; stores values rather
    // than mutable card instances so cooldowns and subsequent rounds cannot change a row.
    public sealed class EnemyDecisionTrace
    {
        public readonly struct Candidate
        {
            public int CardIndex { get; }
            public string CardId { get; }
            public string CardName { get; }
            public Vector2Int Direction { get; }
            public Vector2Int PositionAfter { get; }
            public int EnergyAfter { get; }
            public int EffectiveDamage { get; }
            public EnemyActionScore Score { get; }
            internal Candidate(int index, CardInstance card, Vector2Int direction,
                EnemyPlanningState before, EnemyPlanningState after, EnemyActionScore score)
            {
                CardIndex = index; CardId = card.Definition.id; CardName = card.Definition.name;
                Direction = direction; PositionAfter = after.Position; EnergyAfter = after.Energy;
                EffectiveDamage = after.TotalEffectiveDamage - before.TotalEffectiveDamage; Score = score;
            }
        }

        public sealed class Slot
        {
            private readonly List<Candidate> candidates = new List<Candidate>();
            private readonly List<int> eligibleIndices = new List<int>(3);
            public int Index { get; }
            public Vector2Int PositionBefore { get; }
            public int EnergyBefore { get; }
            public IReadOnlyList<Candidate> Candidates { get; }
            public IReadOnlyList<int> EligibleIndices { get; }
            public float BestScore { get; internal set; }
            public float MinimumScore { get; internal set; }
            public int SelectedIndex { get; internal set; } = -1;
            public string Note { get; internal set; }
            internal Slot(int index, EnemyPlanningState state)
            {
                Index = index; PositionBefore = state.Position; EnergyBefore = state.Energy;
                Candidates = candidates.AsReadOnly();
                EligibleIndices = eligibleIndices.AsReadOnly();
            }
            internal void Add(int index, CardInstance card, Vector2Int direction,
                EnemyPlanningState before, EnemyPlanningState after, EnemyActionScore score) =>
                candidates.Add(new Candidate(index, card, direction, before, after, score));
            internal void AddEligible(int index) => eligibleIndices.Add(index);
        }

        private readonly List<Slot> slots = new List<Slot>(3);
        public int Round { get; }
        public uint DecisionSeed { get; }
        public IReadOnlyList<Slot> Slots { get; }
        internal EnemyDecisionTrace(int round, uint seed) { Round = round; DecisionSeed = seed; Slots = slots.AsReadOnly(); }
        internal Slot BeginSlot(int index, EnemyPlanningState state)
        {
            var slot = new Slot(index, state); slots.Add(slot); return slot;
        }
    }
}
