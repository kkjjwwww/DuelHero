using System;
using UnityEngine;
namespace DuelHero.Data
{
    [Serializable]
    public sealed class UnitDefinition
    {
        public string id, name;
        public int startingHealth, maxHealth, startingEnergy, maxEnergy;
    }
    public sealed class UnitDatabase : ScriptableObject
    {
        public string sourceSpreadsheetId, importedAtUtc;
        public UnitDefinition[] players = Array.Empty<UnitDefinition>();
        public UnitDefinition[] enemies = Array.Empty<UnitDefinition>();
    }
}
