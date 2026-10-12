using System.Collections.Generic;
using System.Linq;
using DuelHero.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // Reads owned instances; selected enemy actions are never exposed here.
    public sealed class EnemyCardListUI : MonoBehaviour
    {
        [SerializeField] private EnemyActionPlanProvider source;
        [SerializeField] private CardReservationQueue roundSource;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Font font;
        private readonly List<(CardInstance card, Image background, Text state)> rows = new();
        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (source != null) source.CardsChanged += RefreshStates;
            if (roundSource != null) roundSource.Changed += RefreshStates;
            Close();
        }
        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (source != null) source.CardsChanged -= RefreshStates;
            if (roundSource != null) roundSource.Changed -= RefreshStates;
            Close();
        }
        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (source == null || roundSource == null || panel == null || content == null) return;
            Rebuild();
            panel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }
        public void Close() { if (panel != null) panel.SetActive(false); }
        private void Rebuild()
        {
            rows.Clear();
            foreach (Transform child in content.Cast<Transform>().ToArray())
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var cards = source.Cards;
            if (titleLabel != null) titleLabel.text = "적 보유 카드 · " + cards.Count + "장";
            if (detailLabel != null) detailLabel.text = "카드에 마우스를 올리면 상세 설명을 확인합니다.";
            foreach (var card in cards.OrderBy(c => c.Definition.actionType).ThenBy(c => c.Definition.name))
            {
                var go = new GameObject("EnemyCard_" + card.Definition.id, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardHoverInfo));
                go.transform.SetParent(content, false);
                go.GetComponent<LayoutElement>().preferredHeight = 76;
                var image = go.GetComponent<Image>();
                var name = Label(go.transform, "Name", card.Definition.name, 16, 6, 24);
                var metric = string.Join(" · ", new[] { "피해", "방어", "회복", "이동" }
                    .Zip(new[] { "damage", "guard", "energyRestore", "move" }, (label, type) =>
                        card.Definition.Amount(type) > 0 ? label + " " + card.Definition.Amount(type) : null).Where(x => x != null));
                Label(go.transform, "Values", "에너지 " + card.Definition.energyCost + " · " + metric, 13, 30, 20);
                var state = Label(go.transform, "State", "", 13, 51, 20);
                rows.Add((card, image, state));
                var hover = go.GetComponent<CardHoverInfo>();
                hover.ShowInfo = () =>
                {
                    if (detailLabel == null) return;
                    var keywords = source.Database.keywords.Where(k => card.Definition.keywordIds.Contains(k.id)).Select(k => k.name);
                    detailLabel.text = card.Definition.name + " — " + card.Definition.ResolvedDescription()
                        + "\n" + string.Join(" · ", keywords);
                };
                hover.HideInfo = () => { if (detailLabel != null) detailLabel.text = "카드에 마우스를 올리면 상세 설명을 확인합니다."; };
            }
            RefreshStates();
        }
        private void RefreshStates()
        {
            if (roundSource == null) return;
            foreach (var row in rows)
            {
                int cooldown = row.card.RemainingCooldown(roundSource.CurrentRound);
                row.state.text = cooldown > 0 ? "재사용 대기 " + cooldown + "턴" : "사용 가능";
                row.state.color = cooldown > 0 ? new Color(0.65f, 0.65f, 0.65f) : Color.white;
                row.background.color = cooldown > 0 ? new Color(0.09f, 0.09f, 0.09f, 0.95f) : new Color(0.23f, 0.23f, 0.23f, 0.95f);
            }
        }
        private Text Label(Transform parent, string name, string value, int size, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = Color.white;
            text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
            var rect = text.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -top); rect.sizeDelta = new Vector2(-16, height);
            return text;
        }
    }
}
