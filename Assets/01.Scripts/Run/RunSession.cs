using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DuelHero.Map;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Shop;

namespace DuelHero.Run
{
    // Only state that survives between encounters belongs here.
    // Energy, status effects, cooldowns and reservations stay in the battle scene.
    public sealed class RunSession
    {
        private readonly List<string> cardIds;
        private readonly ReadOnlyCollection<string> cardIdsView;

        public RouteMap Map { get; }
        public RouteMapProgress Progress { get; }
        public string PendingBattleNodeId { get; private set; }
        public string PendingShopNodeId { get; private set; }
        public string PendingRestNodeId { get; private set; }
        public bool HasPendingNode => PendingBattleNodeId != null || PendingShopNodeId != null || PendingRestNodeId != null;
        public const int RestCardRemovalGold = 30;
        public int Gold { get; private set; }
        private ShopOffer[] shopOffers = Array.Empty<ShopOffer>();
        private readonly HashSet<string> boughtShopArtifacts = new();
        private int shopRefreshCount;
        public IReadOnlyList<ShopOffer> ShopOffers => Array.AsReadOnly(shopOffers);
        public int PlayerHealth { get; private set; }
        public int PlayerMaxHealth { get; }
        public IReadOnlyList<string> CardIds => cardIdsView;

        public RunSession(uint seed, int startingHealth, int maxHealth, IEnumerable<string> startingCardIds, int startingGold = 0)
        {
            if (maxHealth <= 0 || startingHealth <= 0 || startingHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(startingHealth), "런 시작 체력과 최대 체력을 확인해주세요.");

            cardIds = CopyCardIds(startingCardIds);
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold));
            Gold = startingGold;
            cardIdsView = cardIds.AsReadOnly();
            PlayerHealth = startingHealth;
            PlayerMaxHealth = maxHealth;
            Map = RouteMapGenerator.Generate(seed);
            Progress = new RouteMapProgress(Map);
        }

        public bool TryBeginBattle(string nodeId)
        {
            if (PlayerHealth <= 0 || HasPendingNode || !Progress.CanSelect(nodeId)) return false;
            var type = Map.GetNode(nodeId).Type;
            if (type != RouteNodeType.Battle && type != RouteNodeType.Boss) return false;

            // Entering a battle must not unlock the next route before victory.
            PendingBattleNodeId = nodeId;
            return true;
        }

        public bool TryCompleteBattle(int remainingHealth, IEnumerable<string> ownedCardIds, int goldReward = 0)
        {
            if (PendingBattleNodeId == null || PlayerHealth <= 0) return false;
            if (remainingHealth <= 0 || remainingHealth > PlayerMaxHealth)
                throw new ArgumentOutOfRangeException(nameof(remainingHealth), "승리 후 체력은 1 이상, 최대 체력 이하여야 합니다.");

            var nextCards = CopyCardIds(ownedCardIds);
            if (goldReward < 0) throw new ArgumentOutOfRangeException(nameof(goldReward));
            int nextGold = checked(Gold + goldReward);
            if (!Progress.TrySelect(PendingBattleNodeId)) return false;
            ApplyPlayerState(remainingHealth, nextCards);
            Gold = nextGold;
            PendingBattleNodeId = null;
            return true;
        }

        public bool TryCancelBattle(string nodeId)
        {
            if (nodeId == null || PendingBattleNodeId != nodeId) return false;
            PendingBattleNodeId = null;
            return true;
        }

        public bool TryBeginShop(string nodeId, IEnumerable<CardDefinition> definitions, ShopConfig config)
        {
            if (PlayerHealth <= 0 || HasPendingNode || !Progress.CanSelect(nodeId) ||
                Map.GetNode(nodeId).Type != RouteNodeType.Shop) return false;
            var offers = ShopOfferSelector.Generate(definitions, config, Map.Seed, nodeId);
            boughtShopArtifacts.Clear();
            shopRefreshCount = 0;
            shopOffers = offers; PendingShopNodeId = nodeId; return true;
        }

        // Only used when entry failed before the shop scene opened. No purchase is discarded.
        public bool TryCancelShopEntry(string nodeId)
        {
            if (nodeId == null || PendingShopNodeId != nodeId || boughtShopArtifacts.Count != 0 || Array.Exists(shopOffers, o => o.Sold)) return false;
            PendingShopNodeId = null; shopOffers = Array.Empty<ShopOffer>(); return true;
        }

        public bool TryBuyShopOffer(string nodeId, int index)
        {
            if (PlayerHealth <= 0 || nodeId == null || PendingShopNodeId != nodeId || index < 0 || index >= shopOffers.Length) return false;
            var offer = shopOffers[index];
            if (offer.Sold || Gold < offer.Price) return false;
            cardIds.Add(offer.CardId);
            Gold -= offer.Price; offer.Sold = true; return true;
        }

        public bool TryCompleteShop(string nodeId)
        {
            if (nodeId == null || PendingShopNodeId != nodeId || !Progress.TrySelect(nodeId)) return false;
            PendingShopNodeId = null; shopOffers = Array.Empty<ShopOffer>(); return true;
        }

        public bool IsShopArtifactSold(string id) => boughtShopArtifacts.Contains(id);

        public bool CanBuyShopArtifact(string nodeId, ArtifactDefinition artifact)
        {
            if (PlayerHealth <= 0 || nodeId == null || PendingShopNodeId != nodeId || artifact == null ||
                !artifact.shopEnabled || string.IsNullOrEmpty(artifact.id) || artifact.shopPrice < 0 ||
                Gold < artifact.shopPrice || IsShopArtifactSold(artifact.id)) return false;
            return artifact.effectKey switch
            {
                ArtifactDefinition.Heal => artifact.effectValue > 0 && PlayerHealth < PlayerMaxHealth,
                ArtifactDefinition.RefreshShop => shopOffers.Length > 0,
                _ => false
            };
        }

        public bool TryBuyShopArtifact(string nodeId, ArtifactDefinition artifact, IEnumerable<CardDefinition> definitions, ShopConfig config)
        {
            if (!CanBuyShopArtifact(nodeId, artifact)) return false;
            if (artifact.effectKey == ArtifactDefinition.RefreshShop)
            {
                if (definitions == null || config == null) return false;
                var newOffers = ShopOfferSelector.Generate(definitions, config, Map.Seed, nodeId, shopRefreshCount + 1);
                if (newOffers.Length == 0) return false;
                shopOffers = newOffers;
                shopRefreshCount++;
            }
            else
            {
                PlayerHealth += Math.Min(artifact.effectValue, PlayerMaxHealth - PlayerHealth);
            }
            Gold -= artifact.shopPrice;
            boughtShopArtifacts.Add(artifact.id);
            return true;
        }

        public bool TryBeginRest(string nodeId)
        {
            if (PlayerHealth <= 0 || HasPendingNode || !Progress.CanSelect(nodeId) ||
                Map.GetNode(nodeId).Type != RouteNodeType.Rest) return false;
            PendingRestNodeId = nodeId;
            return true;
        }

        public bool TryCancelRestEntry(string nodeId)
        {
            if (nodeId == null || PendingRestNodeId != nodeId) return false;
            PendingRestNodeId = null;
            return true;
        }

        public bool TryHealAtRest(string nodeId, int amount)
        {
            if (amount < 0 || !TryCompleteRest(nodeId)) return false;
            PlayerHealth += Math.Min(amount, PlayerMaxHealth - PlayerHealth);
            return true;
        }

        public bool TryRemoveCardAtRest(string nodeId, int index, string expectedId)
        {
            if (cardIds.Count <= 1 || index < 0 || index >= cardIds.Count ||
                cardIds[index] != expectedId || expectedId == WaitCard.Definition.id) return false;
            int nextGold = checked(Gold + RestCardRemovalGold);
            if (!TryCompleteRest(nodeId)) return false;
            cardIds.RemoveAt(index);
            Gold = nextGold;
            return true;
        }

        private bool TryCompleteRest(string nodeId)
        {
            if (nodeId == null || PendingRestNodeId != nodeId || PlayerHealth <= 0 || !Progress.TrySelect(nodeId)) return false;
            PendingRestNodeId = null;
            return true;
        }

        public void SavePlayerState(int health, IEnumerable<string> ownedCardIds)
        {
            if (health < 0 || health > PlayerMaxHealth)
                throw new ArgumentOutOfRangeException(nameof(health), "체력은 0 이상, 최대 체력 이하여야 합니다.");
            var nextCards = CopyCardIds(ownedCardIds);
            ApplyPlayerState(health, nextCards);
        }

        private void ApplyPlayerState(int health, List<string> nextCards)
        {
            PlayerHealth = health;
            cardIds.Clear();
            cardIds.AddRange(nextCards);
        }

        private static List<string> CopyCardIds(IEnumerable<string> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<string>();
            foreach (var id in source)
            {
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("보유 카드 ID는 비어 있을 수 없습니다.", nameof(source));
                copy.Add(id);
            }
            return copy;
        }
    }
}
