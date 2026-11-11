using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class OptionsButtonRotation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform icon;
        [SerializeField, Min(0f)] private float duration = .5f;
        private Button button;
        private Tween rotation;
        private Quaternion initialRotation;

        private void Awake()
        {
            button = GetComponent<Button>();
            initialRotation = icon.localRotation;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || (button != null && !button.IsInteractable())) return;
            ResetRotation();
            rotation = icon.DOLocalRotate(new Vector3(0f, 0f, -360f), duration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(ResetRotation);
        }

        public void OnPointerExit(PointerEventData eventData) => ResetRotation();

        private void ResetRotation()
        {
            rotation?.Kill();
            rotation = null;
            if (icon != null) icon.localRotation = initialRotation;
        }

        private void OnDisable() => ResetRotation();
    }
}
