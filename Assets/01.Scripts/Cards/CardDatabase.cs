using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DuelHero.Cards
{
    [Serializable]
    public class CardEffectDefinition
    {
        public string id, cardId, effectType, targetType;
        public int resolutionOrder, value, durationSlots;
        public Vector2Int[] rangeOffsets;
    }

    [Serializable]
    public class CardDefinition
    {
        public string id, name, description, actionType, rarity;
        public int energyCost;
        public string[] keywordIds;
        public bool isObtainable, isBasicAction, isStarterCard;
        public CardEffectDefinition[] effects;

        public string ResolvedDescription()
        {
            return Regex.Replace(description, @"\{effect(\d+)\.(value|durationSlots)\}", match =>
            {
                int index = int.Parse(match.Groups[1].Value) - 1;
                if (index < 0 || index >= effects.Length)
                    throw new InvalidOperationException($"{id}: invalid description placeholder {match.Value}");
                return (match.Groups[2].Value == "value" ? effects[index].value : effects[index].durationSlots).ToString();
            });
        }

        public int Amount(string effectType) => effects.Where(e => e.effectType == effectType).Sum(e => e.value);
    }

    [Serializable]
    public class KeywordDefinition
    {
        public string id, name, description, resolutionRule, triggerTiming, parameters;
    }

    public class CardDatabase : ScriptableObject
    {
        public string sourceSpreadsheetId;
        public string importedAtUtc;
        public CardDefinition[] cards = Array.Empty<CardDefinition>();
        public KeywordDefinition[] keywords = Array.Empty<KeywordDefinition>();

        public IEnumerable<CardDefinition> StarterCards => cards.Where(c => c.isStarterCard || c.isBasicAction);
    }

    // Runtime state belongs to a particular owned copy, never to the shared database.
    public sealed class CardInstance
    {
        public CardDefinition Definition { get; }
        public int ReservedSlot { get; private set; } = -1;
        public int AvailableFromRound { get; private set; } = 1;
        public CardInstance(CardDefinition definition) => Definition = definition;
        public int RemainingCooldown(int round) => Definition.isBasicAction ? 0 : Mathf.Max(0, AvailableFromRound - round);
        public bool CanReserve(int round) => ReservedSlot < 0 && RemainingCooldown(round) == 0;
        public bool TryReserve(int slot, int round)
        {
            if (slot < 0 || slot > 2 || !CanReserve(round)) return false;
            ReservedSlot = slot;
            return true;
        }
        public void ClearReservation() => ReservedSlot = -1;
        public void MarkUsed(int round)
        {
            // Use in N: unavailable in N+1, available again in N+2.
            if (!Definition.isBasicAction) AvailableFromRound = round + 2;
            ClearReservation();
        }
    }
}
