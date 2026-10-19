using System;

namespace DuelHero.Cards
{
    // System action: not imported, obtained, or removed as an owned card.
    public static class WaitCard
    {
        public const string Id = "system_wait";
        public static CardDefinition Definition { get; } = new()
        {
            id = Id, name = "대기", description = "아무행동도 하지않고 기다린다.",
            actionType = "특수", energyCost = 0, isBasicAction = true,
            keywordIds = Array.Empty<string>(), effects = Array.Empty<CardEffectDefinition>()
        };

        public static bool IsWait(CardDefinition card) => card?.id == Id;
    }
}
