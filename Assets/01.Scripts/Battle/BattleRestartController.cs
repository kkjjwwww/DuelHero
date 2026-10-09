using UnityEngine;
using UnityEngine.SceneManagement;
namespace DuelHero.Battle
{
    public sealed class BattleRestartController : MonoBehaviour
    {
        private bool restarting;
        public void Restart()
        {
            if (restarting) return;
            var scene = gameObject.scene;
            if (string.IsNullOrEmpty(scene.path)) return;
            restarting = true;
#if UNITY_EDITOR
            // Also supports a development scene not yet listed in Build Settings.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadSceneAsync(scene.path, LoadSceneMode.Single);
#endif
        }
    }
}
