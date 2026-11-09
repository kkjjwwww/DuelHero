using System;
using System.Collections.Generic;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Map;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Run
{
    public sealed class RestController : MonoBehaviour
    {
        [SerializeField] private RunConfig config;
        [SerializeField] private CardDatabase database;
        [SerializeField] private EnemyCardListUI deckPopup;
        [SerializeField] private GameObject choices, confirmation;
        [SerializeField] private Text status, message, selectedCardLabel;
        [SerializeField] private Button heal, remove, confirm, cancel, returnToMap;
        [SerializeField] private string mapScenePath = "Assets/00.Scenes/MapScene.unity";

        private RunSession run;
        private string nodeId;
        private bool completed, transitioning;
        private int selectedIndex = -1;
        private string selectedId;
        private bool IsCurrent => run != null && ReferenceEquals(run, RunSessionStore.Current);
        private bool CanChoose => IsCurrent && nodeId != null && run.PendingRestNodeId == nodeId && !completed && !transitioning;
        private bool CanRemove => CanChoose && run.CardIds.Count > 1;

        private void Awake()
        {
            run = RunSessionStore.Current;
            nodeId = run?.PendingRestNodeId;
        }

        private void OnEnable()
        {
            heal.onClick.AddListener(Heal);
            remove.onClick.AddListener(OpenDeck);
            confirm.onClick.AddListener(ConfirmRemoval);
            cancel.onClick.AddListener(CancelRemoval);
            returnToMap.onClick.AddListener(ReturnToMap);
            deckPopup.OpenChanged += Refresh;
        }

        private void OnDisable()
        {
            heal.onClick.RemoveListener(Heal);
            remove.onClick.RemoveListener(OpenDeck);
            confirm.onClick.RemoveListener(ConfirmRemoval);
            cancel.onClick.RemoveListener(CancelRemoval);
            returnToMap.onClick.RemoveListener(ReturnToMap);
            deckPopup.OpenChanged -= Refresh;
            deckPopup.Close();
        }

        private void Start()
        {
            message.text = CanChoose ? "" : "맵에서 휴식 노드에 진입해주세요.";
            Refresh();
        }

        private void Refresh()
        {
            status.text = IsCurrent ? $"HP {run.PlayerHealth}/{run.PlayerMaxHealth}    ·    휴식 회복량 {config.restHealAmount} HP    ·    골드 {run.Gold}" : "";
            bool showChoices = CanChoose && !deckPopup.IsOpen && !confirmation.activeSelf;
            choices.SetActive(showChoices);
            heal.interactable = showChoices;
            remove.interactable = showChoices && CanRemove;
            returnToMap.gameObject.SetActive(!transitioning && (completed || !CanChoose));
        }

        private void Heal()
        {
            if (!CanChoose || deckPopup.IsOpen || confirmation.activeSelf || !CanReturn()) return;
            if (run.TryHealAtRest(nodeId, config.restHealAmount)) FinishVisit();
        }

        private void OpenDeck()
        {
            if (!CanRemove || deckPopup.IsOpen || confirmation.activeSelf) return;
            var cards = new List<CardInstance>();
            var indices = new Dictionary<CardInstance, int>();
            for (int i = 0; i < run.CardIds.Count; i++)
            {
                string id = run.CardIds[i];
                if (id == WaitCard.Definition.id) continue;
                var definition = Array.Find(database.cards, card => card != null && card.id == id);
                if (definition == null)
                {
                    message.text = "카드 데이터를 찾을 수 없습니다: " + id;
                    return;
                }
                var instance = new CardInstance(definition);
                cards.Add(instance);
                indices.Add(instance, i);
            }
            message.text = "";
            deckPopup.OpenOwnedCards(database, cards, card =>
            {
                if (!CanRemove || !indices.TryGetValue(card, out int index)) return;
                selectedIndex = index;
                selectedId = card.Definition.id;
                selectedCardLabel.text = $"「{card.Definition.name}」 한 장을 제거할까요?\n제거하면 {RunSession.RestCardRemovalGold}골드를 얻습니다.";
                confirmation.SetActive(true);
                deckPopup.Close();
            });
        }

        private void CancelRemoval()
        {
            selectedIndex = -1;
            selectedId = null;
            confirmation.SetActive(false);
            Refresh();
        }

        private void ConfirmRemoval()
        {
            if (!CanRemove || !confirmation.activeSelf || !CanReturn()) return;
            if (run.TryRemoveCardAtRest(nodeId, selectedIndex, selectedId))
                FinishVisit();
            else
            {
                CancelRemoval();
                message.text = "선택한 카드를 제거할 수 없습니다.";
            }
        }

        private void FinishVisit()
        {
            completed = true;
            confirmation.SetActive(false);
            deckPopup.Close();
            ReturnToMap();
        }

        private bool CanReturn()
        {
            if (Application.CanStreamedLevelBeLoaded(mapScenePath)) return true;
            message.text = "맵 씬을 불러올 수 없습니다. 빌드 씬 등록을 확인해주세요.";
            return false;
        }

        private void ReturnToMap()
        {
            if (transitioning || (!completed && CanChoose) || !CanReturn()) return;
            transitioning = true;
            message.text = "";
            Refresh();
            try { MapSceneFade.Load(mapScenePath, RestoreReturn); }
            catch (Exception error) { RestoreReturn(error); }

            void RestoreReturn(Exception error)
            {
                transitioning = false;
                message.text = "맵 복귀에 실패했습니다. 다시 시도해주세요.";
                Debug.LogWarning(error.Message, this);
                Refresh();
            }
        }
    }
}
