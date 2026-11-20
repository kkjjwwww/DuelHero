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
        [SerializeField] private BattleTurnController battleTurn;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private Sprite[] digits;
        [SerializeField, Min(2)] private int damageForMaxScale = 50;
        [SerializeField, Min(1f)] private float maxDamageNumberScale = 1.6f;
        private const float Duration = .55f;
        private const float FadeDuration = .18f;
        private static readonly Color DamageRed = new Color(1f, .29f, .26f);
        private readonly System.Random random = new();
        private readonly List<(Transform target, int direction, Tween motion)> active = new();
        private Canvas canvas;
        private Canvas damageOverlay;

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
            if (damageOverlay != null) Destroy(damageOverlay.gameObject);
            damageOverlay = null;
        }

        private void Show(BattleActionResult result)
        {
            if (result.Outcome != BattleActionOutcome.Success || result.Value <= 0 ||
                (result.Kind != BattleActionKind.Damage && result.Kind != BattleActionKind.StatusDamage) ||
                !resolver.TryGetUnit(result.TargetId, out var target, out _))
                return;

            bool finalHit = result.IsLethalHit && battleTurn != null && battleTurn.IsFinalEnemyHit(result.TargetId);
            PlayImpact(result.DisplayDamage, result.IsLethalHit, result.Kind == BattleActionKind.StatusDamage, finalHit);
            ShowNumber(target.transform, result.DisplayDamage);
        }

#if UNITY_EDITOR
        public void ShowDamageForDebug(Transform target, int amount, int displayDamage, bool lethal)
        {
            if (Application.isPlaying && isActiveAndEnabled && target != null && amount > 0)
            {
                PlayImpact(displayDamage, lethal, false);
                ShowNumber(target, displayDamage);
            }
        }
#endif

        private void PlayImpact(int amount, bool lethal, bool statusDamage, bool finalHit = false)
        {
            if (battleCamera != null && battleCamera.TryGetComponent<BattleCameraController>(out var controller))
                controller.PlayDamageImpact(amount, lethal, statusDamage, finalHit);
        }

        private void ShowNumber(Transform target, int amount)
        {
            if (battleCamera == null) return;
            var sprite = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 origin = sprite != null ? sprite.bounds.center : target.position;
            if (battleCamera.TryGetComponent<BattleCameraController>(out var controller))
                origin += controller.ImpactWorldOffset;
            Vector3 screen = battleCamera.WorldToScreenPoint(origin);
            if (screen.z <= 0f) return;
            int previous = active.FindLastIndex(number => number.target == target);
            int direction = previous >= 0 ? -active[previous].direction : random.Next(2) * 2 - 1;
            float distance = direction * Mathf.Lerp(18f, 32f, (float)random.NextDouble());
            float height = Mathf.Lerp(20f, 28f, (float)random.NextDouble());

            var root = new GameObject("Damage Number", typeof(RectTransform), typeof(CanvasGroup), typeof(SpriteNumberUI));
            var rect = (RectTransform)root.transform;
            var parent = GetDamageOverlay();
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            float damageRatio = Mathf.InverseLerp(1f, damageForMaxScale, amount);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, maxDamageNumberScale, damageRatio);
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var numberUI = root.GetComponent<SpriteNumberUI>();
            numberUI.SetDigitSprites(digits);
            numberUI.SetValue(amount);
            foreach (var image in root.GetComponentsInChildren<Image>())
                image.color = DamageRed;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var start);
            start -= parent.rect.center;
            void Render(float progress)
            {
                var arc = new Vector2(distance * progress, 4f * height * progress * (1f - progress) - 8f * progress);
                rect.anchoredPosition = start + arc;
                group.alpha = Mathf.Clamp01((1f - progress) * Duration / FadeDuration);
            }

            Render(0f);
            Tween motion = DOVirtual.Float(0f, 1f, Duration, Render).SetEase(Ease.Linear).SetUpdate(UpdateType.Late);
            if (controller != null && controller.IsHoldingImpact) motion.Pause();
            active.Add((target, direction, motion));
            motion.OnKill(() =>
            {
                active.RemoveAll(entry => entry.motion == motion);
                if (root != null) Destroy(root);
            });
        }

        private void LateUpdate()
        {
            if (battleCamera != null && battleCamera.TryGetComponent<BattleCameraController>(out var controller) &&
                controller.IsHoldingImpact) return;

            foreach (var number in active)
                if (!number.motion.IsPlaying()) number.motion.Play();
        }

        private RectTransform GetDamageOverlay()
        {
            if (damageOverlay == null)
            {
                var root = new GameObject("Damage Number Overlay", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
                damageOverlay = root.GetComponent<Canvas>();
                damageOverlay.renderMode = RenderMode.ScreenSpaceOverlay;
                damageOverlay.sortingOrder = canvas.sortingOrder;
                damageOverlay.targetDisplay = canvas.targetDisplay;
            }
            damageOverlay.scaleFactor = canvas.scaleFactor;
            Canvas.ForceUpdateCanvases();
            return (RectTransform)damageOverlay.transform;
        }
    }
}
