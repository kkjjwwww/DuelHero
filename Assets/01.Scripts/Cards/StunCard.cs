using System;
namespace DuelHero.Cards
{
    // A slot marker, never an owned or obtainable card.
    public static class StunCard
    {
        public static readonly CardDefinition Definition = new CardDefinition
        {
            id = "__stunned_slot", name = "기절", description = "아무 행동도 할 수 없다.",
            actionType = "특수", rarity = "일반", energyCost = 0, cooldownTurns = 0,
            isBasicAction = true, keywordIds = Array.Empty<string>(), effects = Array.Empty<CardEffectDefinition>()
        };
        public static bool IsStun(CardDefinition card) => card?.id == Definition.id;
        public static CardView Prefab => UnityEngine.Resources.Load<CardView>("StunCard");
    }
}
