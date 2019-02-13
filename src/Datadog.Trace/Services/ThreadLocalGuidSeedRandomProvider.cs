using System;
using System.Threading;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class ThreadLocalGuidSeedRandomProvider : IRandomProvider
    {
        // Implementation that seeds with a better distribution across multiple machines than the default time-based seed, continues
        // to use ThreadLocal storage primarily to limit the number of Random objects generated (getting a random value from a
        // ThreadLocal storage that isn't in the same async flow is fine here, we just want good randomness that is thread-safe),
        // and provides better distribution across multiple threads/async-contexts on the same machine vs. the time-based approach
        // (which would result in duplicate random sequences for Random()'s created in the same Environment.TickCount (a 10-16 millisecond window)
        private readonly ThreadLocal<Random> _random;

        private int _seed = Guid.NewGuid().GetHashCode();

        private ThreadLocalGuidSeedRandomProvider()
        {
            _random = new ThreadLocal<Random>(() => new Random(NextSeed));
        }

        public static ThreadLocalGuidSeedRandomProvider Instance { get; } = new ThreadLocalGuidSeedRandomProvider();

        private int NextSeed => Interlocked.Increment(ref _seed);

        public Random GetRandom() => _random.Value;
    }
}
