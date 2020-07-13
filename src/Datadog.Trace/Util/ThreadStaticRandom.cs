using System;

namespace Datadog.Trace.Util
{
    internal class ThreadStaticRandom
    {
#if !NETSTANDARD
        private static readonly Random GlobalRandom = new Random();
#endif

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
    }
}
