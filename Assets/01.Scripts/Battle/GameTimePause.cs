using UnityEngine;

namespace DuelHero.Battle
{
    internal static class GameTimePause
    {
        private static int count;
        private static float previousTimeScale;

        internal static void Acquire()
        {
            if (count++ != 0) return;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        internal static void Release()
        {
            if (count > 0 && --count == 0) Time.timeScale = previousTimeScale;
        }
    }
}
