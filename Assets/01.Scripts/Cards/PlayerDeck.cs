using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Cards
{
    public sealed class PlayerDeck : MonoBehaviour
    {
        [SerializeField] private CardDatabase database;
        private readonly List<CardInstance> cards = new();
        private IReadOnlyList<CardInstance> readOnlyCards;
        [NonSerialized] private bool initialized;
        public CardDatabase Database => database;
        public IReadOnlyList<CardInstance> Cards => readOnlyCards ??= cards.AsReadOnly();
        public event Action Changed;

        private void Awake() => Initialize();

        // Called once per deck lifecycle, never when the UI redraws.
        public void Initialize()
        {
            if (initialized || database == null) return;
            foreach (var definition in database.StarterCards)
                cards.Add(new CardInstance(definition));
            initialized = true;
            Changed?.Invoke();
        }

        public CardInstance AddCard(string cardId)
        {
            Initialize();
            if (database == null) throw new InvalidOperationException("PlayerDeck에 CardDatabase를 지정해주세요.");
            var definition = Array.Find(database.cards, card => card.id == cardId);
            if (definition == null) throw new ArgumentException("존재하지 않는 카드 ID: " + cardId, nameof(cardId));
            var instance = new CardInstance(definition);
            cards.Add(instance);
            Changed?.Invoke();
            return instance;
        }

        public bool RemoveCard(CardInstance instance)
        {
            if (instance == null || instance.ReservedSlot >= 0 || !cards.Remove(instance)) return false;
            Changed?.Invoke();
            return true;
        }

        // Reservation/round systems can notify the UI after changing owned instances.
        public void NotifyStateChanged() => Changed?.Invoke();
    }
}
