using System;
using System.IO;
using DuelHero.Map;
using DuelHero.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Title
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        private const string MapScenePath = "Assets/00.Scenes/MapScene.unity";
        [SerializeField] private Button continueButton;
        [SerializeField] private Text errorLabel;
        private bool starting;

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
            }
            catch (Exception error) { RestoreTitle(error); }
        }

        public void StartNewGame()
        {
            if (starting) return;
            if (!Application.CanStreamedLevelBeLoaded(MapScenePath))
            {
                Debug.LogError("맵 씬을 불러올 수 없습니다: " + MapScenePath, this);
                return;
            }

            starting = true;
            try { MapSceneFade.Load(MapScenePath, RestoreTitle, RunSessionStore.Clear); }
            catch (Exception error) { RestoreTitle(error); }
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
