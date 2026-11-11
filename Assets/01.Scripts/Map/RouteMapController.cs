using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Run;
using DuelHero.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private bool drawingMap;
        private Coroutine revealRoutine;
        private Tween revealTween;
        public RouteMap CurrentMap => session?.Map;
        public RouteMapProgress Progress => session?.Progress;
        public bool IsTransitioning { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            view.NodeClicked += HandleNodeClicked;
            view.RestartRequested += RestartRun;
            if (ownedCardsButton != null) ownedCardsButton.onClick.AddListener(ToggleOwnedCards);
            if (session != null) RefreshNodeStates();
        }

        private void OnDisable()
        {
            view.NodeClicked -= HandleNodeClicked;
            view.RestartRequested -= RestartRun;
            if (ownedCardsButton != null) ownedCardsButton.onClick.RemoveListener(ToggleOwnedCards);
            if (ownedCardsView != null) ownedCardsView.Close();
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = null;
            revealTween?.Kill();
            revealTween = null;
            if (drawingMap) view.SetRevealProgress(1f);
            drawingMap = false;
        }

        private void Start()
        {
            if (goldLabel != null)
                goldDisplay = CurrencyLabel.CreateGoldBalance(goldLabel);
            session = RunSessionStore.Current;
            bool newMap = session == null;
            session ??= CreateRun(CreateRandomSeed());
            RenderRun(newMap);
        }

        private void ToggleOwnedCards()
        {
            if (IsTransitioning || drawingMap || session == null || cardDatabase == null || ownedCardsView == null) return;
            ownedCardsView.ToggleRunDeck(cardDatabase, session.CardIds);
        }

        private void Update()
        {
            if (session == null) return;
            var mouse = Mouse.current;
            var pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool pointerInside = pointer.x >= 0f && pointer.y >= 0f &&
                pointer.x <= Screen.width && pointer.y <= Screen.height;
            bool followPointer = mouse != null && pointerInside && Application.isFocused &&
                !IsTransitioning && !drawingMap && (ownedCardsView == null || !ownedCardsView.IsOpen);
            var normalizedPointer = new Vector2(pointer.x / Screen.width, pointer.y / Screen.height);
            view.PositionMap(normalizedPointer, followPointer);
        }

        private void HandleNodeClicked(string nodeId)
        {
            if (!isActiveAndEnabled || IsTransitioning || drawingMap || session == null || (ownedCardsView != null && ownedCardsView.IsOpen) ||
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
            if (IsTransitioning || drawingMap) return;
            session = CreateRun(previewSeed);
            RenderRun();
        }

        public void RestartRun()
        {
            if (IsTransitioning || drawingMap) return;
            session = CreateRun(CreateRandomSeed());
            RenderRun(Application.isPlaying);
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

        private void RenderRun(bool reveal = false)
        {
            if (ownedCardsView != null) ownedCardsView.Close();
            if (ownedCardsCount != null) ownedCardsCount.SetValue(session.CardIds.Count);
            LastError = null;
            view.Render(CurrentMap);
            view.FocusNode(Progress.CurrentNodeId ?? CurrentMap.StartNodeId);
            drawingMap = reveal;
            view.SetRevealProgress(reveal ? 0f : 1f);
            RefreshNodeStates();
            if (reveal) revealRoutine = StartCoroutine(RevealMapAfterFade());
        }

        private IEnumerator RevealMapAfterFade()
        {
            while (MapSceneFade.IsInProgress) yield return null;

            revealTween = DOVirtual.Float(0f, 1f, 1.2f, view.SetRevealProgress)
                .SetEase(Ease.Linear).SetUpdate(true);
            yield return revealTween.WaitForCompletion();

            revealRoutine = null;
            revealTween = null;
            drawingMap = false;
            view.SetRevealProgress(1f);
            RefreshNodeStates();
        }

        private void RefreshNodeStates()
        {
            if (goldDisplay != null) goldDisplay.SetAmount(session.Gold);
            else if (goldLabel != null) goldLabel.text = "골드  " + session.Gold;
            bool inputLocked = IsTransitioning || drawingMap || session.HasPendingNode || session.PlayerHealth <= 0;
            view.RefreshProgress(Progress, inputLocked);
            view.SetCleared(Progress.IsComplete);
            view.SetRestartInteractable(!IsTransitioning && !drawingMap);
        }
    }
}
