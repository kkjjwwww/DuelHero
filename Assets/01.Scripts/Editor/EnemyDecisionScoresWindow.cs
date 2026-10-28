using DuelHero.Battle;
using UnityEditor;
using UnityEngine;

public sealed class EnemyDecisionScoresWindow : EditorWindow
{
    [SerializeField] private EnemyActionPlanProvider source;
    private Vector2 scroll;
    private EnemyDecisionTrace displayedTrace;

    [MenuItem("Tools/Duel Hero/AI/Decision scores")]
    public static void Open() => GetWindow<EnemyDecisionScoresWindow>("AI 점수 디버그");

    private void OnEnable()
    {
        minSize = new Vector2(900, 350);
        UtilityPlanSelector.CaptureScores = true;
        FollowSelection();
        if (source == null) source = Object.FindFirstObjectByType<EnemyActionPlanProvider>();
    }
    private void OnDisable() => UtilityPlanSelector.CaptureScores = false;
    private void OnSelectionChange() { FollowSelection(); Repaint(); }
    private void FollowSelection()
    {
        var selected = Selection.activeGameObject;
        var provider = selected != null ? selected.GetComponent<EnemyActionPlanProvider>() : null;
        if (provider != null) source = provider;
    }
    private void OnInspectorUpdate()
    {
        if (source != null && source.LastDecisionTrace != displayedTrace) Repaint();
    }

    private void OnGUI()
    {
        UtilityPlanSelector.CaptureScores = EditorGUILayout.Toggle("후보 점수 기록", UtilityPlanSelector.CaptureScores);
        source = (EnemyActionPlanProvider)EditorGUILayout.ObjectField("대상 적", source, typeof(EnemyActionPlanProvider), true);
        if (GUILayout.Button("현재 씬에서 적 찾기")) source = Object.FindFirstObjectByType<EnemyActionPlanProvider>();
        EditorGUILayout.HelpBox("계획 생성 전에 창을 열어두세요. Hierarchy에서 적을 선택하거나 대상 적을 지정하면 최신 라운드를 표시합니다. " +
            "기록을 끄거나 창을 닫으면 새 후보 기록을 수집하지 않습니다. 공개된 계획을 다시 계산하지 않습니다.", MessageType.Info);
        if (source == null) { EditorGUILayout.LabelField("플레이 중인 적의 EnemyActionPlanProvider를 지정해주세요."); return; }
        displayedTrace = source.LastDecisionTrace;
        if (displayedTrace == null)
        {
            EditorGUILayout.LabelField("아직 기록이 없습니다. 기록을 켠 상태에서 전투를 시작하거나 다음 라운드로 진행해주세요.");
            return;
        }
        EditorGUILayout.LabelField($"{source.name} · 라운드 {displayedTrace.Round} · 판단 시드 {displayedTrace.DecisionSeed}", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("총점 = 피해 + 공격 기회·접근 + 에너지 − 비용 부담. 점수는 앞 슬롯을 포함한 누적 평가값이며 슬롯 안에서 비교합니다. " +
            "●는 추첨 후보, ▶는 실제 선택입니다. 허용 점수 차이 안의 상위 후보 중 균등 확률로 선택합니다. " +
            "비용 부족·쿨다운·예약 중·이미 선택한 카드는 평가 대상에서 제외됩니다.", MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var slot in displayedTrace.Slots)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"슬롯 {slot.Index + 1} · 시작 위치 {slot.PositionBefore} · 에너지 {slot.EnergyBefore}", EditorStyles.boldLabel);
            if (slot.SelectedIndex < 0) { EditorGUILayout.HelpBox(slot.Note, MessageType.Info); continue; }
            var selected = slot.Candidates[slot.SelectedIndex];
            EditorGUILayout.LabelField($"선택: {selected.CardName} [{Direction(selected.Direction)}] · {selected.Score.Total:F3}점", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"최고 {slot.BestScore:F3}점 · 허용 최소 {slot.MinimumScore:F3}점 · 추첨 후보 {slot.EligibleIndices.Count}개");
            EditorGUILayout.BeginHorizontal();
            Cell("선택", 35); Cell("카드 / 보유 번호", 170); Cell("방향", 40); Cell("총점", 70);
            Cell("피해 점수", 70); Cell("기회·접근", 75); Cell("에너지", 65); Cell("비용 감점", 75);
            Cell("종료 에너지", 80); Cell("이번 피해", 65);
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < slot.Candidates.Count; i++)
            {
                var row = slot.Candidates[i];
                var color = GUI.backgroundColor;
                if (i == slot.SelectedIndex) GUI.backgroundColor = new Color(.55f, .9f, .65f);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                bool eligible = false;
                for (int j = 0; j < slot.EligibleIndices.Count; j++) if (slot.EligibleIndices[j] == i) { eligible = true; break; }
                Cell(i == slot.SelectedIndex ? "▶" : eligible ? "●" : "", 35);
                GUILayout.Label(new GUIContent($"{row.CardName} #{row.CardIndex + 1}", row.CardId + " · 종료 위치 " + row.PositionAfter), GUILayout.Width(170));
                Cell(Direction(row.Direction), 40); Cell(row.Score.Total.ToString("F3"), 70);
                Cell(row.Score.Damage.ToString("F3"), 70); Cell(row.Score.Opportunity.ToString("F3"), 75);
                Cell(row.Score.Energy.ToString("F3"), 65); Cell(row.Score.CostPenalty.ToString("F3"), 75);
                Cell(row.EnergyAfter.ToString(), 80); Cell(row.EffectiveDamage.ToString(), 65);
                EditorGUILayout.EndHorizontal(); GUI.backgroundColor = color;
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static void Cell(string text, float width) => GUILayout.Label(text, GUILayout.Width(width));
    private static string Direction(Vector2Int direction) => direction == Vector2Int.right ? "오른쪽" :
        direction == Vector2Int.up ? "위" : direction == Vector2Int.left ? "왼쪽" : "아래";
}
