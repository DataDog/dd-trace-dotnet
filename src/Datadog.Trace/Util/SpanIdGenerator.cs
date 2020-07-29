using System;

namespace Datadog.Trace.Util
{
    internal class SpanIdGenerator
    {
#if !NETSTANDARD
        private static readonly Random GlobalSeedGenerator = new Random();
#endif

        [ThreadStatic]
        private static Random _threadRandom;

        public static ulong CreateNew()
        {
            Random random = GetRandom();

            long high = random.Next(int.MinValue, int.MaxValue);
            long low = random.Next(int.MinValue, int.MaxValue);

            // Concatenate both values, and truncate the 32 top bits from low
            var value = high << 32 | (low & 0xFFFFFFFF);

            return (ulong)value & 0x7FFFFFFFFFFFFFFF;
        }

        private static Random GetRandom()
        {
            Random random = _threadRandom;

            if (random == null)
            {
#if NETSTANDARD
                random = new Random();
#else
                // On .NET Framework, the clock is used to seed the new random instances.
                // Resolution is too low, so if many threads are created at the same time,
                // some could end up using the same seed.
                // Instead, use a shared random instance to generate the seed.
                // The same approach was used for System.Random on .NET Core:
                // https://docs.microsoft.com/en-us/dotnet/api/system.random.-ctor?view=netcore-3.1
                int seed;

                lock (GlobalSeedGenerator)
                {
                    seed = GlobalSeedGenerator.Next();
                }

                random = new Random(seed);
#endif

                _threadRandom = random;
            }

            return random;
        }
    }
}
