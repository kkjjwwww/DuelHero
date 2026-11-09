using System.Collections.Generic;
using DG.Tweening;
using DuelHero.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Battle
{
    [RequireComponent(typeof(Canvas))]
    public sealed class BattleDamageNumberUI : MonoBehaviour
    {
        [SerializeField] private CombatEffectResolver resolver;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private Sprite[] digits;
        private const float Duration = .55f;
        private const float FadeDuration = .18f;
        private static readonly Color DamageRed = new Color(1f, .29f, .26f);
        private readonly System.Random random = new();
        private readonly List<(Transform target, int direction, Tween motion)> active = new();
        private Canvas canvas;

        private void OnEnable()
        {
            canvas = GetComponent<Canvas>();
            if (resolver != null) resolver.ActionResolved += Show;
        }

        private void OnDisable()
        {
            if (resolver != null) resolver.ActionResolved -= Show;
            for (int i = active.Count - 1; i >= 0; i--)
                active[i].motion.Kill();
            active.Clear();
        }

        private void Show(BattleActionResult result)
        {
            if (result.Outcome != BattleActionOutcome.Success || result.Value <= 0 ||
                (result.Kind != BattleActionKind.Damage && result.Kind != BattleActionKind.StatusDamage) ||
                !resolver.TryGetUnit(result.TargetId, out var target, out _))
                return;

            ShowNumber(target.transform, result.Value);
        }

#if UNITY_EDITOR
        public void ShowDamageForDebug(Transform target, int amount)
        {
            if (Application.isPlaying && isActiveAndEnabled && target != null && amount > 0)
                ShowNumber(target, amount);
        }
#endif

        private void ShowNumber(Transform target, int amount)
        {
            if (battleCamera == null) return;
            var sprite = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 origin = sprite != null ? sprite.bounds.center : target.position;
            int previous = active.FindLastIndex(number => number.target == target);
            int direction = previous >= 0 ? -active[previous].direction : random.Next(2) * 2 - 1;
            float distance = direction * Mathf.Lerp(18f, 32f, (float)random.NextDouble());
            float height = Mathf.Lerp(20f, 28f, (float)random.NextDouble());

            var root = new GameObject("Damage Number", typeof(RectTransform), typeof(CanvasGroup), typeof(SpriteNumberUI));
            var rect = (RectTransform)root.transform;
            var parent = (RectTransform)transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var numberUI = root.GetComponent<SpriteNumberUI>();
            numberUI.SetDigitSprites(digits);
            numberUI.SetValue(amount);
            foreach (var image in root.GetComponentsInChildren<Image>())
                image.color = DamageRed;

            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            void Render(float progress)
            {
                Vector3 screen = battleCamera.WorldToScreenPoint(origin);
                if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var point))
                {
                    group.alpha = 0f;
                    return;
                }
                var arc = new Vector2(distance * progress, 4f * height * progress * (1f - progress) - 8f * progress);
                rect.anchoredPosition = point - parent.rect.center + arc;
                group.alpha = Mathf.Clamp01((1f - progress) * Duration / FadeDuration);
            }

            Render(0f);
            Tween motion = DOVirtual.Float(0f, 1f, Duration, Render).SetEase(Ease.Linear).SetUpdate(UpdateType.Late, true);
            active.Add((target, direction, motion));
            motion.OnKill(() =>
            {
                active.RemoveAll(entry => entry.motion == motion);
                if (root != null) Destroy(root);
            });
        }
    }
}
