using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DuelHero.Cards;
using UnityEditor;
using UnityEngine;

public sealed class GoogleSheetSyncWindow : EditorWindow
{
    private string spreadsheetId;
    private string status = "최초 1회 OAuth JSON 가져오기와 Google 로그인이 필요합니다.";
    private bool busy;
    private SheetSyncScope scope = SheetSyncScope.All;
    private CancellationTokenSource cancellation;
    private string PreferenceKey => "DuelHero.SheetId." + Application.dataPath;

    [MenuItem("Tools/Duel Hero/시트 데이터 갱신")]
    public static void Open() => GetWindow<GoogleSheetSyncWindow>("시트 데이터 갱신");
    private void OnEnable()
    {
        spreadsheetId = EditorPrefs.GetString(PreferenceKey, CardSheetImporter.DefaultSpreadsheetId);
        minSize = new Vector2(440, 360);
        AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        EditorApplication.quitting += Cancel;
    }
    private void OnDisable()
    {
        Cancel();
        AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
        EditorApplication.quitting -= Cancel;
    }
    private void Cancel() => cancellation?.Cancel();
    private void OnGUI()
    {
        GUILayout.Label("Google Sheets → 게임 데이터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("카드·유닛·전투 설정을 선택해 갱신합니다. 전체 갱신은 모든 검증을 통과한 뒤 함께 저장합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(busy))
        {
            spreadsheetId = EditorGUILayout.TextField("시트 ID 또는 URL", spreadsheetId);
            scope = (SheetSyncScope)EditorGUILayout.Popup("갱신 범위", (int)scope, new[] { "전체", "카드", "유닛 (플레이어·적)", "전투 설정" });
            GUILayout.Label("OAuth 설정: " + (GoogleSheetSyncClient.HasClient ? "등록됨" : "필요"));
            GUILayout.Label("저장된 로그인: " + (GoogleSheetSyncClient.HasToken ? "있음" : "없음"));
            if (GUILayout.Button("1. 데스크톱 OAuth 클라이언트 JSON 가져오기"))
            {
                string path = EditorUtility.OpenFilePanel("Google Desktop OAuth client JSON", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try { GoogleSheetSyncClient.ImportDesktopClient(File.ReadAllText(path)); status = "OAuth 설정을 등록했습니다. Google 로그인 버튼을 눌러주세요."; }
                    catch { status = "설정을 가져오지 못했습니다. 데스크톱 앱용 OAuth JSON 파일과 로컬 저장 권한을 확인해주세요."; }
                }
            }
            using (new EditorGUI.DisabledScope(!GoogleSheetSyncClient.HasClient))
                if (GUILayout.Button("2. Google 로그인 (시트 읽기 전용)")) Run(SignIn);
            using (new EditorGUI.DisabledScope(!GoogleSheetSyncClient.HasToken || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                if (GUILayout.Button("시트 데이터 갱신", GUILayout.Height(34))) Run(Sync);
            if (GoogleSheetSyncClient.HasToken && GUILayout.Button("저장된 로그인 지우기")) { GoogleSheetSyncClient.ForgetToken(); status = "이 PC에 저장된 로그인을 지웠습니다."; }
        }
        if (busy && GUILayout.Button("취소")) Cancel();
        EditorGUILayout.HelpBox(status, MessageType.None);
        var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        if (database != null) EditorGUILayout.LabelField("카드 갱신 (UTC)", database.importedAtUtc);
        var units = AssetDatabase.LoadAssetAtPath<DuelHero.Data.UnitDatabase>(UnitSheetImporter.DatabasePath);
        if (units != null) EditorGUILayout.LabelField("유닛 갱신 (UTC)", units.importedAtUtc);
        var config = AssetDatabase.LoadAssetAtPath<DuelHero.Data.BattleConfig>(BattleConfigSheetImporter.DatabasePath);
        if (config != null) EditorGUILayout.LabelField("전투 설정 갱신 (UTC)", config.importedAtUtc);
        if (GUILayout.Button("설정 및 사용 설명 열기")) Application.OpenURL("file://" + Path.GetFullPath("Docs/CardDataImport.md").Replace('\\', '/'));
    }
    private async void Run(Func<CancellationToken, Task> operation)
    {
        if (busy) return;
        busy = true;
        cancellation = new CancellationTokenSource();
        try { await operation(cancellation.Token); }
        catch (OperationCanceledException) { status = "작업이 취소되었거나 로그인 대기 시간이 만료되었습니다."; }
        catch (Exception error) { status = "갱신하지 못했습니다: " + error.Message; }
        finally { busy = false; cancellation.Dispose(); cancellation = null; if (this != null) Repaint(); }
    }
    private async Task SignIn(CancellationToken token)
    {
        status = "브라우저에서 로그인해주세요. 최대 3분간 기다립니다.";
        await GoogleSheetSyncClient.SignInAsync(token);
        status = "로그인했습니다. 시트 데이터 갱신 버튼을 눌러주세요.";
    }
    private async Task Sync(CancellationToken token)
    {
        string id = spreadsheetId.Trim();
        var match = Regex.Match(id, @"/spreadsheets/d/([A-Za-z0-9_-]+)");
        if (match.Success) id = match.Groups[1].Value;
        var selectedScope = scope;
        status = "선택한 시트를 읽는 중입니다…";
        var csv = await GameSheetImporter.DownloadAsync(id, selectedScope, token);
        token.ThrowIfCancellationRequested();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Play 모드와 컴파일이 끝난 뒤 다시 갱신해주세요.");
        string stage = Path.GetFullPath(Path.Combine("Library", "DuelHeroSheetSync", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var file in csv) File.WriteAllText(Path.Combine(stage, file.Key), file.Value, new UTF8Encoding(false));
            token.ThrowIfCancellationRequested();
            string result = GameSheetImporter.ImportFolder(stage, id, selectedScope);
            EditorPrefs.SetString(PreferenceKey, id);
            AssetDatabase.Refresh();
            status = "갱신 완료. " + result + " 씬 미리보기를 보존하려면 씬을 저장해주세요.";
        }
        finally { Directory.Delete(stage, true); }
    }
}
