namespace DuelHero.Battle
{
    // Local integer PRNG: independent of Unity's global random state and runtime string hashes.
    internal struct EnemyDecisionRandom
    {
        private uint state;
        internal EnemyDecisionRandom(uint seed) => state = seed;
        internal int Next(int exclusiveMax)
        {
            state = unchecked(state + 0x9E3779B9u);
            uint value = state;
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            return (int)(value % (uint)exclusiveMax);
        }

        internal static uint CreateSeed(uint runSeed, string nodeId, string actorId, int round)
        {
            uint hash = Mix(2166136261u, runSeed);
            hash = Text(hash, nodeId); hash = Text(hash, actorId);
            return Mix(hash, unchecked((uint)round));
        }
        private static uint Text(uint hash, string text)
        {
            hash = Mix(hash, (uint)(text?.Length ?? 0));
            if (text != null) foreach (char value in text) hash = unchecked((hash ^ value) * 16777619u);
            return hash;
        }
        private static uint Mix(uint hash, uint value)
        {
            for (int i = 0; i < 4; i++) { hash = unchecked((hash ^ (value & 255u)) * 16777619u); value >>= 8; }
            return hash;
        }
    }
}
