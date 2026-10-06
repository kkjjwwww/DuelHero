using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public enum SheetSyncScope { All, Cards, Units, BattleConfig }

// Coordinates validation and saving; individual importers own their schemas.
public static class GameSheetImporter
{
    private sealed class Pending
    {
        public string Path;
        public ScriptableObject Parsed, Target;
        public string Before;
        public bool Created;
    }
    public static async Task<Dictionary<string, string>> DownloadAsync(string id, SheetSyncScope scope, CancellationToken cancellation)
    {
        var output = new Dictionary<string, string>();
        if (scope == SheetSyncScope.All || scope == SheetSyncScope.Cards)
            foreach (var file in await GoogleSheetSyncClient.DownloadCsvAsync(id, cancellation)) output.Add(file.Key, file.Value);
        if (scope == SheetSyncScope.All || scope == SheetSyncScope.Units)
            foreach (var file in await GoogleSheetSyncClient.DownloadCsvAsync(id, new[] { "Player Data", "Enemy Data" }, new[] { "Players.csv", "Enemies.csv" }, "id", cancellation)) output.Add(file.Key, file.Value);
        if (scope == SheetSyncScope.All || scope == SheetSyncScope.BattleConfig)
            foreach (var file in await GoogleSheetSyncClient.DownloadCsvAsync(id, new[] { "BattleConfig" }, new[] { "BattleConfig.csv" }, "key", cancellation)) output.Add(file.Key, file.Value);
        return output;
    }
    public static string ImportFolder(string folder, string spreadsheetId, SheetSyncScope scope)
    {
        var assets = new List<Pending>();
        var files = new Dictionary<string, string>();
        var previousFiles = new Dictionary<string, byte[]>();
        try
        {
            // All parsers finish before modifying files or existing assets.
            if (scope == SheetSyncScope.All || scope == SheetSyncScope.Cards)
            {
                assets.Add(new Pending { Path = CardSheetImporter.DatabasePath, Parsed = CardSheetImporter.ParseFolder(folder) });
                foreach (string name in GoogleSheetSyncClient.CsvNames) files.Add(Path.Combine(CardSheetImporter.SourceFolder, name), Path.Combine(folder, name));
            }
            if (scope == SheetSyncScope.All || scope == SheetSyncScope.Units)
            {
                assets.Add(new Pending { Path = UnitSheetImporter.DatabasePath, Parsed = UnitSheetImporter.ParseFolder(folder) });
                foreach (string name in new[] { "Players.csv", "Enemies.csv" }) files.Add(Path.Combine(UnitSheetImporter.SourceFolder, name), Path.Combine(folder, name));
            }
            if (scope == SheetSyncScope.All || scope == SheetSyncScope.BattleConfig)
            {
                assets.Add(new Pending { Path = BattleConfigSheetImporter.DatabasePath, Parsed = BattleConfigSheetImporter.ParseFolder(folder) });
                files.Add(Path.Combine(BattleConfigSheetImporter.SourceFolder, "BattleConfig.csv"), Path.Combine(folder, "BattleConfig.csv"));
            }
            if (assets.Count == 0) throw new ArgumentOutOfRangeException(nameof(scope));
            var incoming = files.ToDictionary(file => file.Key, file => File.ReadAllBytes(file.Value));
            foreach (var file in files) previousFiles.Add(file.Key, File.Exists(file.Key) ? File.ReadAllBytes(file.Key) : null);
            foreach (var asset in assets)
            {
                asset.Target = AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset.Path);
                if (asset.Target != null && asset.Target.GetType() != asset.Parsed.GetType()) throw new InvalidOperationException(asset.Path + ": 에셋 유형이 다릅니다.");
                asset.Before = asset.Target == null ? null : JsonUtility.ToJson(asset.Target);
                asset.Parsed.name = asset.Target == null ? Path.GetFileNameWithoutExtension(asset.Path) : asset.Target.name;
                var serialized = new SerializedObject(asset.Parsed);
                serialized.FindProperty("sourceSpreadsheetId").stringValue = spreadsheetId;
                serialized.FindProperty("importedAtUtc").stringValue = DateTime.UtcNow.ToString("o");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            try
            {
                foreach (var file in incoming)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.Key)); File.WriteAllBytes(file.Key, file.Value);
                }
                foreach (var asset in assets)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(asset.Path));
                    if (asset.Target == null)
                    {
                        asset.Target = ScriptableObject.CreateInstance(asset.Parsed.GetType());
                        AssetDatabase.CreateAsset(asset.Target, asset.Path); asset.Created = true;
                    }
                    EditorUtility.CopySerialized(asset.Parsed, asset.Target);
                    EditorUtility.SetDirty(asset.Target); AssetDatabase.SaveAssetIfDirty(asset.Target);
                }
            }
            catch (Exception saveError)
            {
                var failures = new List<Exception> { saveError };
                foreach (var asset in assets)
                {
                    try
                    {
                        if (asset.Created) AssetDatabase.DeleteAsset(asset.Path);
                        else if (asset.Before != null)
                        {
                            JsonUtility.FromJsonOverwrite(asset.Before, asset.Target);
                            EditorUtility.SetDirty(asset.Target); AssetDatabase.SaveAssetIfDirty(asset.Target);
                        }
                    }
                    catch (Exception rollbackError) { failures.Add(rollbackError); }
                }
                foreach (var file in previousFiles)
                {
                    try { if (file.Value != null) File.WriteAllBytes(file.Key, file.Value); else if (File.Exists(file.Key)) File.Delete(file.Key); }
                    catch (Exception rollbackError) { failures.Add(rollbackError); }
                }
                if (failures.Count > 1) throw new AggregateException("저장 실패 후 일부 복원도 실패했습니다. 로컬 파일 상태를 확인해주세요.", failures);
                throw;
            }
            if (scope == SheetSyncScope.All || scope == SheetSyncScope.Cards) CardSheetImporter.RefreshDeckViews();
            return string.Join(" / ", assets.Select(asset => asset.Parsed is DuelHero.Data.UnitDatabase units ? $"플레이어 {units.players.Length}, 적 {units.enemies.Length}" : asset.Parsed is DuelHero.Cards.CardDatabase cards ? $"카드 {cards.cards.Length}" : "전투 설정 갱신"));
        }
        finally { foreach (var asset in assets) if (asset.Parsed != null) UnityEngine.Object.DestroyImmediate(asset.Parsed); }
    }
}
