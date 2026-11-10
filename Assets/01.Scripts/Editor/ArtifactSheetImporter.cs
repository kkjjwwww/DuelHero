using System;
using System.IO;
using System.Linq;
using DuelHero.Data;
using UnityEngine;

internal static class ArtifactSheetImporter
{
    public const string TabName = "Artifact Data";
    public const string CsvName = "Artifacts.csv";
    public const string SourceFolder = "Assets/Data/ArtifactSheets";
    public const string DatabasePath = "Assets/Data/ArtifactDatabase.asset";

    public static ArtifactDatabase ParseFolder(string folder)
    {
        var rows = SheetCsvTable.Read(Path.Combine(folder, CsvName), "id",
            "id", "name", "description", "iconKey", "effectKey", "effectValue", "shopPrice", "shopEnabled");
        var definitions = rows.Select(row =>
        {
            var id = row["id"];
            if (new[] { "name", "description", "iconKey", "effectKey" }.Any(column => row[column].Length == 0))
                throw new FormatException(id + ": 이름, 설명, 아이콘과 효과를 입력해주세요.");
            if (!bool.TryParse(row["shopEnabled"], out bool enabled))
                throw new FormatException(id + ": shopEnabled는 TRUE 또는 FALSE여야 합니다.");
            int value = SheetCsvTable.NonNegative(row["effectValue"], id + ".effectValue");
            if (row["effectKey"] == ArtifactDefinition.Heal && value == 0)
                throw new FormatException(id + ": 회복량은 양수여야 합니다.");
            return new ArtifactDefinition
            {
                id = id, name = row["name"], description = row["description"], iconKey = row["iconKey"],
                effectKey = row["effectKey"], effectValue = value,
                shopPrice = SheetCsvTable.NonNegative(row["shopPrice"], id + ".shopPrice"), shopEnabled = enabled
            };
        }).ToArray();
        var database = ScriptableObject.CreateInstance<ArtifactDatabase>();
        database.artifacts = definitions;
        return database;
    }
}
