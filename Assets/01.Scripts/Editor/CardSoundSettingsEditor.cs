using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Audio;
using DuelHero.Cards;
using DuelHero.Battle;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardSoundSettings))]
public sealed class CardSoundSettingsEditor : Editor
{
    private static readonly string[] TimingLabels = { "일반 공격 발동", "돌진 출발", "적중 결과", "돌진 공격 이펙트 시작", "방어 자세 시작" };
    private int previewActor;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var database = serializedObject.FindProperty("cardDatabase");
        var catalog = serializedObject.FindProperty("soundCatalog");
        EditorGUILayout.PropertyField(database, new GUIContent("카드 데이터베이스"));
        EditorGUILayout.PropertyField(catalog, new GUIContent("효과음 목록"));
        previewActor = EditorGUILayout.Popup("연출 시간 기준", previewActor, new[] { "플레이어", "적" });

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
            var card = cardData?.cards?.FirstOrDefault(c => c != null && c.id == cardId.stringValue);
            bool duplicate = !string.IsNullOrEmpty(cardId.stringValue) && !used.Add(cardId.stringValue);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string cardLabel = card != null ? card.name : string.IsNullOrEmpty(cardId.stringValue) ? "카드 미선택" : $"찾을 수 없음: {cardId.stringValue}";
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, $"{cardLabel} · 효과음 {cues.arraySize}개" + (duplicate ? " · 중복 ID" : ""), true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("삭제", GUILayout.Width(55)))
            {
                entries.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();
            if (!entry.isExpanded)
            {
                EditorGUILayout.EndVertical();
                continue;
            }
            DrawSelection(cardId, "카드", cardChoices);
            if (duplicate)
                EditorGUILayout.HelpBox("같은 카드가 여러 번 등록되어 있습니다.", MessageType.Warning);

            card = cardData?.cards?.FirstOrDefault(c => c != null && c.id == cardId.stringValue);
            DrawCardEffects(card);
            DrawPresentationInfo(card);
            var availableTimings = AvailableTimings(card);
            EditorGUILayout.LabelField("공통 효과음 사용", EditorStyles.boldLabel);
            DrawCommonSound(entry, "suppressMovement", "이동 먼지음");
            DrawCommonSound(entry, "suppressDamage", "피격음");
            DrawCommonSound(entry, "suppressShield", "보호막 방어음");
            DrawCommonSound(entry, "suppressEnergy", "에너지 회복음");

            for (int j = 0; j < cues.arraySize; j++)
            {
                var cue = cues.GetArrayElementAtIndex(j);
                var timing = cue.FindPropertyRelative("timing");
                var soundId = cue.FindPropertyRelative("soundId");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string timingLabel = timing.enumValueIndex >= 0 && timing.enumValueIndex < TimingLabels.Length ? TimingLabels[timing.enumValueIndex] : "알 수 없는 시점";
                string soundLabel = string.IsNullOrEmpty(soundId.stringValue) ? "효과음 미선택" : soundId.stringValue;
                cue.isExpanded = EditorGUILayout.Foldout(cue.isExpanded, $"{timingLabel} · {soundLabel}", true, EditorStyles.foldoutHeader);
                if (GUILayout.Button("삭제", GUILayout.Width(55)))
                {
                    cues.DeleteArrayElementAtIndex(j);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                if (!cue.isExpanded)
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }
                DrawTiming(timing, availableTimings, card != null);
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
                cue.isExpanded = true;
                cue.FindPropertyRelative("timing").enumValueIndex = availableTimings.Count > 0 ? availableTimings[0] : 0;
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
            entry.isExpanded = true;
            entry.FindPropertyRelative("cardId").stringValue = "";
            entry.FindPropertyRelative("sounds").ClearArray();
            entry.FindPropertyRelative("suppressMovement").boolValue = false;
            entry.FindPropertyRelative("suppressDamage").boolValue = false;
            entry.FindPropertyRelative("suppressShield").boolValue = false;
            entry.FindPropertyRelative("suppressEnergy").boolValue = false;
        }
        EditorGUILayout.HelpBox("연출 시간은 열린 전투씬의 현재 설정을 기준으로 표시합니다.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawCommonSound(SerializedProperty entry, string field, string label)
    {
        var property = entry.FindPropertyRelative(field);
        property.boolValue = !EditorGUILayout.Toggle(label, !property.boolValue);
    }

    private void DrawPresentationInfo(CardDefinition card)
    {
        if (card == null) return;
        EditorGUILayout.LabelField("연출 시간 정보", EditorStyles.boldLabel);
        var controllers = Resources.FindObjectsOfTypeAll<BattleTurnController>()
            .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded && c.gameObject.scene.name == "BattleScene")
            .ToArray();
        if (controllers.Length != 1)
        {
            EditorGUILayout.HelpBox("전투씬을 열고 전투 컨트롤러가 하나인지 확인해주세요. 연출 시간을 읽을 수 없습니다.", MessageType.Info);
            return;
        }
        var controller = new SerializedObject(controllers[0]);
        var attack = controller.FindProperty(previewActor == 0 ? "playerAttackPresentation" : "enemyAttackPresentation")?.objectReferenceValue;
        var jump = controller.FindProperty(previewActor == 0 ? "playerJumpPresentation" : "enemyJumpPresentation")?.objectReferenceValue;
        var effects = card.effects ?? Array.Empty<CardEffectDefinition>();
        bool dash = effects.Any(e => e != null && e.IsDash && !e.IsCounter);
        bool normalMove = effects.Any(e => e != null && e.effectType == "move" && !e.IsDash);
        bool normalAttack = effects.Any(e => e != null && e.effectType == "damage" && (!dash || e.IsCounter));
        if (normalAttack || dash)
        {
            if (attack == null) EditorGUILayout.HelpBox("선택한 유닛의 공격 연출이 연결되지 않았습니다.", MessageType.Warning);
            else
            {
                var data = new SerializedObject(attack);
                if (normalAttack)
                {
                    float windup = data.FindProperty("windupDuration").floatValue;
                    float strike = data.FindProperty("strikeDuration").floatValue;
                    float recovery = data.FindProperty("recoveryDuration").floatValue;
                    EditorGUILayout.LabelField($"일반 공격: 준비 {windup:0.##}초 · 동작 {strike:0.##}초 · 복귀 {recovery:0.##}초");
                    EditorGUILayout.LabelField($"공격 발동: 공격 모션 시작 + {windup:0.##}초", EditorStyles.miniLabel);
                }
                if (dash)
                    EditorGUILayout.LabelField($"돌진 이동: 출발 후 {data.FindProperty("dashDuration").floatValue:0.##}초");
            }
        }
        if (normalMove)
        {
            if (jump == null) EditorGUILayout.HelpBox("선택한 유닛의 도약 연출이 연결되지 않았습니다.", MessageType.Warning);
            else
            {
                var data = new SerializedObject(jump);
                EditorGUILayout.LabelField($"도약: 준비 {data.FindProperty("takeoffDuration").floatValue:0.##}초 · 공중 {data.FindProperty("flightDuration").floatValue:0.##}초 · 착지 {data.FindProperty("landingDuration").floatValue:0.##}초");
                EditorGUILayout.LabelField("일반 이동은 칸마다, 긴 도약은 이동 전체에 한 번 적용됩니다.", EditorStyles.miniLabel);
            }
        }
        if (effects.Any(e => e != null && e.effectType == "damage"))
        {
            string lifetime = card.attackVfx switch
            {
                "dash_slash" => "약 0.39초",
                "sweep" => "약 0.78초",
                "thrust" => "약 0.26초",
                "whirlwind" => "약 0.45초",
                "thorn_throw" => "약 0.45초",
                "rift_charge" => "약 0.45초 (폭발 이펙트)",
                _ => null
            };
            EditorGUILayout.LabelField($"공격 이펙트: {(string.IsNullOrEmpty(card.attackVfx) ? "미지정" : card.attackVfx)}");
            EditorGUILayout.LabelField(lifetime == null ? "이펙트 유지 시간 정보 없음" : $"이펙트 오브젝트 유지: {lifetime}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("실제 파티클 표시 길이는 다를 수 있습니다. 연결 상태와 조건에 따라 이펙트가 생략될 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("적중 결과: 양쪽 준비 및 공격 연출 이후 (상황에 따라 변동)", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static List<int> AvailableTimings(CardDefinition card)
    {
        var result = new List<int>();
        if (card?.effects == null) return result;
        bool dash = card.effects.Any(e => e != null && e.IsDash && !e.IsCounter);
        bool baseDamage = card.effects.Any(e => e != null && e.effectType == "damage" && !e.IsCounter);
        bool counterDamage = card.effects.Any(e => e != null && e.effectType == "damage" && e.IsCounter);
        if ((!dash && baseDamage) || counterDamage) result.Add((int)CardSoundSettings.Timing.Attack);
        if (dash) result.Add((int)CardSoundSettings.Timing.DashLaunch);
        if (baseDamage || counterDamage) result.Add((int)CardSoundSettings.Timing.Hit);
        if (dash && baseDamage) result.Add((int)CardSoundSettings.Timing.DashImpact);
        if (card.effects.Any(e => e != null && e.effectType == "guard" && e.value > 0)) result.Add((int)CardSoundSettings.Timing.GuardPose);
        return result;
    }

    private static void DrawCardEffects(CardDefinition card)
    {
        if (card == null) return;
        EditorGUILayout.LabelField("카드 효과 및 연출 기준", EditorStyles.boldLabel);
        if (card.effects == null || card.effects.Length == 0)
        {
            EditorGUILayout.HelpBox("카드에 등록된 효과가 없습니다.", MessageType.Info);
            return;
        }
        bool dash = card.effects.Any(e => e != null && e.IsDash && !e.IsCounter);
        foreach (var effect in card.effects.Where(e => e != null).OrderBy(e => e.resolutionOrder))
        {
            string counter = effect.IsCounter ? " · 반격" : "";
            string detail = effect.effectType switch
            {
                "move" => $"이동 · {(effect.IsDash ? "돌진" : effect.IsLongJump ? "긴 도약" : "일반 이동")} · {effect.value}칸",
                "damage" => $"피해 · {effect.value}" + (effect.UsesMovementPath ? " · 이동 경로 기준" : ""),
                "guard" => $"보호막 · {effect.value}",
                "energyRestore" => $"에너지 회복 · {effect.value}",
                "healthRestore" => $"체력 회복 · {effect.value}",
                _ => effect.effectType
            };
            EditorGUILayout.LabelField($"{effect.resolutionOrder}. {detail}{counter}");
            if (effect.effectType == "guard" && effect.value > 0)
                EditorGUILayout.LabelField("   기준: 방어 자세 시작", EditorStyles.miniLabel);
            if (effect.IsDash && !effect.IsCounter)
                EditorGUILayout.LabelField("   기준: 돌진 출발", EditorStyles.miniLabel);
            if (effect.effectType == "damage")
                EditorGUILayout.LabelField(dash && !effect.IsCounter
                    ? "   기준: 돌진 공격 이펙트 시작 / 적중 결과"
                    : "   기준: 일반 공격 발동 / 적중 결과", EditorStyles.miniLabel);
        }
        if (dash)
            EditorGUILayout.HelpBox("돌진 이동과 공격 이펙트 사이에 상대 준비 연출과 일반 공격 연출이 있을 수 있습니다. 대기 시간은 상황에 따라 달라집니다.", MessageType.Info);
    }

    private static void DrawTiming(SerializedProperty timing, List<int> available, bool cardFound)
    {
        int current = timing.enumValueIndex;
        int index = available.IndexOf(current);
        var labels = new List<string> { index < 0 ? "현재 카드에서 사용할 수 없음" : "선택하세요" };
        labels.AddRange(available.Select(i => TimingLabels[i]));
        using (new EditorGUI.DisabledScope(available.Count == 0))
        {
            int next = EditorGUILayout.Popup("재생 시점", index + 1, labels.ToArray());
            if (next > 0) timing.enumValueIndex = available[next - 1];
        }
        if (index < 0)
            EditorGUILayout.HelpBox(cardFound ? "이 카드에서는 저장된 시점이 발생하지 않습니다. 설정은 보존됩니다." : "카드를 선택하면 사용할 수 있는 시점을 확인할 수 있습니다.", MessageType.Warning);
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
