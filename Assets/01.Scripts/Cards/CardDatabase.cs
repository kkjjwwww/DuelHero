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
        public string statusId, triggerEffectId;
        public string triggerTiming;
        public string movementStyle, rangeBasis;
        public bool IsSelfStun => effectType == "applyStatus" && targetType == "자신" && statusId == "stun" && !IsCounter;
        public bool IsDash => effectType == "move" && movementStyle == "dash";
        public bool IsLongJump => effectType == "move" && movementStyle == "longJump";
        public bool UsesMovementPath => rangeBasis == "movementPath";
        public int statusMultiplier;
        public bool IsCounter => triggerTiming == "counter";
        public int resolutionOrder, value, durationSlots;
        public Vector2Int[] rangeOffsets;
    }

    [Serializable]
    public class CardKeywordParameter
    {
        public string keywordId;
        public int value;
    }

    [Serializable]
    public class CardDefinition
    {
        public string id, name, description, actionType, rarity;
        public string attackStyle;
        public string attackVfx, hitVfx;
        [NonSerialized] public Vector2Int executionDirection;
        public int energyCost;
        public bool spendsAllEnergy;
        public int EnergyCostAt(int available) => spendsAllEnergy ? available : energyCost;
        public string EnergyCostLabel => spendsAllEnergy ? "?" : energyCost.ToString();
        public int SelfStun => effects?.Where(e => e.IsSelfStun).Sum(e => e.value) ?? 0;
        public int cooldownTurns = 1;
        public int EffectiveCooldownTurns => isBasicAction ? 0 : cooldownTurns;
        public string[] keywordIds;
        public bool isObtainable, isBasicAction, isStarterCard;
        public CardEffectDefinition[] effects;
        public CardKeywordParameter[] keywordParameters = Array.Empty<CardKeywordParameter>();

        public int KeywordParameter(string keywordId, int defaultValue = 0) =>
            keywordParameters.FirstOrDefault(parameter => parameter.keywordId == keywordId)?.value ?? defaultValue;

        public string ResolvedDescription(Func<CardEffectDefinition, int, string> formatValue = null,
            Func<int, string> formatCooldown = null)
        {
            return Regex.Replace(description, @"\[\{cooldownTurns\}턴\]|\{cooldownTurns\}|\{effect(\d+)\.(value|durationSlots|statusMultiplier)\}", match =>
            {
                if (match.Value == "{cooldownTurns}") return EffectiveCooldownTurns.ToString();
                if (match.Value == "[{cooldownTurns}턴]")
                {
                    int turns = EffectiveCooldownTurns;
                    return turns <= 0 ? string.Empty : formatCooldown != null ? formatCooldown(turns) : $"[{turns}턴]";
                }
                int index = int.Parse(match.Groups[1].Value) - 1;
                if (index < 0 || index >= effects.Length)
                    throw new InvalidOperationException($"{id}: invalid description placeholder {match.Value}");
                var effect = effects[index];
                if (match.Groups[2].Value == "durationSlots") return effect.durationSlots.ToString();
                if (match.Groups[2].Value == "statusMultiplier") return effect.statusMultiplier.ToString();
                return formatValue != null ? formatValue(effect, effect.value) : effect.value.ToString();
            });
        }

        public int Amount(string effectType) => effects.Where(e => e.effectType == effectType).Sum(e => e.value);
    }

    [Serializable]
    public class KeywordDefinition
    {
        public string id, name, description, parameters;
        public string textColor;
        public string TextColor => string.IsNullOrWhiteSpace(textColor) ? "#FFBA47" : textColor;
    }

    [Serializable]
    public class StatusDefinition
    {
        public string id, name, category, keywordId, stackRule, countUnit;
    }

    public class CardDatabase : ScriptableObject
    {
        public string sourceSpreadsheetId;
        public string importedAtUtc;
        public CardDefinition[] cards = Array.Empty<CardDefinition>();
        public KeywordDefinition[] keywords = Array.Empty<KeywordDefinition>();
        public StatusDefinition[] statuses = Array.Empty<StatusDefinition>();

        public IEnumerable<CardDefinition> StarterCards => cards.Where(c => c.isStarterCard || c.isBasicAction);
    }

    // Runtime state belongs to a particular owned copy, never to the shared database.
    public sealed class CardInstance
    {
        public CardDefinition Definition { get; }
        public int ReservedSlot { get; private set; } = -1;
        public Vector2Int SelectedDirection { get; private set; } = Vector2Int.right;
        public int AvailableFromRound { get; private set; } = 1;
        public CardInstance(CardDefinition definition) => Definition = definition;
        public int RemainingCooldown(int round) => Definition.isBasicAction ? 0 : Mathf.Max(0, AvailableFromRound - round);
        public int RemainingPreparation(int round)
        {
#if UNITY_EDITOR
            if (preparationSkippedForDebug) return 0;
#endif
            return Mathf.Max(0, Definition.KeywordParameter("preparation") - (round - 1));
        }
#if UNITY_EDITOR
        private bool preparationSkippedForDebug;
        public void SkipPreparationForDebug() => preparationSkippedForDebug = true;
        public void ResetCooldownForDebug() => AvailableFromRound = 1;
#endif
        public int RemainingAvailabilityDelay(int round) => Mathf.Max(RemainingPreparation(round), RemainingCooldown(round));
        public bool IsExhausted { get; private set; }
        public bool CanReserve(int round) => !IsExhausted && ReservedSlot < 0 && RemainingAvailabilityDelay(round) == 0;
        internal bool ExhaustAfterUse()
        {
            if (IsExhausted || Definition.keywordIds == null || Array.IndexOf(Definition.keywordIds, "exhaust") < 0) return false;
            IsExhausted = true;
            return true;
        }
        public bool TryReserve(int slot, int round)
        {
            if (slot < 0 || slot > 2 || !CanReserve(round)) return false;
            ReservedSlot = slot;
            SelectedDirection = Vector2Int.right;
            return true;
        }
        internal void RelocateReservation(int slot) => ReservedSlot = slot;
        internal bool SetDirection(Vector2Int direction)
        {
            if (ReservedSlot < 0 || !CardDirectionRules.CanSelect(Definition) || !CardDirectionRules.IsCardinal(direction)) return false;
            SelectedDirection = direction;
            return true;
        }
        public void ClearReservation() { ReservedSlot = -1; SelectedDirection = Vector2Int.right; }
        public void MarkUsed(int round, bool exhaust = true)
        {
            if (exhaust) ExhaustAfterUse();
            if (!Definition.isBasicAction) AvailableFromRound = round + Definition.EffectiveCooldownTurns + 1;
            ClearReservation();
        }
    }
}
