using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class RandomExtensions
    {
        private static readonly Random GlobalRandom = new Random();

        [ThreadStatic]
        private static Random _threadRandom;

        public static Random GetRandom()
        {
            var random = _threadRandom;

            if (random == null)
            {
#if NETSTANDARD
                random = new Random();
#else
                // On .net framework, the clock is used to seed the new random instances
                // Resolution is too low, so if a bunch of threads are created at the same time,
                // they'll all get the same seed.
                // Instead, use a shared random instance to generate the seed
                int seed;

                lock (GlobalRandom)
                {
                    seed = GlobalRandom.Next();
                }

                random = new Random(seed);
#endif

                _threadRandom = random;
            }

            return random;
        }

        public static ulong NextUInt63(this Random rnd)
        {
            long high = rnd.Next(int.MinValue, int.MaxValue);
            long low = rnd.Next(int.MinValue, int.MaxValue);

            // Concatenate both values, and truncate the 32 top bits from low
            var value = high << 32 | (low & 0xFFFFFFFF);

            return (ulong)value & 0x7FFFFFFFFFFFFFFF;
        }
    }
}
