using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [RequireComponent(typeof(Button))]
    public sealed class DisabledExecuteSound : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private Button button;
        private bool disabledOnPress;

        private void Awake() => button = GetComponent<Button>();

        public void OnPointerDown(PointerEventData eventData)
        {
            disabledOnPress = eventData.button == PointerEventData.InputButton.Left && !button.IsInteractable();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && disabledOnPress && !button.IsInteractable())
                DuelHero.Audio.SoundEffectPlayer.Play("battle.execute.unavailable");
            disabledOnPress = false;
        }

        private void OnDisable() => disabledOnPress = false;
    }
}
