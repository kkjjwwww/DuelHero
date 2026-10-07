using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;
using UnityEngine.UI;
namespace DuelHero.Logging
{
    public sealed class BattleLogUI : MonoBehaviour
    {
        [SerializeField] private BattleLog source;
        [SerializeField] private CardDatabase cards;
        [SerializeField] private DuelHero.Data.UnitDatabase units;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private Font font;
        private readonly List<Text> rows = new();
        private void OnEnable() { if (source != null) source.Changed += Refresh; Refresh(); }
        private void OnDisable() { if (source != null) source.Changed -= Refresh; }
        public void Refresh()
        {
            if (source == null || content == null || scroll == null) return;
            bool follow = rows.Count == 0 || scroll.verticalNormalizedPosition <= 0.03f;
            Vector2 oldPosition = content.anchoredPosition;
            while (rows.Count < source.Entries.Count)
            {
                var go = new GameObject("LogRow", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
                go.transform.SetParent(content, false);
                var text = go.GetComponent<Text>(); text.font = font; text.fontSize = 14;
                text.color = new Color(0.95f, 0.95f, 0.95f); text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.alignment = TextAnchor.MiddleLeft;
                rows.Add(text);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < source.Entries.Count);
                if (i >= source.Entries.Count) continue;
                rows[i].text = Format(source.Entries[i]);
                rows[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, content.rect.width - 16));
                rows[i].GetComponent<LayoutElement>().preferredHeight = Mathf.Max(26, rows[i].preferredHeight + 8);
            }
            Canvas.ForceUpdateCanvases();
            if (follow) scroll.verticalNormalizedPosition = 0;
            else content.anchoredPosition = oldPosition;
        }
        private string Format(BattleLogEntry entry)
        {
            string prefix = "턴 " + entry.Round + " · ";
            string actor = units == null ? entry.ActorId : units.players.Concat(units.enemies).FirstOrDefault(u => u.id == entry.ActorId)?.name ?? entry.ActorId;
            string card = cards == null ? entry.CardId : cards.cards.FirstOrDefault(c => c.id == entry.CardId)?.name ?? entry.CardId;
            string action = string.IsNullOrEmpty(card) ? "이동" : card;
            string target = units == null ? entry.TargetId : units.players.Concat(units.enemies).FirstOrDefault(u => u.id == entry.TargetId)?.name ?? entry.TargetId;
            return prefix + (entry.Kind switch
            {
                BattleLogKind.TurnStarted => "시작",
                BattleLogKind.TurnEnded => "종료",
                BattleLogKind.CardExecuted => actor + " · " + card + " 실행",
                BattleLogKind.ExecutionBlocked => actor + " · " + card + " 실행 불가 (미구현 효과)",
                BattleLogKind.Movement => actor + " · " + action + (entry.Result == BattleLogResult.BoundaryBlocked ? ": 경계로 이동 실패 " + entry.From : ": " + entry.From + " → " + entry.To),
                BattleLogKind.Damage => actor + " · " + card + (entry.Result == BattleLogResult.NoTarget ? ": 명중 대상 없음" : " → " + target + ": 피해 " + entry.Value + (entry.ReducedDamage > 0 ? " (방어로 " + entry.ReducedDamage + " 감소)" : "")),
                BattleLogKind.GuardApplied => actor + " · 방어 +" + entry.Value + " (현재 포함 " + entry.DurationSlots + "슬롯)",
                BattleLogKind.GuardExpired => actor + " · 방어 " + entry.Value + " 만료",
                _ => entry.Kind.ToString()
            });
        }
    }
}
