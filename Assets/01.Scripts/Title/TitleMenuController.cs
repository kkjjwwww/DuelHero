using DuelHero.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelHero.Title
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        private const string MapScenePath = "Assets/00.Scenes/MapScene.unity";
        private bool starting;

        public void StartNewGame()
        {
            if (starting) return;
            if (!Application.CanStreamedLevelBeLoaded(MapScenePath))
            {
                Debug.LogError("맵 씬을 불러올 수 없습니다: " + MapScenePath, this);
                return;
            }

            var load = SceneManager.LoadSceneAsync(MapScenePath, LoadSceneMode.Single);
            if (load == null) return;
            starting = true;
            RunSessionStore.Clear();
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
