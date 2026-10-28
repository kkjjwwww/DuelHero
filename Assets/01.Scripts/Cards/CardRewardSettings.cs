using UnityEngine;

namespace DuelHero.Cards
{
    [CreateAssetMenu(menuName = "Duel Hero/Card Reward Settings")]
    public sealed class CardRewardSettings : ScriptableObject
    {
        [Min(0)] public int commonWeight = 62;
        [Min(0)] public int uncommonWeight = 25;
        [Min(0)] public int rareWeight = 10;
        [Min(0)] public int legendaryWeight = 3;
        [Range(0f, 1f)] public float ownedCardMultiplier = .25f;

        public int WeightFor(string rarity) => rarity switch
        {
            "일반" => commonWeight,
            "고급" => uncommonWeight,
            "희귀" => rareWeight,
            "전설" => legendaryWeight,
            _ => 0
        };
    }
}
