using System;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Run;
using DuelHero.UI;
using UnityEngine;

namespace DuelHero.Map
{
    public sealed class RouteMapController : MonoBehaviour
    {
        [SerializeField] private uint previewSeed = 42;
        [SerializeField] private RouteMapView view;
        [SerializeField] private UnityEngine.UI.Button ownedCardsButton;
        [SerializeField] private EnemyCardListUI ownedCardsView;
        [SerializeField] private SpriteNumberUI ownedCardsCount;
        [SerializeField] private UnitDatabase unitDatabase;
        [SerializeField] private string playerUnitId;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private RunConfig runConfig;
        [SerializeField] private ShopConfig shopConfig;
        [SerializeField] private UnityEngine.UI.Text goldLabel;
        [SerializeField] private string shopScenePath = "Assets/00.Scenes/ShopScene.unity";
        [SerializeField] private string restScenePath = "Assets/00.Scenes/RestScene.unity";
        [SerializeField] private string battleScenePath = "Assets/00.Scenes/BattleScene.unity";

        private RunSession session;
        private CurrencyLabel goldDisplay;
        public RouteMap CurrentMap => session?.Map;
        public RouteMapProgress Progress => session?.Progress;
        public bool IsTransitioning { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            view.NodeClicked += HandleNodeClicked;
            view.RestartRequested += RestartRun;
            if (ownedCardsButton != null) ownedCardsButton.onClick.AddListener(ToggleOwnedCards);
        }

        private void OnDisable()
        {
            view.NodeClicked -= HandleNodeClicked;
            view.RestartRequested -= RestartRun;
            if (ownedCardsButton != null) ownedCardsButton.onClick.RemoveListener(ToggleOwnedCards);
            if (ownedCardsView != null) ownedCardsView.Close();
        }

        private void Start()
        {
            if (goldLabel != null)
                goldDisplay = new CurrencyLabel(goldLabel, Resources.Load<Sprite>("UI/Currency/money-pouch"), 36f);
            session = RunSessionStore.Current ?? CreateRun(CreateRandomSeed());
            RenderRun();
        }

        private void ToggleOwnedCards()
        {
            if (IsTransitioning || session == null || ownedCardsView == null) return;
            if (ownedCardsView.IsOpen) { ownedCardsView.Close(); return; }
            var cards = session.CardIds.Select(id =>
            {
                var definition = Array.Find(cardDatabase.cards, card => card != null && card.id == id);
                if (definition == null) throw new InvalidOperationException("존재하지 않는 카드 ID: " + id);
                return new CardInstance(definition);
            }).ToArray();
            ownedCardsView.OpenOwnedCards(cardDatabase, cards);
        }

        private void HandleNodeClicked(string nodeId)
        {
            if (!isActiveAndEnabled || IsTransitioning || session == null || (ownedCardsView != null && ownedCardsView.IsOpen) ||
                session.HasPendingNode || session.PlayerHealth <= 0 || !Progress.CanSelect(nodeId)) return;

            var type = CurrentMap.GetNode(nodeId).Type;
            if (type == RouteNodeType.Battle || type == RouteNodeType.Boss)
                EnterBattle(nodeId);
            else if (type == RouteNodeType.Shop)
                EnterShop(nodeId);
            else if (type == RouteNodeType.Rest)
                EnterRest(nodeId);
            else if (Progress.TrySelect(nodeId))
                RefreshNodeStates();
        }

        private void EnterBattle(string nodeId)
        {
            if (!Application.CanStreamedLevelBeLoaded(battleScenePath))
            {
                LastError = $"전투 씬을 불러올 수 없습니다. 빌드 씬 등록을 확인해주세요: {battleScenePath}";
                Debug.LogWarning(LastError, this);
                return;
            }
            if (!session.TryBeginBattle(nodeId)) return;

            IsTransitioning = true;
            LastError = null;
            RefreshNodeStates();
            try
            {
                MapSceneFade.Load(battleScenePath, RestoreBattleEntry);
            }
            catch (Exception error) { RestoreBattleEntry(error); }

            void RestoreBattleEntry(Exception error)
            {
                session.TryCancelBattle(nodeId);
                IsTransitioning = false;
                LastError = error.Message;
                RefreshNodeStates();
                Debug.LogWarning(LastError, this);
            }
        }

        private void EnterShop(string nodeId)
        {
            if (!Application.CanStreamedLevelBeLoaded(shopScenePath))
            { LastError = "상점 씬을 불러올 수 없습니다: " + shopScenePath; Debug.LogWarning(LastError, this); return; }
            try
            {
                if (!session.TryBeginShop(nodeId, cardDatabase.cards, shopConfig)) return;
                IsTransitioning = true; LastError = null; RefreshNodeStates();
                MapSceneFade.Load(shopScenePath, RestoreShopEntry);
            }
            catch (Exception error) { RestoreShopEntry(error); }

            void RestoreShopEntry(Exception error)
            {
                session.TryCancelShopEntry(nodeId); IsTransitioning = false; LastError = error.Message;
                RefreshNodeStates(); Debug.LogWarning(LastError, this);
            }
        }

        private void EnterRest(string nodeId)
        {
            if (!Application.CanStreamedLevelBeLoaded(restScenePath))
            { LastError = "휴식 씬을 불러올 수 없습니다: " + restScenePath; Debug.LogWarning(LastError, this); return; }
            if (!session.TryBeginRest(nodeId)) return;
            IsTransitioning = true;
            LastError = null;
            RefreshNodeStates();
            try { MapSceneFade.Load(restScenePath, RestoreEntry); }
            catch (Exception error) { RestoreEntry(error); }

            void RestoreEntry(Exception error)
            {
                session.TryCancelRestEntry(nodeId);
                IsTransitioning = false;
                LastError = error.Message;
                RefreshNodeStates();
                Debug.LogWarning(LastError, this);
            }
        }

        [ContextMenu("Generate fixed seed preview")]
        public void GeneratePreview()
        {
            if (IsTransitioning) return;
            session = CreateRun(previewSeed);
            RenderRun();
        }

        public void RestartRun()
        {
            if (IsTransitioning) return;
            session = CreateRun(CreateRandomSeed());
            RenderRun();
        }

        private uint CreateRandomSeed()
        {
            uint seed = unchecked((uint)System.Guid.NewGuid().GetHashCode());
            if (CurrentMap != null && seed == CurrentMap.Seed) seed = unchecked(seed + 1);
            return seed;
        }

        private RunSession CreateRun(uint seed)
        {
            if (unitDatabase == null || cardDatabase == null || runConfig == null)
                throw new InvalidOperationException("맵의 시작 유닛 DB와 카드 DB를 연결해주세요.");
            var player = Array.Find(unitDatabase.players, unit => unit != null && unit.id == playerUnitId);
            if (player == null)
                throw new InvalidOperationException($"플레이어 유닛 ID를 찾을 수 없습니다: {playerUnitId}");
            var cards = cardDatabase.StarterCards.Select(card => card.id).ToArray();
            // Editor previews must not replace a live runtime session.
            return Application.isPlaying
                ? RunSessionStore.StartNew(seed, player.startingHealth, player.maxHealth, cards, runConfig.startingGold)
                : new RunSession(seed, player.startingHealth, player.maxHealth, cards, runConfig.startingGold);
        }

        private void RenderRun()
        {
            if (ownedCardsView != null) ownedCardsView.Close();
            if (ownedCardsCount != null) ownedCardsCount.SetValue(session.CardIds.Count);
            LastError = null;
            view.Render(CurrentMap);
            RefreshNodeStates();
        }

        private void RefreshNodeStates()
        {
            if (goldDisplay != null) goldDisplay.SetAmount(session.Gold);
            else if (goldLabel != null) goldLabel.text = "골드  " + session.Gold;
            bool inputLocked = IsTransitioning || session.HasPendingNode || session.PlayerHealth <= 0;
            foreach (var node in CurrentMap.Nodes)
            {
                var state = node.Id == Progress.CurrentNodeId ? RouteNodeState.Current
                    : Progress.CompletedNodeIds.Contains(node.Id) ? RouteNodeState.Completed
                    : !inputLocked && Progress.CanSelect(node.Id) ? RouteNodeState.Available
                    : RouteNodeState.Locked;
                view.SetNodeState(node.Id, state);
            }
            view.SetCleared(Progress.IsComplete);
            view.SetRestartInteractable(!IsTransitioning);
        }
    }
}
