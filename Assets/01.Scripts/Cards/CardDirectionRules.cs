using System;
using System.Linq;
using UnityEngine;

namespace DuelHero.Cards
{
    public static class CardDirectionRules
    {
        public const string KeywordId = "direction_select";
        public static bool CanSelect(CardDefinition card) => card?.keywordIds != null && Array.IndexOf(card.keywordIds, KeywordId) >= 0;
        public static bool IsCardinal(Vector2Int direction) => direction == Vector2Int.right || direction == Vector2Int.up || direction == Vector2Int.left || direction == Vector2Int.down;
        public static Vector2Int Rotate(Vector2Int offset, Vector2Int direction)
        {
            if (direction == Vector2Int.right) return offset;
            if (direction == Vector2Int.up) return new Vector2Int(-offset.y, offset.x);
            if (direction == Vector2Int.left) return -offset;
            if (direction == Vector2Int.down) return new Vector2Int(offset.y, -offset.x);
            throw new ArgumentException("상하좌우 방향을 지정해주세요.");
        }
        // Execution snapshots own their effect coordinates; imported definitions stay unchanged.
        public static CardDefinition ForExecution(CardDefinition card, Vector2Int direction)
        {
            if (card == null || !CanSelect(card)) return card;
            if (!IsCardinal(direction)) throw new ArgumentException("상하좌우 방향을 지정해주세요.");
            return new CardDefinition
            {
                id = card.id, name = card.name, description = card.description, actionType = card.actionType,
                rarity = card.rarity, energyCost = card.energyCost, keywordIds = card.keywordIds,
                isObtainable = card.isObtainable, isBasicAction = card.isBasicAction, isStarterCard = card.isStarterCard,
                effects = card.effects.Select(e => new CardEffectDefinition
                {
                    id = e.id, cardId = e.cardId, effectType = e.effectType, targetType = e.targetType,
                    resolutionOrder = e.resolutionOrder, value = e.value, durationSlots = e.durationSlots,

                    rangeOffsets = e.rangeOffsets?.Select(offset => Rotate(offset, direction)).ToArray()

                }).ToArray()
            };
        }
    }
}
