using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DuelHero.Map;

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
        public int PlayerHealth { get; private set; }
        public int PlayerMaxHealth { get; }
        public IReadOnlyList<string> CardIds => cardIdsView;

        public RunSession(uint seed, int startingHealth, int maxHealth, IEnumerable<string> startingCardIds)
        {
            if (maxHealth <= 0 || startingHealth <= 0 || startingHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(startingHealth), "런 시작 체력과 최대 체력을 확인해주세요.");

            cardIds = CopyCardIds(startingCardIds);
            cardIdsView = cardIds.AsReadOnly();
            PlayerHealth = startingHealth;
            PlayerMaxHealth = maxHealth;
            Map = RouteMapGenerator.Generate(seed);
            Progress = new RouteMapProgress(Map);
        }

        public bool TryBeginBattle(string nodeId)
        {
            if (PlayerHealth <= 0 || PendingBattleNodeId != null || !Progress.CanSelect(nodeId)) return false;
            var type = Map.GetNode(nodeId).Type;
            if (type != RouteNodeType.Battle && type != RouteNodeType.Boss) return false;

            // Entering a battle must not unlock the next route before victory.
            PendingBattleNodeId = nodeId;
            return true;
        }

        public bool TryCompleteBattle(int remainingHealth, IEnumerable<string> ownedCardIds)
        {
            if (PendingBattleNodeId == null || PlayerHealth <= 0) return false;
            if (remainingHealth <= 0 || remainingHealth > PlayerMaxHealth)
                throw new ArgumentOutOfRangeException(nameof(remainingHealth), "승리 후 체력은 1 이상, 최대 체력 이하여야 합니다.");

            var nextCards = CopyCardIds(ownedCardIds);
            if (!Progress.TrySelect(PendingBattleNodeId)) return false;
            ApplyPlayerState(remainingHealth, nextCards);
            PendingBattleNodeId = null;
            return true;
        }

        public bool TryCancelBattle(string nodeId)
        {
            if (nodeId == null || PendingBattleNodeId != nodeId) return false;
            PendingBattleNodeId = null;
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
