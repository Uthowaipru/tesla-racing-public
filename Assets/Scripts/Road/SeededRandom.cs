namespace VehicleSystem
{
    /// <summary>
    /// A small random number generator that gives the SAME numbers on every device for the same seed (Unity's own Random does
    /// not promise that between versions and platforms). The road, the traffic and the pickups are all built from it, so
    /// every player who uses the host's seed gets the same road.
    ///
    /// Use For(seed, index, channel): the numbers for one chunk depend only on the seed, the chunk number and a channel
    /// (1 = which chunk, 2 = traffic, 3 = pickups ...), never on what was generated before. So a chunk can be built at any
    /// time and always looks the same.
    /// </summary>
    public struct SeededRandom
    {
        const ulong Golden = 0x9E3779B97F4A7C15UL;

        ulong state;

        public SeededRandom(ulong seed) { state = seed; }

        public static SeededRandom For(int seed, int index, int channel)
        {
            ulong h = (ulong)(uint)seed * Golden;
            h ^= (ulong)(uint)index * 0xBF58476D1CE4E5B9UL + 0x632BE59BD9B4E019UL;
            h ^= (ulong)(uint)channel * 0x94D049BB133111EBUL + 0x8CB92BA72F3D8DD7UL;
            return new SeededRandom(Mix(h));
        }

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            state += Golden;
            return Mix(state);
        }

        /// <summary>0 (inclusive) to 1 (exclusive).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>min (inclusive) to max (exclusive).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextULong() % (ulong)(max - min));
        }

        public bool Chance(float probability) => NextFloat() < probability;
    }
}
