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
        [SerializeField] private Font labelFont;
        [SerializeField] private Text seedLabel;
        [SerializeField] private Color stageLabelColor = new Color(.58f, .63f, .65f);

        public void Render(RouteMap map)
        {
            ClearNodes();
            var positions = new Dictionary<string, Vector2>();
            for (int stage = 1; stage <= 5; stage++)
            {
                var nodes = map.Nodes.Where(node => node.Stage == stage).ToList();
                float y = (stage - 1) / 4f;
                CreateText("Stage " + stage, nodeLayer, new Vector2(0f, y),
                    new Vector2(-72f, 0f), new Vector2(90f, 32f),
                    stage.ToString("00"), 22, stageLabelColor);
                for (int index = 0; index < nodes.Count; index++)
                {
                    var node = nodes[index];
                    var position = new Vector2((index + 1f) / (nodes.Count + 1f), y);
                    positions.Add(node.Id, position);
                    CreateNode(node, position);
                }
            }

            var segments = new List<Vector4>();
            foreach (var node in map.Nodes)
            {
                foreach (var targetId in node.NextNodeIds)
                {
                    var start = positions[node.Id];
                    var end = positions[targetId];
                    segments.Add(new Vector4(start.x, start.y, end.x, end.y));
                }
            }
            connections.SetConnections(segments);
            seedLabel.text = $"시드 {map.Seed}  ·  규칙 {map.RulesVersion}";
        }

        private void CreateNode(RouteNode node, Vector2 position)
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
            instance.Render(node.Type);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(instance.gameObject);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
#endif
        }

        private void CreateText(string name, Transform parent, Vector2 anchor, Vector2 offset,
            Vector2 size, string value, int fontSize, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            var text = label.GetComponent<Text>();
            text.font = labelFont;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
        }

        private void ClearNodes()
        {
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
