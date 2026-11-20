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
        private static readonly string[] Statuses = { "기절", "출혈", "예지" };
        private readonly List<CardInstance> addedCards = new();
        private int categoryIndex;
        private int cardIndex;
        private int targetCount = 12;
        private UnitStats selectedUnit;
        private int healthInput;
        private int energyInput;
        private int statusIndex;
        private int statusAmount = 1;
        private int statAmount = 10;
        private int damageAmount = 10;
        private int guardSlots = 1;
        private string statResult;
        private Vector2 scrollPosition;
        private int enemySlot, enemyCard, enemyDirection;
        private string enemyReservationResult;
        private int enemyKind;
        private string enemyChangeResult;

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
            DrawStatusDebug(queue, turn);
            DrawEnemyChange(turn);
            DrawEnemyReservation(turn);
            EditorGUILayout.Space(12);
            DrawCards(deck, queue, deckList, editable);
            DrawCardTimers(deck, queue.CurrentRound, editable && turn != null && turn.CanEditForDebug);
            EditorGUILayout.Space(12);
            DrawStats(queue.UnitStats, editable);
            if (!editable) EditorGUILayout.HelpBox("행동 실행 및 복원 중에는 조작할 수 없습니다.", MessageType.Info);
        }

        private void DrawCardTimers(PlayerDeck deck, int round, bool editable)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("보유 카드 대기시간", EditorStyles.boldLabel);
            for (int i = 0; i < deck.Cards.Count; i++)
            {
                var card = deck.Cards[i];
                if (card.IsExhausted) continue;
                int preparation = card.RemainingPreparation(round);
                int cooldown = card.RemainingCooldown(round);
                using var row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField($"{i + 1}. {card.Definition.name}");
                using (new EditorGUI.DisabledScope(!editable || preparation == 0))
                    if (GUILayout.Button($"준비 건너뛰기 ({preparation}턴)"))
                    {
                        card.SkipPreparationForDebug();
                        deck.NotifyStateChanged();
                    }
                using (new EditorGUI.DisabledScope(!editable || cooldown == 0))
                    if (GUILayout.Button($"쿨다운 초기화 ({cooldown}턴)"))
                    {
                        card.ResetCooldownForDebug();
                        deck.NotifyStateChanged();
                    }
            }
        }

        private void DrawEnemyChange(BattleTurnController turn)
        {
            var initializer = turn != null ? turn.EnemyInitializerForDebug : null;
            var choices = initializer?.DebugEnemyChoices.Where(choice => turn.CanSelectEnemyForDebug(choice.id)).ToArray();
            if (choices == null || choices.Length == 0) return;
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("적 종류 변경", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("현재 적", initializer.EncounterName ?? "알 수 없음");
            enemyKind = EditorGUILayout.Popup("변경할 적", Mathf.Clamp(enemyKind, 0, choices.Length - 1),
                choices.Select(choice => $"{choice.name} ({choice.id})").ToArray());
            using (new EditorGUI.DisabledScope(!turn.CanEditForDebug))
                if (GUILayout.Button("적 교체"))
                    enemyChangeResult = turn.TryChangeEnemyForDebug(choices[enemyKind].id, out var error) ? "적을 교체했습니다." : error;
            if (!string.IsNullOrEmpty(enemyChangeResult)) EditorGUILayout.HelpBox(enemyChangeResult, MessageType.Info);
            EditorGUILayout.LabelField("적의 능력치·상태·행동 예약이 초기화됩니다.", EditorStyles.wordWrappedMiniLabel);
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

        private void DrawStatusDebug(CardReservationQueue queue, BattleTurnController turn)
        {
            EditorGUILayout.LabelField("버프 디버프 부여", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(turn == null || !turn.CanEditForDebug))
            {
                statusIndex = EditorGUILayout.Popup("버프·디버프", statusIndex, Statuses);
                statusAmount = Mathf.Clamp(EditorGUILayout.IntField("부여할 수치", statusAmount), 1, statusIndex == 2 ? 3 : 99);
                var player = queue.UnitStats;
                using (new EditorGUI.DisabledScope(player == null || !player.IsInitialized))
                    DrawStatusButton(turn, player, "플레이어");
                if (statusIndex == 2) return;
                foreach (var unit in FindObjectsByType<UnitStats>(FindObjectsSortMode.None))
                {
                    if (unit == player || !unit.IsInitialized) continue;
                    DrawStatusButton(turn, unit, unit.gameObject.name);
                }
            }
        }

        private void DrawStatusButton(BattleTurnController turn, UnitStats unit, string targetName)
        {
            bool isBleed = statusIndex == 1;
            int current = (statusIndex == 2 ? unit?.PendingForesight : isBleed ? unit?.Bleed : unit?.Stun) ?? 0;
            if (!GUILayout.Button($"{targetName}에게 {Statuses[statusIndex]} 부여 · 현재 {current}")) return;
            if (statusIndex == 2) unit.ApplyForesight(statusAmount);
            else if (isBleed) turn.TryApplyBleedForDebug(unit, statusAmount);
            else turn.TryApplyStunForDebug(unit, statusAmount);
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
            var target = units[nextIndex];
            if (target != selectedUnit) SelectUnit(target);

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
                damageAmount = Mathf.Max(1, EditorGUILayout.IntField("체력 피해량", damageAmount));
                statAmount = Mathf.Max(1, EditorGUILayout.IntField("변화량", statAmount));
                guardSlots = Mathf.Max(1, EditorGUILayout.IntField("보호막 지속 슬롯", guardSlots));
                var unit = target;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("체력 피해"))
                        ApplyDebugDamage(unit);
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

        private void ApplyDebugDamage(UnitStats unit)
        {
            if (unit == null || !unit.IsInitialized)
            {
                statResult = "체력 피해를 적용할 수 없습니다. 초기화된 전투 유닛을 선택해 주세요.";
                return;
            }
            string beforeStats = FormatStats(unit);
            int before = unit.Health;
            int displayDamage = Mathf.Max(0, damageAmount - unit.GuardReduction);
            unit.TakeDamage(damageAmount);
            int damage = before - unit.Health;
            healthInput = unit.Health;
            energyInput = unit.Energy;
            statResult = $"체력 피해 · 요청 {damageAmount}, 실제 {damage}\n이전  {beforeStats}\n이후  {FormatStats(unit)}";
            if (damage > 0)
                FindFirstObjectByType<BattleDamageNumberUI>()?.ShowDamageForDebug(unit.transform, damage, displayDamage, unit.Health <= 0);
        }

        private void ApplyStatChange(string label, Action apply)
        {
            var unit = selectedUnit;
            if (unit == null || !unit.IsInitialized)
            {
                statResult = "초기화된 전투 유닛을 선택해 주세요.";
                return;
            }
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
