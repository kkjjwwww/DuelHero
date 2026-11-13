using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class OptionsPopupBackdrop : MonoBehaviour
    {
        [SerializeField] private GameObject backdrop;
        private static int openCount;
        private static float previousTimeScale;
        public static bool IsOpen => openCount > 0;

        public void OpenWithSound()
        {
            gameObject.SetActive(true);
            DuelHero.Audio.SoundEffectPlayer.Play("title.popup.open");
        }

        public void CloseWithSound()
        {
            gameObject.SetActive(false);
            DuelHero.Audio.SoundEffectPlayer.Play("title.popup.close");
        }

        private void OnEnable()
        {
            if (openCount++ == 0)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                foreach (var fan in FindObjectsByType<CardFanLayout>(FindObjectsSortMode.None)) fan.ResetFocus();
                foreach (var inspection in FindObjectsByType<CardInspectionPresenter>(FindObjectsSortMode.None)) inspection.Hide();
                foreach (var trigger in FindObjectsByType<DelayedTooltipTrigger>(FindObjectsSortMode.None)) trigger.Cancel();
                foreach (var tooltip in FindObjectsByType<KeywordTooltip>(FindObjectsSortMode.None)) tooltip.Hide();
            }
            var rect = backdrop.GetComponent<RectTransform>();
            rect.SetParent(transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var button = backdrop.GetComponent<Button>();
            button.onClick.RemoveListener(Close);
            button.onClick.AddListener(Close);
            transform.SetAsLastSibling();
            backdrop.transform.SetSiblingIndex(transform.GetSiblingIndex() - 1);
            backdrop.SetActive(true);
        }

        private void Close() => gameObject.SetActive(false);
        private void OnDisable()
        {
            if (backdrop != null)
            {
                backdrop.SetActive(false);
            }
            if (openCount > 0 && --openCount == 0) Time.timeScale = previousTimeScale;
        }
        private void OnDestroy()
        {
            if (backdrop != null && backdrop.transform.parent != transform) Destroy(backdrop);
        }
    }
}
