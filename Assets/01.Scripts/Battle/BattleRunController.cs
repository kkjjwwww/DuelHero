using System;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Map;
using DuelHero.Run;
using DuelHero.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelHero.Battle
{
    public sealed class BattleRunController : MonoBehaviour
    {
        [SerializeField] private BattleTurnController source;
        [SerializeField] private BattleResultUI view;
        [SerializeField] private UnitStats playerStats;
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private BattleRestartController standaloneRestart;
        [SerializeField] private CardRewardSettings rewardSettings;
        [SerializeField] private CardRewardUI rewardView;
        [SerializeField] private string mapScenePath = "Assets/00.Scenes/MapScene.unity";

        private RunSession run;
        private string battleNodeId;
        private BattleOutcome outcome;
        private bool processed;
        private bool transitioning;
        private bool rewardPending;
        private int victoryHealth;
        private CardDefinition[] rewardOptions = Array.Empty<CardDefinition>();
        public string LastError { get; private set; }

        private void Awake()
        {
            var current = RunSessionStore.Current;
            if (current?.PendingBattleNodeId == null) return;
            run = current;
            battleNodeId = current.PendingBattleNodeId;
        }

        private void OnEnable()
        {
            source.BattleFinished += HandleFinished;
            view.ActionRequested += HandleAction;
            if (rewardView != null) rewardView.CardSelected += HandleRewardSelected;
            HandleFinished(source.Outcome);
            RefreshView();
        }

        private void OnDisable()
        {
            source.BattleFinished -= HandleFinished;
            view.ActionRequested -= HandleAction;
            if (rewardView != null) rewardView.CardSelected -= HandleRewardSelected;
        }

        private void HandleFinished(BattleOutcome result)
        {
            if (result == BattleOutcome.Running || processed || rewardPending) return;
            outcome = result;
            try
            {
                if (run != null)
                {
                    if (!ReferenceEquals(run, RunSessionStore.Current) || run.PendingBattleNodeId != battleNodeId)
                        throw new InvalidOperationException("전투를 시작한 런 또는 노드가 변경되었습니다.");
                    if (playerStats == null || !playerStats.IsInitialized || playerDeck == null)
                        throw new InvalidOperationException("전투 결과에 필요한 플레이어 상태와 덱을 확인해주세요.");
                    var cards = playerDeck.Cards.Select(card => card.Definition.id).ToArray();
                    if (result == BattleOutcome.Victory)
                    {
                        if (run.Map.GetNode(battleNodeId).Type == RouteNodeType.Battle)
                        {
                            if (rewardView == null || rewardSettings == null)
                                throw new InvalidOperationException("카드 보상 UI와 추첨 설정을 연결해주세요.");
                            victoryHealth = playerStats.Health;
                            rewardOptions = CardRewardSelector.Generate(playerDeck.Database.cards, cards,
                                rewardSettings, run.Map.Seed, battleNodeId);
                            rewardView.Show(rewardOptions, playerDeck.Database);
                            rewardPending = true;
                        }
                        else if (!run.TryCompleteBattle(playerStats.Health, cards))
                            throw new InvalidOperationException("전투 승리 결과를 런에 반영하지 못했습니다.");
                    }
                    else
                        run.SavePlayerState(playerStats.Health, cards);
                }
                processed = !rewardPending;
                LastError = null;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                Debug.LogError(LastError, this);
            }
            RefreshView();
        }

        private void HandleAction()
        {
            if (rewardPending) { ResolveReward(null); return; }
            if (!processed || transitioning) return;
            if (run != null && !ReferenceEquals(run, RunSessionStore.Current)) return;
            if (run != null && !Application.CanStreamedLevelBeLoaded(mapScenePath))
            {
                LastError = $"맵 씬을 불러올 수 없습니다: {mapScenePath}";
                Debug.LogWarning(LastError, this);
                return;
            }
            transitioning = true;
            LastError = null;
            RefreshView();
            try
            {
                if (run == null)
                    standaloneRestart.Restart();
                else
                {
                    if (SceneManager.LoadSceneAsync(mapScenePath, LoadSceneMode.Single) == null)
                        throw new InvalidOperationException("맵 씬 로드를 시작하지 못했습니다.");
                    // Keep the defeated run until loading has actually been requested.
                    if (outcome != BattleOutcome.Victory) RunSessionStore.Clear();
                }
            }
            catch (Exception error)
            {
                transitioning = false;
                LastError = error.Message;
                Debug.LogWarning(LastError, this);
                RefreshView();
            }
        }

        private void HandleRewardSelected(string cardId)
        {
            if (rewardPending && rewardOptions.Any(card => card.id == cardId)) ResolveReward(cardId);
        }

        private void ResolveReward(string cardId)
        {
            if (!rewardPending || transitioning) return;
            rewardView.SetInteractable(false);
            CardInstance added = null;
            try
            {
                if (!ReferenceEquals(run, RunSessionStore.Current) || run.PendingBattleNodeId != battleNodeId)
                    throw new InvalidOperationException("보상을 지급할 런 또는 전투 노드가 변경되었습니다.");
                if (cardId != null) added = playerDeck.AddCard(cardId);
                if (!run.TryCompleteBattle(victoryHealth, playerDeck.Cards.Select(card => card.Definition.id)))
                    throw new InvalidOperationException("카드 보상과 승리 결과를 런에 반영하지 못했습니다.");
            }
            catch (Exception error)
            {
                if (added != null) playerDeck.RemoveCard(added);
                LastError = error.Message;
                Debug.LogWarning(LastError, this);
                rewardView.SetInteractable(true);
                RefreshView();
                return;
            }
            rewardPending = false;
            processed = true;
            rewardView.Hide();
            RefreshView();
            HandleAction();
        }

        private void RefreshView()
        {
            string label = rewardPending && rewardOptions.Length > 0 ? "건너뛰기"
                : run != null && outcome == BattleOutcome.Victory ? "계속" : "재시작";
            view.Show(outcome, label, (processed || rewardPending) && !transitioning &&
                (run != null || standaloneRestart != null));
        }
    }
}
