using System;
using System.Collections.Generic;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;

namespace DuelHero.Battle
{
    // Turns pure predictions into a display request when reservations, direction, or hover changes.
    public sealed class BattlePreviewPresenter : MonoBehaviour
    {
        [SerializeField] private CardReservationQueue queue;
        [SerializeField] private GridMovement player;
        [SerializeField] private UnitStats playerStats;
        [SerializeField] private BattlePreviewRenderer renderer;
        private CardInstance hoveredCard;
        private int hoveredSlot = -1;

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
        }
        public void FocusCard(CardInstance card) { hoveredCard = card; hoveredSlot = -1; Refresh(); }
        public void ClearCard(CardInstance card) { if (ReferenceEquals(hoveredCard, card)) { hoveredCard = null; Refresh(); } }
        public void FocusSlot(int slot) { hoveredSlot = slot; hoveredCard = null; Refresh(); }
        public void ClearSlot(int slot) { if (hoveredSlot == slot) { hoveredSlot = -1; Refresh(); } }
        public void Refresh()
        {
            if (renderer == null || queue == null || player == null || playerStats == null || !playerStats.IsInitialized || queue.IsExecuting)
            { renderer?.Clear(); return; }
            try
            {
                var initial = new CardPreviewState(player.GridPosition, playerStats.Energy, playerStats.MaxEnergy, player.Width, player.Height);
                IReadOnlyList<CardPreviewResult> reserved = CardPreviewCalculator.CalculateQueue(initial, queue.Reservations);
                CardPreviewResult focus = null;
                if (hoveredCard != null)
                {
                    focus = hoveredCard.ReservedSlot >= 0 && hoveredCard.ReservedSlot < reserved.Count
                        ? reserved[hoveredCard.ReservedSlot]
                        : CardPreviewCalculator.CalculateCandidate(initial, queue.Reservations, hoveredCard.Definition, Vector2Int.right);
                }
                else if (hoveredSlot >= 0 && hoveredSlot < reserved.Count) focus = reserved[hoveredSlot];
                renderer.Show(reserved, focus);
            }
            catch (ArgumentException error)
            {
                renderer.Clear();
                Debug.LogWarning("카드 프리뷰를 표시할 수 없습니다: " + error.Message, this);
            }
        }
    }
}
