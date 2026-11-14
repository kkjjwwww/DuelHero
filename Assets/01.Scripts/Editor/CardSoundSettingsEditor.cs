using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Audio;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardSoundSettings))]
public sealed class CardSoundSettingsEditor : Editor
{
    private static readonly string[] TimingLabels = { "공격 발동", "돌진 출발", "적중" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var database = serializedObject.FindProperty("cardDatabase");
        var catalog = serializedObject.FindProperty("soundCatalog");
        EditorGUILayout.PropertyField(database, new GUIContent("카드 데이터베이스"));
        EditorGUILayout.PropertyField(catalog, new GUIContent("효과음 목록"));

        var cardData = database.objectReferenceValue as DuelHero.Cards.CardDatabase;
        var soundData = catalog.objectReferenceValue as SoundEffectCatalog;
        if (cardData == null || soundData == null)
            EditorGUILayout.HelpBox("카드 데이터베이스와 효과음 목록을 연결해주세요.", MessageType.Warning);

        var entries = serializedObject.FindProperty("cards");
        var cardChoices = cardData == null || cardData.cards == null
            ? new List<(string id, string label)>()
            : cardData.cards.Where(c => c != null && !string.IsNullOrEmpty(c.id))
                .Select(c => (c.id, $"{c.name} ({c.id})")).ToList();
        var soundChoices = soundData == null || soundData.entries == null
            ? new List<(string id, string label)>()
            : soundData.entries.Where(s => s != null && !string.IsNullOrEmpty(s.id))
                .Select(s => (s.id, s.id + (s.clip == null ? " (음원 없음)" : ""))).ToList();

        if (cardChoices.GroupBy(c => c.id).Any(g => g.Count() > 1))
            EditorGUILayout.HelpBox("중복된 카드 ID가 있습니다. 카드 데이터베이스를 확인해주세요.", MessageType.Warning);
        if (soundChoices.GroupBy(s => s.id).Any(g => g.Count() > 1))
            EditorGUILayout.HelpBox("중복된 효과음 ID가 있습니다. 효과음 목록을 확인해주세요.", MessageType.Warning);

        var used = new HashSet<string>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var cardId = entry.FindPropertyRelative("cardId");
            var cues = entry.FindPropertyRelative("sounds");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"카드 설정 {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("삭제", GUILayout.Width(55)))
            {
                entries.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();
            DrawSelection(cardId, "카드", cardChoices);
            if (!string.IsNullOrEmpty(cardId.stringValue) && !used.Add(cardId.stringValue))
                EditorGUILayout.HelpBox("같은 카드가 여러 번 등록되어 있습니다.", MessageType.Warning);

            for (int j = 0; j < cues.arraySize; j++)
            {
                var cue = cues.GetArrayElementAtIndex(j);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"소리 {j + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("삭제", GUILayout.Width(55)))
                {
                    cues.DeleteArrayElementAtIndex(j);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                var timing = cue.FindPropertyRelative("timing");
                timing.enumValueIndex = EditorGUILayout.Popup("재생 시점", timing.enumValueIndex, TimingLabels);
                var soundId = cue.FindPropertyRelative("soundId");
                DrawSelection(soundId, "효과음", soundChoices);
                var selected = soundData?.Find(soundId.stringValue);
                if (selected != null && selected.clip == null)
                    EditorGUILayout.HelpBox("선택한 효과음에 음원이 연결되지 않았습니다.", MessageType.Warning);
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("volume"), new GUIContent("개별 볼륨"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("delay"), new GUIContent("지연 시간 (초)"));
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("소리 추가"))
            {
                int index = cues.arraySize++;
                var cue = cues.GetArrayElementAtIndex(index);
                cue.FindPropertyRelative("timing").enumValueIndex = 0;
                cue.FindPropertyRelative("soundId").stringValue = "";
                cue.FindPropertyRelative("volume").floatValue = 1f;
                cue.FindPropertyRelative("delay").floatValue = 0f;
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("카드 설정 추가"))
        {
            int index = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("cardId").stringValue = "";
            entry.FindPropertyRelative("sounds").ClearArray();
        }
        EditorGUILayout.HelpBox("현재는 설정을 편집하는 단계입니다. 전투 재생 및 연출 시간 표시는 아직 연결되지 않았습니다.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSelection(SerializedProperty property, string label, List<(string id, string label)> choices)
    {
        string value = property.stringValue;
        int selected = choices.FindIndex(choice => choice.id == value);
        var labels = new List<string> { string.IsNullOrEmpty(value) ? "선택하세요" : selected < 0 ? $"찾을 수 없음: {value}" : "선택하세요" };
        labels.AddRange(choices.Select(choice => choice.label));
        int next = EditorGUILayout.Popup(label, selected + 1, labels.ToArray());
        if (next > 0) property.stringValue = choices[next - 1].id;
        if (!string.IsNullOrEmpty(value) && selected < 0)
            EditorGUILayout.HelpBox($"등록되지 않은 {label} ID: {value}", MessageType.Warning);
    }
}
