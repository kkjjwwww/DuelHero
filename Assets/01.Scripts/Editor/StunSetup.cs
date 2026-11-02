using System;
using System.Linq;
using DuelHero.Cards;
using UnityEditor;
using UnityEngine;

public static class StunSetup
{
    public const string PrefabPath = "Assets/Resources/StunCard.prefab";
    [InitializeOnLoadMethod]
    private static void RunRequestedSetup()
    {
        if (!System.IO.File.Exists("Temp/StunSetup.request")) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            System.IO.File.Delete("Temp/StunSetup.request");
            try { Run(); StunValidation.Run(); System.IO.File.WriteAllText("Temp/StunSetup.result", "PASS"); }
            catch (Exception error) { Debug.LogException(error); System.IO.File.WriteAllText("Temp/StunSetup.result", error.ToString()); }
        };
    }
    [MenuItem("Tools/Duel Hero/Cards/Set up stun assets")]
    public static void Run()
    {
        CardSheetImporter.ImportFolder(CardSheetImporter.SourceFolder, refreshViews: false);
        var source = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        var dev = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardRewardSetup.DevelopmentDatabasePath);
        if (dev != null)
        {
            // Keep the existing development-only reward pool while refreshing real definitions.
            dev.cards = source.cards.Concat(dev.cards.Where(c => c.id.StartsWith("reward_test_", StringComparison.Ordinal))).ToArray();
            dev.keywords = source.keywords; dev.statuses = source.statuses;
            EditorUtility.SetDirty(dev); AssetDatabase.SaveAssetIfDirty(dev);
        }
        BuildPrefab();
        Debug.Log("STUN_SETUP_OK");
    }
    public static void BuildPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        var go = PrefabUtility.LoadPrefabContents("Assets/02.Prefabs/CardView.prefab");
        try
        {
            go.name = "StunCard";
            var view = go.GetComponent<CardView>();
            view.Bind(StunCard.Definition, source.keywords);
            view.SetState(string.Empty, 0, false);
            var cracks = new GameObject("BrokenSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(StunCardCracks));
            cracks.transform.SetParent(view.VisualRoot, false);
            var rect = (RectTransform)cracks.transform;
            rect.anchorMin = new Vector2(.08f,.08f); rect.anchorMax = new Vector2(.92f,.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = cracks.GetComponent<StunCardCracks>();
            graphic.color = new Color(.035f, .025f, .055f, .9f); graphic.raycastTarget = false;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
}
