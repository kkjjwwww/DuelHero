using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Run
{
    public static class RunSessionStore
    {
        public static RunSession Current { get; private set; }

        public static RunSession StartNew(uint seed, int startingHealth, int maxHealth,
            IEnumerable<string> startingCardIds)
        {
            // Construct first so invalid input cannot discard the current run.
            var session = new RunSession(seed, startingHealth, maxHealth, startingCardIds);
            Current = session;
            return session;
        }

        public static void Clear() => Current = null;

        // Also reset when Enter Play Mode has domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
