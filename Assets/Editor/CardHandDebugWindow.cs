using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEditor;
using UnityEngine;

namespace DuelHero.EditorTools
{
    public sealed class CardHandDebugWindow : EditorWindow
    {
        private static readonly string[] Categories = { "공격", "이동", "방어", "회복", "특수" };
        private readonly List<CardInstance> addedCards = new();
        private int categoryIndex;
        private int cardIndex;
        private int targetCount = 12;
        private UnitStats selectedUnit;
        private int healthInput;
        private int energyInput;
        private int stunInput = 1;
        private int statAmount = 10;
        private int guardSlots = 1;
        private string statResult;
        private Vector2 scrollPosition;
        private int enemySlot, enemyCard, enemyDirection;
        private string enemyReservationResult;

        [MenuItem("Duel Hero/Debug/Card Hand and Stats")]
        private static void Open() => GetWindow<CardHandDebugWindow>("전투 디버그");

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            addedCards.Clear();
            selectedUnit = null;
            Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 중에 사용할 수 있습니다.", MessageType.Info);
                return;
            }

            var deck = FindFirstObjectByType<PlayerDeck>();
            var queue = FindFirstObjectByType<CardReservationQueue>();
            var deckList = FindFirstObjectByType<DeckListUI>();
            var turn = FindFirstObjectByType<BattleTurnController>();
            if (deck == null || queue == null || deckList == null)
            {
                EditorGUILayout.HelpBox("전투 씬의 덱, 예약 슬롯, 카드 목록을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            bool editable = !queue.IsExecuting && (turn == null || !turn.IsInputLocked);
            using var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition);
            scrollPosition = scroll.scrollPosition;
            EditorGUILayout.LabelField("전투 종료", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(turn == null || !turn.CanWinForDebug))
            {
                if (GUILayout.Button("즉시 승리") && turn.TryWinForDebug()) return;
            }
            EditorGUILayout.Space(12);
            DrawStunDebug(queue, turn);
            DrawEnemyReservation(turn);
            EditorGUILayout.Space(12);
            DrawCards(deck, queue, deckList, editable);
            EditorGUILayout.Space(12);
            DrawStats(queue.UnitStats, editable);
            if (!editable) EditorGUILayout.HelpBox("행동 실행 및 복원 중에는 조작할 수 없습니다.", MessageType.Info);
        }

        private void DrawEnemyReservation(BattleTurnController turn)
        {
            var provider = turn != null ? turn.EnemyPlanForDebug : null;
            if (provider == null || turn.PreparedEnemyCards.Count == 0) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("적 행동 예약", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Join(" → ", turn.PreparedEnemyCards.Select(card => card.name)));
            using var disabled = new EditorGUI.DisabledScope(!turn.CanEditForDebug);
            var cards = provider.Cards;
            var labels = new[] { "대기" }.Concat(cards.Select(card => card.Definition.name)).ToArray();
            enemySlot = EditorGUILayout.Popup("슬롯", enemySlot, new[] { "1", "2", "3" });
            enemyCard = EditorGUILayout.Popup("카드", Mathf.Clamp(enemyCard, 0, labels.Length - 1), labels);
            var selected = enemyCard == 0 ? null : cards[enemyCard - 1];
            var direction = Vector2Int.right;
            if (selected != null && CardDirectionRules.CanSelect(selected.Definition))
            {
                enemyDirection = EditorGUILayout.Popup("방향", enemyDirection, new[] { "오른쪽", "위", "왼쪽", "아래" });
                direction = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down }[enemyDirection];
            }
            if (GUILayout.Button("선택한 슬롯에 예약"))
            {
                try
                {
                    turn.ReserveEnemyCardForDebug(enemySlot, selected, direction);
                    enemyReservationResult = "예약했습니다.";
                }
                catch (ArgumentException error) { enemyReservationResult = error.Message; }
                catch (InvalidOperationException error) { enemyReservationResult = error.Message; }
            }
            if (!string.IsNullOrEmpty(enemyReservationResult))
                EditorGUILayout.HelpBox(enemyReservationResult, MessageType.Info);
        }

        private void DrawStunDebug(CardReservationQueue queue, BattleTurnController turn)
        {
            EditorGUILayout.LabelField("기절 테스트", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(turn == null || !turn.CanEditForDebug))
            {
                stunInput = Mathf.Clamp(EditorGUILayout.IntField("부여할 기절 수치", stunInput), 1, 99);
                var player = queue.UnitStats;
                using (new EditorGUI.DisabledScope(player == null || !player.IsInitialized))
                    if (GUILayout.Button("플레이어에게 기절 부여 · 현재 " + (player != null ? player.Stun : 0)))
                        turn.TryApplyStunForDebug(player, stunInput);
                foreach (var unit in FindObjectsByType<UnitStats>(FindObjectsSortMode.None))
                {
                    if (unit == player || !unit.IsInitialized) continue;
                    if (GUILayout.Button(unit.gameObject.name + "에게 기절 부여 · 현재 " + unit.Stun))
                        turn.TryApplyStunForDebug(unit, stunInput);
                }
            }
        }

        private void DrawCards(PlayerDeck deck, CardReservationQueue queue, DeckListUI deckList, bool editable)
        {
            EditorGUILayout.LabelField("손패 카드", EditorStyles.boldLabel);
            int nextCategory = EditorGUILayout.Popup("카테고리", categoryIndex, Categories);
            if (nextCategory != categoryIndex) { categoryIndex = nextCategory; cardIndex = 0; }

            var cards = deck.Database == null ? Array.Empty<CardDefinition>() :
                deck.Database.cards.Where(card => card != null && card.actionType == Categories[categoryIndex]).ToArray();
            if (cards.Length == 0)
            {
                EditorGUILayout.HelpBox("선택한 카테고리에 카드 데이터가 없습니다.", MessageType.Info);
                return;
            }

            cardIndex = Mathf.Clamp(cardIndex, 0, cards.Length - 1);
            cardIndex = EditorGUILayout.Popup("추가할 카드", cardIndex,
                cards.Select(card => card.name + " (" + card.id + ")").ToArray());
            targetCount = Mathf.Max(1, EditorGUILayout.IntField("표시할 장수", targetCount));
            int currentCount = deckList.CountCategory(Categories[categoryIndex]);
            EditorGUILayout.LabelField("현재 장수", currentCount.ToString());

            using (new EditorGUI.DisabledScope(!editable || deckList.IsTransitioning))
            {
                if (GUILayout.Button("선택한 카드 1장 추가"))
                {
                    addedCards.Add(deck.AddCard(cards[cardIndex].id));
                    if (deckList.SelectedCategory != Categories[categoryIndex]) deckList.ToggleCategory(Categories[categoryIndex]);
                }
                if (GUILayout.Button("선택한 카테고리를 목표 장수까지 채우기"))
                {
                    for (int i = currentCount; i < targetCount; i++) addedCards.Add(deck.AddCard(cards[cardIndex].id));
                    if (deckList.SelectedCategory != Categories[categoryIndex]) deckList.ToggleCategory(Categories[categoryIndex]);
                }
                using (new EditorGUI.DisabledScope(addedCards.Count == 0))
                {
                    if (GUILayout.Button("추가한 테스트 카드만 제거")) RemoveAddedCards(deck, queue);
                }
            }
            EditorGUILayout.LabelField("추가한 테스트 카드", addedCards.Count.ToString());
        }

        private void RemoveAddedCards(PlayerDeck deck, CardReservationQueue queue)
        {
            for (int i = addedCards.Count - 1; i >= 0; i--)
            {
                var card = addedCards[i];
                if (card.ReservedSlot >= 0 && !queue.CancelAt(card.ReservedSlot)) continue;
                if (deck.RemoveCard(card)) addedCards.RemoveAt(i);
            }
        }

        private void DrawStats(UnitStats player, bool editable)
        {
            EditorGUILayout.LabelField("유닛 상태", EditorStyles.boldLabel);
            if (player == null || !player.IsInitialized)
            {
                EditorGUILayout.HelpBox("플레이어 상태를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            var units = new List<UnitStats> { player };
            foreach (var unit in FindObjectsByType<UnitStats>(FindObjectsSortMode.None))
                if (unit != player && unit.IsInitialized) units.Add(unit);
            int selectedIndex = units.IndexOf(selectedUnit);
            if (selectedIndex < 0) { selectedIndex = 0; SelectUnit(player); }
            int nextIndex = EditorGUILayout.Popup("대상", selectedIndex,
                units.Select((unit, index) => (index == 0 ? "플레이어 · " : "적 · ") + unit.gameObject.name).ToArray());
            if (nextIndex != selectedIndex) SelectUnit(units[nextIndex]);

            EditorGUILayout.LabelField("현재 상태", FormatStats(selectedUnit));
            using (new EditorGUI.DisabledScope(!editable))
            {
                healthInput = EditorGUILayout.IntField("HP (최대 " + selectedUnit.MaxHealth + ")", healthInput);
                energyInput = EditorGUILayout.IntField("에너지 (최대 " + selectedUnit.MaxEnergy + ")", energyInput);
                if (GUILayout.Button("HP·에너지 적용"))
                {
                    int health = Mathf.Clamp(healthInput, 0, selectedUnit.MaxHealth);
                    int energy = Mathf.Clamp(energyInput, 0, selectedUnit.MaxEnergy);
                    selectedUnit.SetHealthForDebug(health);
                    if (energy < selectedUnit.Energy) selectedUnit.TrySpendEnergy(selectedUnit.Energy - energy);
                    else selectedUnit.RestoreEnergy(energy - selectedUnit.Energy);
                    healthInput = selectedUnit.Health;
                    energyInput = selectedUnit.Energy;
                    statResult = null;
                }

                EditorGUILayout.Space();
                statAmount = Mathf.Max(1, EditorGUILayout.IntField("변화량", statAmount));
                guardSlots = Mathf.Max(1, EditorGUILayout.IntField("보호막 지속 슬롯", guardSlots));
                var unit = selectedUnit;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("체력 피해"))
                        ApplyStatChange("체력 피해", () => unit.TakeDamage(statAmount));
                    if (GUILayout.Button("체력 회복"))
                        ApplyStatChange("체력 회복", () => unit.Heal(statAmount));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("에너지 소모"))
                    {
                        if (statAmount > unit.Energy)
                            statResult = $"에너지 소모 실패 · 요청 {statAmount}, 현재 {unit.Energy}";
                        else
                            ApplyStatChange("에너지 소모", () => unit.TrySpendEnergy(statAmount));
                    }
                    if (GUILayout.Button("에너지 회복"))
                        ApplyStatChange("에너지 회복", () => unit.RestoreEnergy(statAmount));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("보호막 생성"))
                        ApplyStatChange("보호막 생성", () => unit.ApplyGuard(statAmount, guardSlots));
                    using (new EditorGUI.DisabledScope(unit.GuardReduction == 0))
                    {
                        if (GUILayout.Button("보호막 소모"))
                            ApplyStatChange("보호막 소모", () => unit.TakeDamage(Mathf.Min(statAmount, unit.GuardReduction)));
                    }
                }
            }
            if (!string.IsNullOrEmpty(statResult))
                EditorGUILayout.HelpBox(statResult, MessageType.Info);
        }

        private void ApplyStatChange(string label, Action apply)
        {
            var unit = selectedUnit;
            string before = FormatStats(unit);
            apply();
            healthInput = unit.Health;
            energyInput = unit.Energy;
            statResult = $"{label}\n이전  {before}\n이후  {FormatStats(unit)}";
        }

        private static string FormatStats(UnitStats unit) =>
            $"HP {unit.Health}/{unit.MaxHealth} · 에너지 {unit.Energy}/{unit.MaxEnergy} · 보호막 {unit.GuardReduction}";

        private void SelectUnit(UnitStats unit)
        {
            selectedUnit = unit;
            healthInput = unit.Health;
            energyInput = unit.Energy;
            statResult = null;
        }
    }
}
