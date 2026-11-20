using UnityEngine;

namespace DuelHero.Battle
{
    internal static class GameTimePause
    {
        private static int count;
        private static float previousTimeScale;
        private static bool slowed;
        private static float speed;

        internal static void Acquire()
        {
            if (count++ != 0) return;
            if (!slowed) previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        internal static void Release()
        {
            if (count > 0 && --count == 0) Time.timeScale = previousTimeScale * (slowed ? speed : 1f);
        }

        internal static void BeginSlowMotion(float multiplier)
        {
            if (!slowed && count == 0) previousTimeScale = Time.timeScale;
            slowed = true;
            speed = multiplier;
            if (count == 0) Time.timeScale = previousTimeScale * speed;
        }

        internal static void EndSlowMotion()
        {
            if (!slowed) return;
            slowed = false;
            if (count == 0) Time.timeScale = previousTimeScale;
        }
    }
}
