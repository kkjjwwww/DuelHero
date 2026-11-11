using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Map
{
    public sealed class RouteMapView : MonoBehaviour
    {
        [SerializeField] private RectTransform nodeLayer;
        [SerializeField] private RouteNodeView nodePrefab;
        [SerializeField] private RouteMapConnections connections;
        [SerializeField] private Text seedLabel;
        [SerializeField] private Text clearLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform mapContent;
        [SerializeField, Min(1f)] private float mapHeightScale = 1.35f;
        [SerializeField, Min(.01f)] private float mapWidthScale = .927f;
        [SerializeField, Min(.01f)] private float mapFollowSmoothTime = .22f;

        private readonly Dictionary<RouteNodeView, RouteNode[]> nodeViews = new Dictionary<RouteNodeView, RouteNode[]>();
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        private readonly HashSet<Vector4> allConnections = new HashSet<Vector4>();
        private RouteMap renderedMap;
        private string focusNodeId;
        private Vector2 mapFollowVelocity;
        public event Action<string> NodeClicked;
        public event Action RestartRequested;

        private void OnEnable() => restartButton.onClick.AddListener(HandleRestartClicked);
        private void OnDisable() => restartButton.onClick.RemoveListener(HandleRestartClicked);

        private void HandleRestartClicked() => RestartRequested?.Invoke();

        public void SetCleared(bool cleared) => clearLabel.gameObject.SetActive(cleared);

        public void SetRestartInteractable(bool interactable) => restartButton.interactable = interactable;

        private void HandleNodeClicked(string nodeId) => NodeClicked?.Invoke(nodeId);

        public void SetRevealProgress(float progress)
        {
            connections.SetRevealProgress(progress);
            foreach (var entry in nodeViews)
            {
                bool visible = progress > 0f && positions[entry.Value[0].Id].y <= progress;
                if (entry.Key.gameObject.activeSelf != visible)
                    entry.Key.gameObject.SetActive(visible);
            }
        }

        public void FocusNode(string nodeId)
        {
            if (!Application.isPlaying) return;

            Canvas.ForceUpdateCanvases();
            focusNodeId = nodeId;
            mapFollowVelocity = Vector2.zero;
            UpdateMapSize();
            mapContent.anchoredPosition = GetFocusPosition(GetMapLimits());
        }

        public void PositionMap(Vector2 normalizedPointer, bool followPointer)
        {
            UpdateMapSize();
            var limits = GetMapLimits();
            if (!followPointer || focusNodeId == null)
            {
                mapFollowVelocity = Vector2.zero;
                mapContent.anchoredPosition = ClampMapPosition(mapContent.anchoredPosition, limits);
                return;
            }

            var focus = GetFocusPosition(limits);
            var target = new Vector2(
                GetPointerOffset(normalizedPointer.x, focus.x, limits.x),
                GetPointerOffset(normalizedPointer.y, focus.y, limits.y));
            if (Time.unscaledDeltaTime <= 0f) return;

            var position = Vector2.SmoothDamp(mapContent.anchoredPosition, target,
                ref mapFollowVelocity, mapFollowSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            mapContent.anchoredPosition = ClampMapPosition(position, limits);
        }

        private Vector2 GetFocusPosition(Vector2 limits)
        {
            var nodePoint = Rect.NormalizedToPoint(nodeLayer.rect, positions[focusNodeId]);
            Vector2 localPoint = mapContent.InverseTransformPoint(nodeLayer.TransformPoint(nodePoint));
            // Keep some travel on both sides of the cursor center near map edges.
            return ClampMapPosition(-localPoint, limits * .85f);
        }

        private static float GetPointerOffset(float coordinate, float focus, float limit)
            => coordinate < .5f ? Mathf.Lerp(limit, focus, coordinate * 2f)
                : Mathf.Lerp(focus, -limit, coordinate * 2f - 1f);

        private void UpdateMapSize()
        {
            var size = viewport.rect.size;
            float diameter = Mathf.Max(size.y * mapHeightScale, size.x * mapWidthScale);
            var mapSize = Vector2.one * diameter;
            if (mapContent.sizeDelta != mapSize) mapContent.sizeDelta = mapSize;
        }

        private Vector2 GetMapLimits()
            => Vector2.Max(Vector2.zero, (mapContent.rect.size - viewport.rect.size) * .5f);

        private static Vector2 ClampMapPosition(Vector2 position, Vector2 limits)
        {
            return new Vector2(Mathf.Clamp(position.x, -limits.x, limits.x),
                Mathf.Clamp(position.y, -limits.y, limits.y));
        }

        public void RefreshProgress(RouteMapProgress progress, bool inputLocked)
        {
            var segments = new List<Vector4>();
            var completed = progress.CompletedNodeIds;
            for (int index = 1; index < completed.Count; index++)
                segments.Add(GetConnection(completed[index - 1], completed[index]));

            foreach (var entry in nodeViews)
            {
                string selectedId = null;
                var state = RouteNodeState.Locked;
                foreach (var node in entry.Value)
                {
                    state = node.Id == progress.CurrentNodeId ? RouteNodeState.Current
                        : completed.Contains(node.Id) ? RouteNodeState.Completed
                        : !inputLocked && progress.CanSelect(node.Id) ? RouteNodeState.Available
                        : RouteNodeState.Locked;
                    if (state == RouteNodeState.Locked) continue;
                    selectedId = node.Id;
                    break;
                }

                entry.Key.SetNodeId(selectedId);
                entry.Key.SetState(state);
                if (state == RouteNodeState.Available && progress.CurrentNodeId != null)
                    segments.Add(GetConnection(progress.CurrentNodeId, selectedId));
            }

            connections.SetConnections(segments);
            var backgroundConnections = allConnections.Except(segments);
            if (renderedMap.RulesVersion < RouteMapGenerator.RulesVersion && progress.CurrentNodeId != null)
            {
                // Old checkpoints can have several logical nodes on one icon.
                var current = renderedMap.GetNode(progress.CurrentNodeId);
                var position = positions[current.Id];
                var validOutgoing = current.NextNodeIds.Select(id => GetConnection(current.Id, id)).ToHashSet();
                backgroundConnections = backgroundConnections.Where(segment =>
                    segment.x != position.x || segment.y != position.y || validOutgoing.Contains(segment));
            }
            connections.SetPreview(backgroundConnections.ToArray());
        }

        private Vector4 GetConnection(string sourceId, string targetId)
        {
            var start = positions[sourceId];
            var end = positions[targetId];
            return new Vector4(start.x, start.y, end.x, end.y);
        }

        public void Render(RouteMap map)
        {
            ClearNodes();
            renderedMap = map;
            allConnections.Clear();
            var layoutRandom = new System.Random(unchecked((int)map.Seed));
            for (int stage = 1; stage <= 5; stage++)
            {
                var groups = map.Nodes.Where(node => node.Stage == stage)
                    .GroupBy(node => node.Type).ToList();
                float y = (stage - 1) / 4f;
                for (int index = 0; index < groups.Count; index++)
                {
                    var group = groups[index].ToArray();
                    float xOffset = Mathf.Lerp(-.065f, .065f, (float)layoutRandom.NextDouble());
                    float yOffset = Mathf.Lerp(-.045f, .045f, (float)layoutRandom.NextDouble());
                    var position = new Vector2((index + 1f) / (groups.Count + 1f) + xOffset,
                        Mathf.Clamp01(y + yOffset));
                    float iconAngle = Mathf.Lerp(-9f, 9f, (float)layoutRandom.NextDouble());
                    foreach (var node in group)
                        positions.Add(node.Id, position);
                    nodeViews.Add(CreateNode(group[0], position, iconAngle), group);
                }
            }

            foreach (var node in map.Nodes)
                foreach (var targetId in node.NextNodeIds)
                    allConnections.Add(GetConnection(node.Id, targetId));

            seedLabel.text = $"시드 {map.Seed}";
        }

        private RouteNodeView CreateNode(RouteNode node, Vector2 position, float iconAngle)
        {
            RouteNodeView instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                instance = (RouteNodeView)UnityEditor.PrefabUtility.InstantiatePrefab(nodePrefab, nodeLayer);
            else
#endif
                instance = Instantiate(nodePrefab, nodeLayer, false);
            instance.name = node.Id;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = position;
            instance.Bind(node.Id, node.Type);
            instance.SetIconAngle(iconAngle);
            instance.Clicked += HandleNodeClicked;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(instance.gameObject);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
#endif
            return instance;
        }

        private void ClearNodes()
        {
            foreach (var nodeView in nodeViews.Keys)
                if (nodeView != null) nodeView.Clicked -= HandleNodeClicked;
            nodeViews.Clear();
            positions.Clear();

            for (int index = nodeLayer.childCount - 1; index >= 0; index--)
            {
                var child = nodeLayer.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
