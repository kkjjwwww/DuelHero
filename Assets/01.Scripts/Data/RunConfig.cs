using UnityEngine;
namespace DuelHero.Data
{
    public sealed class RunConfig : ScriptableObject
    {
        public string sourceSpreadsheetId, importedAtUtc;
        [Min(0)] public int startingGold;
        [Min(0)] public int restHealAmount = 30;
    }
}
