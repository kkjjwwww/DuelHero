using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    [ExecuteAlways]
    public class DeckListUI : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private CardReservationQueue reservationQueue;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text detailLabel;
        [SerializeField] private float minimumCardWidth = 108f;
        [SerializeField] private float cardHeight = 118f;
        private readonly List<CardInstance> deck = new();
        private readonly List<GridLayoutGroup> grids = new();
        private readonly List<LayoutElement> groupLayouts = new();
        private Font font;
        private float previousWidth = -1;
        public IReadOnlyList<CardInstance> Deck => deck;

        private void OnEnable()
        {
            if (playerDeck != null) playerDeck.Changed += Rebuild;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorApplication.delayCall += EditorRebuild; return; }
#endif
            Rebuild();
        }
        private void OnDisable()
        {
            if (playerDeck != null) playerDeck.Changed -= Rebuild;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= EditorRebuild;
#endif
        }
#if UNITY_EDITOR
        private void EditorRebuild() { if (this != null && isActiveAndEnabled) Rebuild(); }
#endif

        [ContextMenu("Rebuild deck preview")]
        public void Rebuild()
        {
            if (playerDeck == null || content == null) return;
            // Initialize before subscribing redraws can recurse on the first Changed event.
            playerDeck.Changed -= Rebuild;
            playerDeck.Initialize();
            if (isActiveAndEnabled) playerDeck.Changed += Rebuild;
            deck.Clear(); grids.Clear(); groupLayouts.Clear();
            deck.AddRange(playerDeck.Cards);
            // This content is dedicated to the imported deck; rebuild it without changing other UI.
            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
            string[] types = { "공격", "이동", "방어", "회복" };
            foreach (string type in types.Concat(deck.Select(c => c.Definition.actionType)).Distinct())
            {
                var cards = deck.Where(c => c.Definition.actionType == type).ToArray();
                if (cards.Length == 0) continue;
                var group = Make(type + "Group", content);
                var vertical = group.AddComponent<VerticalLayoutGroup>();
                vertical.spacing = 8; vertical.childControlWidth = vertical.childControlHeight = true;
                vertical.childForceExpandHeight = false;
                var groupLayout = group.AddComponent<LayoutElement>(); groupLayouts.Add(groupLayout);
                var heading = Label("TypeTitle", group.transform, type + "  " + cards.Length, 16);
                heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                var rowArea = Make("Cards", group.transform);
                var grid = rowArea.AddComponent<GridLayoutGroup>(); grid.spacing = new Vector2(8, 8);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                rowArea.AddComponent<LayoutElement>(); grids.Add(grid);
                foreach (var instance in cards) CreateCard(grid.transform, instance);
            }
            previousWidth = -1;
            Canvas.ForceUpdateCanvases(); Reflow();
            if (detailLabel != null) { detailLabel.font = font; detailLabel.text = "카드에 마우스를 올리면 설명을 확인할 수 있습니다."; }
        }

        private void LateUpdate() => Reflow();
        private void Reflow()
        {
            if (content == null || grids.Count == 0) return;
            float width = content.rect.width;
            if (width <= 0 || Mathf.Abs(width - previousWidth) < 0.5f) return;
            previousWidth = width;
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + 8) / (minimumCardWidth + 8)));
            float cellWidth = (width - (columns - 1) * 8) / columns;
            for (int i = 0; i < grids.Count; i++)
            {
                var grid = grids[i]; grid.constraintCount = columns; grid.cellSize = new Vector2(cellWidth, cardHeight);
                int rows = Mathf.CeilToInt(grid.transform.childCount / (float)columns);
                float areaHeight = rows * cardHeight + Mathf.Max(0, rows - 1) * 8;
                grid.GetComponent<LayoutElement>().preferredHeight = areaHeight;
                groupLayouts[i].preferredHeight = 28 + 8 + areaHeight;
            }
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private void CreateCard(Transform parent, CardInstance instance)
        {
            CardDefinition card = instance.Definition;
            var go = Make("Card_" + card.id, parent);
            var image = go.AddComponent<Image>(); image.color = new Color(0.12f, 0.12f, 0.12f, 0.72f);
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.7f, 0.7f, 0.7f); colors.selectedColor = new Color(0.7f, 0.7f, 0.7f); button.colors = colors;
            var name = Label("Name", go.transform, card.name, 17); Position(name.rectTransform, 8, -8, -8, 30);
            string metric = card.Amount("damage") > 0 ? "피해 " + card.Amount("damage")
                : card.Amount("guard") > 0 ? "방어 " + card.Amount("guard")
                : card.Amount("energyRestore") > 0 ? "회복 " + card.Amount("energyRestore") : "이동 " + card.Amount("move") + "칸";
            var values = Label("Values", go.transform, "에너지 " + card.energyCost + "\n" + metric, 12);
            Position(values.rectTransform, 8, -35, -8, 36);
            var state = Label("State", go.transform, instance.ReservedSlot >= 0 ? "예약 슬롯 " + (instance.ReservedSlot + 1) : "미예약", 12); Position(state.rectTransform, 8, -71, -8, 22);
            var tag = Label("Kind", go.transform, card.isBasicAction ? "기본 행동" : "기술 카드", 11); Position(tag.rectTransform, 8, -95, -8, 18);
            button.onClick.AddListener(() => { if (Application.isPlaying && reservationQueue != null) reservationQueue.TryReserve(instance); });
            var trigger = go.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ =>
            {
                if (detailLabel == null) return;
                string keywords = string.Join(" · ", playerDeck.Database.keywords.Where(k => card.keywordIds.Contains(k.id)).Select(k => k.name));
                detailLabel.text = card.name + " — " + card.ResolvedDescription() + (keywords.Length > 0 ? "\n" + keywords : "");
            });
            trigger.triggers.Add(hover);
        }

        private GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go;
        }
        private Text Label(string name, Transform parent, string value, int size)
        {
            var go = Make(name, parent); var text = go.AddComponent<Text>(); text.font = font; text.text = value;
            text.fontSize = size; text.color = new Color(0.96f, 0.96f, 0.96f); text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }
        private static void Position(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, top - height); rect.offsetMax = new Vector2(right, top);
        }
    }
}
