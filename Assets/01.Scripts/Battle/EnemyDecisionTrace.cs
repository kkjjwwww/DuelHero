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
        public float RiskPenalty { get; }
        public float IneffectivePenalty { get; }
        public int RiskDamage { get; }
        public int BlockedDamage { get; }
        public float MovementPenalty { get; }
        public bool Revisited { get; }
        public bool UnproductiveMove { get; }
        public float Total => Damage + Opportunity + Energy - CostPenalty - RiskPenalty - IneffectivePenalty - MovementPenalty;
        internal EnemyActionScore(float damage, float opportunity, float energy, float costPenalty,
            float riskPenalty = 0, float ineffectivePenalty = 0, int riskDamage = 0, int blockedDamage = 0,
            float movementPenalty = 0, bool revisited = false, bool unproductiveMove = false)
        {
            Damage = damage; Opportunity = opportunity; Energy = energy; CostPenalty = costPenalty;
            RiskPenalty = riskPenalty; IneffectivePenalty = ineffectivePenalty;
            RiskDamage = riskDamage; BlockedDamage = blockedDamage;
            MovementPenalty = movementPenalty; Revisited = revisited; UnproductiveMove = unproductiveMove;
        }
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
            private readonly List<int> eligibleIndices = new List<int>(2);
            private readonly List<double> probabilities = new List<double>(2);
            public int Index { get; }
            public Vector2Int PositionBefore { get; }
            public int EnergyBefore { get; }
            public IReadOnlyList<Candidate> Candidates { get; }
            public IReadOnlyList<int> EligibleIndices { get; }
            public float BestScore { get; internal set; }
            public float Temperature { get; internal set; }
            public IReadOnlyList<double> Probabilities { get; }
            public int SelectedIndex { get; internal set; } = -1;
            public string Note { get; internal set; }
            internal Slot(int index, EnemyPlanningState state)
            {
                Index = index; PositionBefore = state.Position; EnergyBefore = state.Energy;
                Candidates = candidates.AsReadOnly();
                EligibleIndices = eligibleIndices.AsReadOnly();
                Probabilities = probabilities.AsReadOnly();
            }
            internal void Add(int index, CardInstance card, Vector2Int direction,
                EnemyPlanningState before, EnemyPlanningState after, EnemyActionScore score) =>
                candidates.Add(new Candidate(index, card, direction, before, after, score));
            internal void AddEligible(int index, double probability)
            { eligibleIndices.Add(index); probabilities.Add(probability); }
        }

        private readonly List<Slot> slots = new List<Slot>(3);
        public int Round { get; }
        public uint DecisionSeed { get; }
        public EnemyRoundStyle Style { get; }
        public IReadOnlyList<Slot> Slots { get; }
        internal EnemyDecisionTrace(int round, uint seed, EnemyRoundStyle style)
        { Round = round; DecisionSeed = seed; Style = style; Slots = slots.AsReadOnly(); }
        internal Slot BeginSlot(int index, EnemyPlanningState state)
        {
            var slot = new Slot(index, state); slots.Add(slot); return slot;
        }
    }
}
