namespace Emberfall.Core.Combat
{
    /// <summary>
    /// xorshift32. Same seed always produces the same battle, on any platform,
    /// which is what makes the visual playback a faithful replay of the simulation.
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() & 0x00FFFFFF) / 16777216f;
        }

        /// <summary>Uniform in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability)
        {
            if (probability >= 1f) return true;
            if (probability <= 0f) return false;
            return NextFloat() < probability;
        }

        /// <summary>Uniform in [-spread, +spread].</summary>
        public float Spread(float spread)
        {
            return (NextFloat() * 2f - 1f) * spread;
        }
    }
}
