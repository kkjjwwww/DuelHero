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
        private void OnEnergyChanged() => Changed?.Invoke();
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
            if (IsExecuting || card == null || playerDeck == null || reservations.Count >= 3) return false;
            bool owned = false;
            foreach (var instance in playerDeck.Cards) if (ReferenceEquals(instance, card)) { owned = true; break; }
            if (!owned || !card.TryReserve(reservations.Count, CurrentRound)) return false;
            reservations.Add(card); Notify(); return true;
        }
        public bool CancelAt(int slot)
        {
            if (IsExecuting || slot < 0 || slot >= reservations.Count) return false;
            reservations[slot].ClearReservation(); reservations.RemoveAt(slot);
            for (int i = slot; i < reservations.Count; i++)
            {
                reservations[i].ClearReservation(); reservations[i].TryReserve(i, CurrentRound);
            }
            Notify(); return true;
        }
        public bool BeginExecution()
        {
            if (IsExecuting || reservations.Count != 3 || !HasEnoughEnergy(out _)) return false;
            IsExecuting = true; Notify(); return true;
        }
        public void SetActiveSlot(int slot) { ActiveSlot = slot; Notify(); }
        public void CompleteExecution(int executedSlots = 3)
        {
            if (!IsExecuting) return;
            for (int i = 0; i < reservations.Count; i++)
                if (i < executedSlots) reservations[i].MarkUsed(CurrentRound); else reservations[i].ClearReservation();
            reservations.Clear(); CurrentRound++; IsExecuting = false; ActiveSlot = -1; Notify();
        }
        public void AbortExecution()
        {
            IsExecuting = false; ActiveSlot = -1; Notify();
        }
        private void Notify() { Changed?.Invoke(); playerDeck.NotifyStateChanged(); }
        private void OnDestroy() { foreach (var card in reservations) card.ClearReservation(); }
    }
}
