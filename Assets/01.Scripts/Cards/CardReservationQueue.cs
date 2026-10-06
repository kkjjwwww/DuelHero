using System;
using System.Collections.Generic;
using UnityEngine;
namespace DuelHero.Cards
{
    public sealed class CardReservationQueue : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        private readonly List<CardInstance> reservations = new();
        private IReadOnlyList<CardInstance> readOnlyReservations;
        public IReadOnlyList<CardInstance> Reservations => readOnlyReservations ??= reservations.AsReadOnly();
        public int CurrentRound { get; private set; } = 1;
        public event Action Changed;
        public bool TryReserve(CardInstance card)
        {
            if (card == null || playerDeck == null || reservations.Count >= 3) return false;
            bool owned = false;
            foreach (var instance in playerDeck.Cards) if (ReferenceEquals(instance, card)) { owned = true; break; }
            if (!owned || !card.TryReserve(reservations.Count, CurrentRound)) return false;
            reservations.Add(card); Notify(); return true;
        }
        public bool CancelAt(int slot)
        {
            if (slot < 0 || slot >= reservations.Count) return false;
            reservations[slot].ClearReservation(); reservations.RemoveAt(slot);
            for (int i = slot; i < reservations.Count; i++)
            {
                reservations[i].ClearReservation(); reservations[i].TryReserve(i, CurrentRound);
            }
            Notify(); return true;
        }
        private void Notify() { Changed?.Invoke(); playerDeck.NotifyStateChanged(); }
        private void OnDestroy() { foreach (var card in reservations) card.ClearReservation(); }
    }
}
