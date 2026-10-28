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
            EditorGUILayout.LabelField("전투 종료", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(turn == null || !turn.CanWinForDebug))
            {
                if (GUILayout.Button("즉시 승리") && turn.TryWinForDebug()) return;
            }
            EditorGUILayout.Space(12);
            DrawCards(deck, queue, deckList, editable);
            EditorGUILayout.Space(12);
            DrawStats(queue.UnitStats, editable);
            if (!editable) EditorGUILayout.HelpBox("행동 실행 및 복원 중에는 조작할 수 없습니다.", MessageType.Info);
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

            EditorGUILayout.LabelField("현재 HP / 에너지", selectedUnit.Health + " / " + selectedUnit.Energy);
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
                }
            }
        }

        private void SelectUnit(UnitStats unit)
        {
            selectedUnit = unit;
            healthInput = unit.Health;
            energyInput = unit.Energy;
        }
    }
}
