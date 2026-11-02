using System;
using System.Collections.Generic;
using UnityEngine;
namespace DuelHero.Cards
{
    public sealed class CardReservationQueue : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private DuelHero.Units.UnitStats unitStats;
        public DuelHero.Units.UnitStats UnitStats => unitStats;
        private void OnEnable() { if (unitStats != null) unitStats.Changed += OnEnergyChanged; }
        private void OnDisable() { if (unitStats != null) unitStats.Changed -= OnEnergyChanged; }
        private void OnEnergyChanged()
        {
            if (!IsExecuting) SynchronizeStunSlots();
            Changed?.Invoke();
            playerDeck?.NotifyStateChanged();
        }
        public bool HasEnoughEnergy(out string reason)
        {
            if (unitStats == null || !unitStats.IsInitialized) { reason = "유닛 에너지가 아직 초기화되지 않았습니다."; return false; }
            return CardEnergyRules.CanExecute(Reservations, unitStats.Energy, unitStats.MaxEnergy, out reason);
        }
        private readonly List<CardInstance> reservations = new();
        private IReadOnlyList<CardInstance> readOnlyReservations;
        public IReadOnlyList<CardInstance> Reservations => readOnlyReservations ??= reservations.AsReadOnly();
        public int CurrentRound { get; private set; } = 1;
        public bool IsExecuting { get; private set; }
        public int ActiveSlot { get; private set; } = -1;
        public event Action Changed;
        public bool TryReserve(CardInstance card)
        {
            if (!IsExecuting) SynchronizeStunSlots();
            if (IsExecuting || card == null || playerDeck == null || reservations.Count >= 3) return false;
            bool systemWait = ReferenceEquals(card, playerDeck.WaitAction);
            bool owned = systemWait;
            foreach (var instance in playerDeck.Cards) if (ReferenceEquals(instance, card)) { owned = true; break; }
            if (!owned || !card.CanReserve(CurrentRound)) return false;
            // Keep the hand's wait action available; each slot owns its reservation state.
            if (systemWait) card = new CardInstance(WaitCard.Definition);
            reservations.Add(card);
            if (!HasEnoughEnergy(out _) || !card.TryReserve(reservations.Count - 1, CurrentRound))
            {
                reservations.RemoveAt(reservations.Count - 1);
                return false;
            }
            Notify(); return true;
        }
        public bool CancelAt(int slot)
        {
            if (IsExecuting || slot < 0 || slot >= reservations.Count || StunCard.IsStun(reservations[slot].Definition)) return false;
            reservations[slot].ClearReservation(); reservations.RemoveAt(slot);
            for (int i = slot; i < reservations.Count; i++)
            {
                reservations[i].RelocateReservation(i);
            }
            SynchronizeStunSlots();
            Notify(); return true;
        }
        private void SynchronizeStunSlots()
        {
            int blocked = Math.Min(3, unitStats != null ? unitStats.Stun : 0);
            int current = 0;
            while (current < reservations.Count && StunCard.IsStun(reservations[current].Definition)) current++;
            if (current == blocked) return;
            if (blocked > current)
            {
                for (int slot = current; slot < blocked; slot++)
                {
                    var marker = new CardInstance(StunCard.Definition);
                    if (slot < reservations.Count) { reservations[slot].ClearReservation(); reservations[slot] = marker; }
                    else reservations.Add(marker);
                }
            }
            else
            {
                reservations.RemoveRange(blocked, current - blocked);
                for (int slot = blocked; slot < reservations.Count; slot++) reservations[slot].RelocateReservation(slot);
            }
        }
        public void BlockFutureSlots(int firstSlot, int count)
        {
            if (!IsExecuting) return;
            for (int slot = firstSlot; slot < Math.Min(3, firstSlot + count); slot++)
            {
                if (slot >= reservations.Count || StunCard.IsStun(reservations[slot].Definition)) continue;
                reservations[slot].ClearReservation();
                reservations[slot] = new CardInstance(StunCard.Definition);
            }
            Notify();
        }
        public bool BeginExecution()
        {
            if (IsExecuting || reservations.Count != 3 || !HasEnoughEnergy(out _)) return false;
            IsExecuting = true; Notify(); return true;
        }
        public bool TrySetDirection(int slot, Vector2Int direction)
        {
            if (IsExecuting || slot < 0 || slot >= reservations.Count || StunCard.IsStun(reservations[slot].Definition) || !reservations[slot].SetDirection(direction)) return false;
            Notify(); return true;
        }
        public void SetActiveSlot(int slot) { ActiveSlot = slot; Notify(); }
        public void CompleteExecution(int executedSlots = 3)
        {
            if (!IsExecuting) return;
            for (int i = 0; i < reservations.Count; i++)
                if (i < executedSlots && !StunCard.IsStun(reservations[i].Definition)) reservations[i].MarkUsed(CurrentRound); else reservations[i].ClearReservation();
            reservations.Clear(); CurrentRound++; IsExecuting = false; ActiveSlot = -1;
            SynchronizeStunSlots(); Notify();
        }
        public void AbortExecution()
        {
            IsExecuting = false; ActiveSlot = -1; Notify();
        }
        private void Notify() { Changed?.Invoke(); playerDeck.NotifyStateChanged(); }
        private void OnDestroy() { foreach (var card in reservations) card.ClearReservation(); }
    }
}
