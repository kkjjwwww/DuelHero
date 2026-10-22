using System.Linq;
using UnityEngine;

namespace DuelHero.Map
{
    public sealed class RouteMapController : MonoBehaviour
    {
        [SerializeField] private uint previewSeed = 42;
        [SerializeField] private RouteMapView view;

        public RouteMap CurrentMap { get; private set; }
        public RouteMapProgress Progress { get; private set; }

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

        private void Start() => GeneratePreview();

        private void HandleNodeClicked(string nodeId)
        {
            if (Progress != null && Progress.TrySelect(nodeId)) RefreshNodeStates();
        }

        [ContextMenu("Generate fixed seed preview")]
        public void GeneratePreview() => GenerateMap(previewSeed);

        public void RestartRun()
        {
            uint seed = unchecked((uint)System.Guid.NewGuid().GetHashCode());
            if (CurrentMap != null && seed == CurrentMap.Seed) seed = unchecked(seed + 1);
            GenerateMap(seed);
        }

        private void GenerateMap(uint seed)
        {
            CurrentMap = RouteMapGenerator.Generate(seed);
            Progress = new RouteMapProgress(CurrentMap);
            view.Render(CurrentMap);
            RefreshNodeStates();
        }

        private void RefreshNodeStates()
        {
            foreach (var node in CurrentMap.Nodes)
            {
                var state = node.Id == Progress.CurrentNodeId ? RouteNodeState.Current
                    : Progress.CompletedNodeIds.Contains(node.Id) ? RouteNodeState.Completed
                    : Progress.CanSelect(node.Id) ? RouteNodeState.Available
                    : RouteNodeState.Locked;
                view.SetNodeState(node.Id, state);
            }
            view.SetCleared(Progress.IsComplete);
        }
    }
}
