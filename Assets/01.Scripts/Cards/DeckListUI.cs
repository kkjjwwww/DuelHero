using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    public class DeckListUI : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private CardReservationQueue reservationQueue;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text detailLabel;
        [SerializeField] private float minimumCardWidth = 132f;
        [SerializeField] private float cardHeight = 118f;
        [Header("Effect Value Colors")]
        [SerializeField] private Color damageColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private Color guardColor = new Color(0.45f, 0.7f, 1f);
        [SerializeField] private Color energyRestoreColor = new Color(0.45f, 0.9f, 0.6f);
        [SerializeField] private Color moveColor = new Color(1f, 0.85f, 0.4f);
        private readonly List<CardInstance> deck = new();
        private readonly List<(CardInstance card, Button button, Text state)> views = new();
        private Font font;
        private CanvasGroup panelVisibility;
        public IReadOnlyList<CardInstance> Deck => deck;
        public string SelectedCategory { get; private set; }
        public event Action ViewChanged;

        private void OnEnable()
        {
            panelVisibility = GetComponent<CanvasGroup>();
            RefreshVisibility();
            if (playerDeck != null) playerDeck.Changed += Rebuild;
            if (reservationQueue != null) reservationQueue.Changed += RefreshStates;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorApplication.delayCall += EditorRebuild; return; }
#endif
            Rebuild();
        }
        private void OnDisable()
        {
            if (playerDeck != null) playerDeck.Changed -= Rebuild;
            if (reservationQueue != null) reservationQueue.Changed -= RefreshStates;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= EditorRebuild;
#endif
        }
#if UNITY_EDITOR
        private void EditorRebuild() { if (this != null && isActiveAndEnabled) Rebuild(); }
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= EditorRebuild;
            UnityEditor.EditorApplication.delayCall += EditorRebuild;
        }
#endif
        public int CountCategory(string category) => playerDeck == null ? 0 : playerDeck.Cards.Count(c => c.Definition.actionType == category);
        public void ToggleCategory(string category)
        {
            if (!new[] { "공격", "이동", "방어", "회복", "특수" }.Contains(category)) return;
            SelectedCategory = SelectedCategory == category ? null : category;
            RefreshVisibility();
            Rebuild();
            var scroll = content == null ? null : content.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.horizontalNormalizedPosition = 0;
        }
        private void RefreshVisibility()
        {
            if (panelVisibility == null) panelVisibility = GetComponent<CanvasGroup>();
            if (panelVisibility == null) return;
            bool visible = SelectedCategory != null;
            panelVisibility.alpha = visible ? 1 : 0;
            panelVisibility.interactable = visible;
            panelVisibility.blocksRaycasts = visible;
        }
        [ContextMenu("Rebuild deck preview")]
        public void Rebuild()
        {
            if (playerDeck == null || content == null) return;
            Vector2 oldPosition = content.anchoredPosition;
            playerDeck.Changed -= Rebuild;
            playerDeck.Initialize();
            if (isActiveAndEnabled) playerDeck.Changed += Rebuild;
            deck.Clear(); deck.AddRange(playerDeck.Cards); views.Clear();
            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
            RefreshVisibility();
            foreach (var instance in deck.Where(c => SelectedCategory != null && c.Definition.actionType == SelectedCategory)
                .OrderByDescending(c => c.Definition.isBasicAction).ThenBy(c => c.Definition.actionType)) CreateCard(content, instance);
            RefreshStates(); Canvas.ForceUpdateCanvases(); content.anchoredPosition = oldPosition;
            if (detailLabel != null) { detailLabel.font = font; detailLabel.text = "카드에 마우스를 올리면 설명과 상태를 확인할 수 있습니다."; }
            ViewChanged?.Invoke();
        }
        private string UnavailableReason(CardInstance instance)
        {
            if (reservationQueue == null) return "예약 관리자 미연결";
            if (reservationQueue.IsExecuting) return "행동 실행 중";
            if (instance.ReservedSlot >= 0) return "예약 슬롯 " + (instance.ReservedSlot + 1);
            int cooldown = instance.RemainingCooldown(reservationQueue.CurrentRound);
            if (cooldown > 0) return "재사용 대기 " + cooldown + "턴";
            if (reservationQueue.Reservations.Count >= 3) return "행동 슬롯 가득 참";
            return null;
        }
        private void RefreshStates()
        {
            foreach (var view in views)
            {
                string reason = UnavailableReason(view.card);
                view.button.interactable = reason == null;
                view.state.text = reason ?? "사용 가능 · 미예약";
            }
        }
        private void CreateCard(Transform parent, CardInstance instance)
        {
            var card = instance.Definition;
            var go = Make("Card_" + card.id, parent);
            var layout = go.AddComponent<LayoutElement>(); layout.preferredWidth = minimumCardWidth; layout.preferredHeight = cardHeight; layout.flexibleWidth = 0;
            var image = go.AddComponent<Image>(); image.color = new Color(0.12f, 0.12f, 0.12f, 0.72f);
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = colors.selectedColor = new Color(0.7f, 0.7f, 0.7f); colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f); button.colors = colors;
            var name = Label("Name", go.transform, card.name, 17); Position(name.rectTransform, 8, -8, -8, 26);
            string metric = card.Amount("damage") > 0 ? "피해 " + ColoredValue(card.Amount("damage"), damageColor)
                : card.Amount("guard") > 0 ? "방어 " + ColoredValue(card.Amount("guard"), guardColor)
                : card.Amount("energyRestore") > 0 ? "회복 " + ColoredValue(card.Amount("energyRestore"), energyRestoreColor)
                : "이동 " + ColoredValue(card.Amount("move"), moveColor) + "칸";
            var values = Label("Values", go.transform, "에너지 " + card.energyCost + "\n" + metric, 12); values.supportRichText = true; Position(values.rectTransform, 8, -34, -8, 34);
            var state = Label("State", go.transform, "", 12); Position(state.rectTransform, 8, -70, -8, 24);
            var kind = Label("Kind", go.transform, card.isBasicAction ? "기본 행동" : "기술 · " + card.actionType, 11); Position(kind.rectTransform, 8, -95, -8, 18);
            views.Add((instance, button, state));
            button.onClick.AddListener(() => { if (Application.isPlaying && reservationQueue != null) reservationQueue.TryReserve(instance); });
            go.AddComponent<CardHoverInfo>().ShowInfo = () =>
            {
                if (detailLabel == null) return;
                string keywords = string.Join(" · ", playerDeck.Database.keywords.Where(k => card.keywordIds.Contains(k.id)).Select(k => k.name));
                string reason = UnavailableReason(instance);
                detailLabel.text = card.name + " — " + card.ResolvedDescription() + (keywords.Length > 0 ? " · " + keywords : "") + (reason == null ? "" : " · " + reason);
            };
        }
        private static string ColoredValue(int value, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + value + "</color>";
        private static GameObject Make(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private Text Label(string name, Transform parent, string value, int size)
        {
            var text = Make(name, parent).AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(0.96f, 0.96f, 0.96f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; return text;
        }
        private static void Position(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, top - height); rect.offsetMax = new Vector2(right, top);
        }
    }
}
