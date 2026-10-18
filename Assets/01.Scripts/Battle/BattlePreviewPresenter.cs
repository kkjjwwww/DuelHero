using System;
using System.Collections.Generic;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;

namespace DuelHero.Battle
{
    public sealed class TurnEnergyPreview
    {
        public int EnergyBefore { get; }
        public int EnergyAfter { get; }
        public int MaxEnergy { get; }
        public int SegmentMin { get; }
        public int SegmentMax { get; }
        public bool IsRecovery => EnergyAfter > EnergyBefore;

        public TurnEnergyPreview(int energyBefore, int energyAfter, int maxEnergy)
        {
            EnergyBefore = energyBefore;
            EnergyAfter = energyAfter;
            MaxEnergy = maxEnergy;
            SegmentMin = Math.Min(energyBefore, energyAfter);
            SegmentMax = Math.Max(energyBefore, energyAfter);
        }
    }

    // Turns pure predictions into a display request when reservations, direction, or hover changes.
    public sealed class BattlePreviewPresenter : MonoBehaviour
    {
        [SerializeField] private CardReservationQueue queue;
        [SerializeField] private GridMovement player;
        [SerializeField] private UnitStats playerStats;
        [SerializeField] private BattlePreviewRenderer renderer;
        private CardInstance hoveredCard;
        private int hoveredSlot = -1;
        private int hoveredDirectionSlot = -1;
        private Vector2Int hoveredDirection = Vector2Int.right;
        public TurnEnergyPreview EnergyPreview { get; private set; }
        public event Action<TurnEnergyPreview> EnergyPreviewChanged;
        public CardPreviewResult GuardPreview { get; private set; }
        public event Action<CardPreviewResult> GuardPreviewChanged;

        private void OnEnable()
        {
            if (queue != null) queue.Changed += Refresh;
            if (playerStats != null) playerStats.Changed += Refresh;
            Refresh();
        }
        private void Start() => Refresh();
        private void OnDisable()
        {
            if (queue != null) queue.Changed -= Refresh;
            if (playerStats != null) playerStats.Changed -= Refresh;
            renderer?.Clear();
            PublishEnergy(null);
            PublishGuard(null);
            hoveredDirectionSlot = -1;
        }
        public void FocusCard(CardInstance card) { hoveredCard = card; hoveredSlot = -1; hoveredDirectionSlot = -1; Refresh(); }
        public void ClearCard(CardInstance card) { if (ReferenceEquals(hoveredCard, card)) { hoveredCard = null; Refresh(); } }
        public void FocusSlot(int slot)
        {
            hoveredSlot = slot; hoveredCard = null;
            if (hoveredDirectionSlot != slot) hoveredDirectionSlot = -1;
            Refresh();
        }
        public void ClearSlot(int slot)
        {
            if (hoveredSlot != slot) return;
            hoveredSlot = -1; hoveredDirectionSlot = -1; Refresh();
        }
        public void FocusDirection(int slot, Vector2Int direction)
        {
            if (queue == null || queue.IsExecuting || slot < 0 || slot >= queue.Reservations.Count ||
                !CardDirectionRules.CanSelect(queue.Reservations[slot].Definition) || !CardDirectionRules.IsCardinal(direction)) return;
            hoveredCard = null; hoveredSlot = slot;
            hoveredDirectionSlot = slot; hoveredDirection = direction; Refresh();
        }
        public void ClearDirection(int slot, Vector2Int direction)
        {
            if (hoveredDirectionSlot != slot || hoveredDirection != direction) return;
            hoveredDirectionSlot = -1; Refresh();
        }
        public void Refresh()
        {
            if (renderer == null || queue == null || player == null || playerStats == null || !playerStats.IsInitialized || queue.IsExecuting)
            { renderer?.Clear(); PublishEnergy(null); PublishGuard(null); return; }
            try
            {
                var initial = new CardPreviewState(player.GridPosition, playerStats.Energy, playerStats.MaxEnergy, player.Width, player.Height);
                IReadOnlyList<CardPreviewResult> reserved = hoveredDirectionSlot >= 0 && hoveredDirectionSlot < queue.Reservations.Count
                    ? CardPreviewCalculator.CalculateQueue(initial, queue.Reservations, hoveredDirectionSlot, hoveredDirection)
                    : CardPreviewCalculator.CalculateQueue(initial, queue.Reservations);
                CardPreviewResult focus = null;
                bool hoveredCandidate = hoveredCard != null &&
                    (hoveredCard.ReservedSlot < 0 || hoveredCard.ReservedSlot >= reserved.Count);
                if (hoveredCard != null)
                {
                    focus = hoveredCandidate
                        ? CardPreviewCalculator.CalculateCandidate(initial, queue.Reservations, hoveredCard.Definition, Vector2Int.right)
                        : reserved[hoveredCard.ReservedSlot];
                }
                else if (hoveredSlot >= 0 && hoveredSlot < reserved.Count) focus = reserved[hoveredSlot];
                renderer.Show(reserved, focus);
                int finalEnergy = hoveredCandidate ? focus.EnergyAfter
                    : reserved.Count > 0 ? reserved[reserved.Count - 1].EnergyAfter : initial.Energy;
                PublishEnergy(finalEnergy != initial.Energy
                    ? new TurnEnergyPreview(initial.Energy, finalEnergy, initial.MaxEnergy)
                    : null);
                var guardResult = hoveredCandidate ? focus : reserved.Count > 0 ? reserved[reserved.Count - 1] : null;
                PublishGuard(guardResult != null && guardResult.GuardPreviewGain > 0 ? guardResult : null);
            }
            catch (ArgumentException error)
            {
                renderer.Clear();
                PublishEnergy(null);
                PublishGuard(null);
                Debug.LogWarning("카드 프리뷰를 표시할 수 없습니다: " + error.Message, this);
            }
        }
        private void PublishEnergy(TurnEnergyPreview result)
        {
            EnergyPreview = result;
            EnergyPreviewChanged?.Invoke(result);
        }
        private void PublishGuard(CardPreviewResult result)
        {
            GuardPreview = result;
            GuardPreviewChanged?.Invoke(result);
        }
    }
}
