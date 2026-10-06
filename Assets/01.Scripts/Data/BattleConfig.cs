using UnityEngine;
namespace DuelHero.Data
{
    public sealed class BattleConfig : ScriptableObject
    {
        public string sourceSpreadsheetId, importedAtUtc;
        public bool hasEnergyRecoveryPerTurn;
        public int energyRecoveryPerTurn;
    }
}
