using DuelHero.Audio;
using DuelHero.Map;
using DuelHero.Run;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Title
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        private const string MapScenePath = "Assets/00.Scenes/MapScene.unity";
        [SerializeField] private Button continueButton;
        [SerializeField] private Text errorLabel;
        [SerializeField] private Sprite noticePanel;
        [SerializeField] private Sprite popupButton;
        [SerializeField] private Sprite disabledPopupButton;
        private bool starting;
        private GameObject confirmation;

        private void Start()
        {
            continueButton.interactable = File.Exists(RunCheckpoint.DefaultPath);
            errorLabel.text = string.Empty;
        }

        public void ContinueGame()
        {
            if (starting) return;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(MapScenePath))
                    throw new InvalidOperationException("맵 씬을 불러올 수 없습니다.");
                var restored = RunCheckpoint.Load();
                errorLabel.text = string.Empty;
                starting = true;
                MapSceneFade.Load(MapScenePath, RestoreTitle, () => RunSessionStore.Restore(restored));
                SoundEffectPlayer.Play("title.game.start");
            }
            catch (Exception error) { RestoreTitle(error); }
        }

        public void StartNewGame()
        {
            if (starting) return;
            if (File.Exists(RunCheckpoint.DefaultPath))
            {
                if (confirmation == null) CreateConfirmation();
                errorLabel.text = string.Empty;
                confirmation.SetActive(true);
                SoundEffectPlayer.Play("title.popup.open"); 
                confirmation.transform.SetAsLastSibling();
                return;
            }
            BeginNewGame(false);
        }

        private void BeginNewGame(bool deleteSave)
        {
            if (!Application.CanStreamedLevelBeLoaded(MapScenePath))
            {
                errorLabel.text = "맵 씬을 불러올 수 없습니다.";
                return;
            }

            try
            {
                if (deleteSave) File.Delete(RunCheckpoint.DefaultPath);
            }
            catch (Exception error)
            {
                confirmation.SetActive(false);
                errorLabel.text = "저장 파일을 삭제하지 못했습니다. 다시 시도해주세요.";
                Debug.LogWarning(error.Message, this);
                return;
            }
            if (confirmation != null) confirmation.SetActive(false);
            continueButton.interactable = File.Exists(RunCheckpoint.DefaultPath);
            errorLabel.text = string.Empty;
            starting = true;
            try
            {
                MapSceneFade.Load(MapScenePath, RestoreTitle, RunSessionStore.Clear);
                SoundEffectPlayer.Play("title.game.start");
            }
            catch (Exception error) { RestoreTitle(error); }
        }

        private void CreateConfirmation()
        {
            confirmation = new GameObject("New Game Confirmation", typeof(RectTransform), typeof(Image));
            var rect = confirmation.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            confirmation.GetComponent<Image>().color = new Color(0f, 0f, 0f, .85f);

            var panelImage = new GameObject("Notice Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var panel = panelImage.rectTransform;
            panel.SetParent(rect, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(920f, 360f);
            panelImage.sprite = noticePanel;
            panelImage.type = Image.Type.Sliced;

            var message = CreateText("Message", panel, new Vector2(0, 44), new Vector2(800, 140));
            message.text = "새로 시작하면 기존 저장 데이터가 삭제됩니다.\n새 게임을 시작할까요?";
            CreateConfirmationButton("확인", panel, new Vector2(-120, -72), () => BeginNewGame(true));
            CreateConfirmationButton("취소", panel, new Vector2(120, -72), () =>
            {
                confirmation.SetActive(false);
                SoundEffectPlayer.Play("title.popup.close");
            });

        }

        private void CreateConfirmationButton(string label, RectTransform parent, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(200, 65);
            var image = obj.GetComponent<Image>();
            image.sprite = popupButton;
            var button = obj.GetComponent<Button>();
            obj.AddComponent<TitleButtonHoverSound>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = disabledPopupButton };
            button.onClick.AddListener(action);
            CreateText("Label", rect, Vector2.zero, new Vector2(200, 65)).text = label;
        }

        private Text CreateText(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = obj.GetComponent<Text>();
            label.font = errorLabel.font; label.fontSize = 28; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false;
            return label;
        }

        private void RestoreTitle(Exception error)
        {
            starting = false;
            errorLabel.text = "게임을 불러오지 못했습니다. 저장 파일을 확인해주세요.";
            Debug.LogError(error.Message, this);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
