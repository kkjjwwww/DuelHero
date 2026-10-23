using System;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelHero.Map
{
    public sealed class RouteMapController : MonoBehaviour
    {
        [SerializeField] private uint previewSeed = 42;
        [SerializeField] private RouteMapView view;
        [SerializeField] private UnitDatabase unitDatabase;
        [SerializeField] private string playerUnitId;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private string battleScenePath = "Assets/00.Scenes/BattleScene.unity";

        private RunSession session;
        public RouteMap CurrentMap => session?.Map;
        public RouteMapProgress Progress => session?.Progress;
        public bool IsTransitioning { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            view.NodeClicked += HandleNodeClicked;
            view.RestartRequested += RestartRun;
        }

        private void OnDisable()
        {
            view.NodeClicked -= HandleNodeClicked;
            view.RestartRequested -= RestartRun;
        }

        private void Start()
        {
            session = RunSessionStore.Current ?? CreateRun(CreateRandomSeed());
            RenderRun();
        }

        private void HandleNodeClicked(string nodeId)
        {
            if (!isActiveAndEnabled || IsTransitioning || session == null ||
                session.PendingBattleNodeId != null || session.PlayerHealth <= 0 || !Progress.CanSelect(nodeId)) return;

            var type = CurrentMap.GetNode(nodeId).Type;
            if (type == RouteNodeType.Battle || type == RouteNodeType.Boss)
                EnterBattle(nodeId);
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
                if (SceneManager.LoadSceneAsync(battleScenePath, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException("전투 씬 로드 요청을 시작하지 못했습니다.");
            }
            catch (Exception error)
            {
                session.TryCancelBattle(nodeId);
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
            if (unitDatabase == null || cardDatabase == null)
                throw new InvalidOperationException("맵의 시작 유닛 DB와 카드 DB를 연결해주세요.");
            var player = Array.Find(unitDatabase.players, unit => unit != null && unit.id == playerUnitId);
            if (player == null)
                throw new InvalidOperationException($"플레이어 유닛 ID를 찾을 수 없습니다: {playerUnitId}");
            var cards = cardDatabase.StarterCards.Select(card => card.id).ToArray();
            // Editor previews must not replace a live runtime session.
            return Application.isPlaying
                ? RunSessionStore.StartNew(seed, player.startingHealth, player.maxHealth, cards)
                : new RunSession(seed, player.startingHealth, player.maxHealth, cards);
        }

        private void RenderRun()
        {
            LastError = null;
            view.Render(CurrentMap);
            RefreshNodeStates();
        }

        private void RefreshNodeStates()
        {
            bool inputLocked = IsTransitioning || session.PendingBattleNodeId != null || session.PlayerHealth <= 0;
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
