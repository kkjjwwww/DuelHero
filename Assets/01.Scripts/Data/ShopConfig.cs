using System;
using UnityEngine;
namespace DuelHero.Data
{
    public sealed class ShopConfig : ScriptableObject
    {
        public string sourceSpreadsheetId, importedAtUtc;
        [Min(1)] public int cardOfferCount;
        [Min(0)] public int commonCardPrice, uncommonCardPrice, rareCardPrice, legendaryCardPrice;
        [Min(0)] public int commonWeight, uncommonWeight, rareWeight, legendaryWeight;
        public int PriceFor(string rarity) => rarity switch {
            "일반" => commonCardPrice, "고급" => uncommonCardPrice, "희귀" => rareCardPrice,
            "전설" => legendaryCardPrice, _ => throw new ArgumentException("지원하지 않는 희귀도: " + rarity) };
        public int WeightFor(string rarity) => rarity switch {
            "일반" => commonWeight, "고급" => uncommonWeight, "희귀" => rareWeight, "전설" => legendaryWeight, _ => 0 };
    }
}
