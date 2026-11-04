using UnityEngine;
namespace DuelHero.Data
{
    public sealed class BattleRewardConfig : ScriptableObject
    {
        public string sourceSpreadsheetId, importedAtUtc;
        [Min(0)] public int normalBattleGold, bossBattleGold;
    }
}
