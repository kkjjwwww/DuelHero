using System;
using UnityEngine;

namespace DuelHero.Data
{
    [Serializable]
    public sealed class ArtifactDefinition
    {
        public const string Heal = "heal";
        public const string RefreshShop = "refresh_shop";

        public string id, name, description, iconKey, effectKey;
        public int effectValue, shopPrice;
        public bool shopEnabled;
    }

    public sealed class ArtifactDatabase : ScriptableObject
    {
        public ArtifactDefinition[] artifacts = Array.Empty<ArtifactDefinition>();
        [SerializeField] private string sourceSpreadsheetId, importedAtUtc;
    }
}
