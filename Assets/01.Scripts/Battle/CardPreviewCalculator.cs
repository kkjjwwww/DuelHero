using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    public enum CardPreviewStatus { Ready, InsufficientEnergy, BlockedByEarlierCard }

    public readonly struct CardPreviewState
    {
        public Vector2Int Position { get; }
        public int Energy { get; }
        public int MaxEnergy { get; }
        public int Width { get; }
        public int Height { get; }
        public CardPreviewState(Vector2Int position, int energy, int maxEnergy, int width, int height)
        {
            if (width <= 0 || height <= 0 || !GridRules.Contains(position, width, height) || energy < 0 || maxEnergy < energy)
                throw new ArgumentException("프리뷰의 위치·전장 크기·에너지 상태를 확인해주세요.");
            Position = position; Energy = energy; MaxEnergy = maxEnergy; Width = width; Height = height;
        }
    }

    public readonly struct CardPreviewRequest
    {
        public CardDefinition Card { get; }
        public Vector2Int Direction { get; }
        public CardPreviewRequest(CardDefinition card, Vector2Int direction)
        { Card = card ?? throw new ArgumentNullException(nameof(card)); Direction = direction; }
    }

    public sealed class CardPreviewResult
    {
        public string CardId { get; }
        public int Slot { get; }
        public CardPreviewStatus Status { get; }
        public Vector2Int StartPosition { get; }
        public Vector2Int EndPosition { get; }
        public IReadOnlyList<Vector2Int> MovementPath { get; }
        public IReadOnlyList<Vector2Int> AttackCells { get; }
        public int EnergyBefore { get; }
        public int EnergyAfterCost { get; }
        public int EnergyAfter { get; }
        public int EnergyCost { get; }
        public int MaxEnergy { get; }
        public bool CanExecute => Status == CardPreviewStatus.Ready;
        // Absolute values on the energy bar, before recovery from this card.
        public int CostSegmentMin => Math.Max(0, EnergyBefore - EnergyCost);
        public int CostSegmentMax => EnergyBefore;
        internal CardPreviewResult(string cardId, int slot, CardPreviewStatus status, Vector2Int start, Vector2Int end,
            List<Vector2Int> path, HashSet<Vector2Int> attacks, int before, int afterCost, int after, int cost, int maximum)
        {
            CardId = cardId; Slot = slot; Status = status; StartPosition = start; EndPosition = end;
            MovementPath = path.AsReadOnly(); AttackCells = Array.AsReadOnly(attacks.OrderBy(c => c.y).ThenBy(c => c.x).ToArray());
            EnergyBefore = before; EnergyAfterCost = afterCost; EnergyAfter = after; EnergyCost = cost; MaxEnergy = maximum;
        }
    }

    // Pure simulation of player actions. Does not predict enemies or mutate live units/reservations.
    public static class CardPreviewCalculator
    {
        public static IReadOnlyList<CardPreviewResult> CalculateQueue(CardPreviewState initial, IReadOnlyList<CardInstance> reservations) =>
            Calculate(initial, reservations.Select(c => new CardPreviewRequest(c.Definition, c.SelectedDirection)).ToArray());

        public static CardPreviewResult CalculateCandidate(CardPreviewState initial, IReadOnlyList<CardInstance> reservations,
            CardDefinition candidate, Vector2Int direction)
        {
            var requests = reservations.Select(c => new CardPreviewRequest(c.Definition, c.SelectedDirection)).ToList();
            requests.Add(new CardPreviewRequest(candidate, direction));
            return Calculate(initial, requests)[requests.Count - 1];
        }

        public static IReadOnlyList<CardPreviewResult> Calculate(CardPreviewState initial, IReadOnlyList<CardPreviewRequest> requests)
        {
            var results = new List<CardPreviewResult>();
            var position = initial.Position;
            int energy = initial.Energy;
            bool blocked = false;
            for (int slot = 0; slot < requests.Count; slot++)
            {
                var request = requests[slot];
                Validate(request.Card);
                var card = CardDirectionRules.ForExecution(request.Card, request.Direction);
                var start = position;
                int before = energy, afterCost = energy;
                var path = new List<Vector2Int> { start };
                var attacks = new HashSet<Vector2Int>();
                var status = blocked ? CardPreviewStatus.BlockedByEarlierCard : CardPreviewStatus.Ready;
                if (!blocked && !CardEnergyRules.TryPayCost(energy, card.energyCost, out afterCost))
                { status = CardPreviewStatus.InsufficientEnergy; blocked = true; }
                if (status == CardPreviewStatus.Ready)
                {
                    energy = afterCost;
                    // Matches BattleTurnController: all preparation, then damage from the final position.
                    foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
                    {
                        if (effect.effectType == "move")
                        {
                            CardEffectExecutor.TryDirection(effect.rangeOffsets, out var direction);
                            for (int step = 0; step < effect.value; step++)
                            {
                                var next = GridRules.Move(position, direction, 1, initial.Width, initial.Height);
                                if (next == position) break;
                                position = next; path.Add(position);
                            }
                        }
                        else if (effect.effectType == "energyRestore")
                            energy = CardEnergyRules.Restore(energy, effect.value, initial.MaxEnergy);
                    }
                    foreach (var effect in card.effects.Where(e => e.effectType == "damage"))
                        foreach (var cell in AttackRangeCalculator.Calculate(position, effect.rangeOffsets))
                            if (GridRules.Contains(cell, initial.Width, initial.Height)) attacks.Add(cell);
                }
                results.Add(new CardPreviewResult(card.id, slot, status, start, position, path, attacks,
                    before, afterCost, energy, card.energyCost, initial.MaxEnergy));
            }
            return results.AsReadOnly();
        }
        private static void Validate(CardDefinition card)
        {
            if (card == null || !CardEffectExecutor.SupportsDefinition(card))
                throw new ArgumentException("프리뷰가 지원하지 않는 카드 데이터 또는 효과입니다.");
        }
    }
}
