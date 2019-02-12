using System;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class AsyncLocalGuidSeedRandomProvider : IRandomProvider
    {
        // Implementation that seeds with a better distribution across multiple machines than the default time-based seed, uses
        // the recommended AsyncLocal for transition, and provides better distribution across multiple threads/async-contexts on the
        // same machine vs. the time-based approach (which would result in duplicate random sequences for Random()'s created in the
        // same Environment.TickCount (a 10-16 millisecond window)
        private readonly AsyncLocalCompat<Random> _random = new AsyncLocalCompat<Random>();
        private int _seed = Guid.NewGuid().GetHashCode();

        private AsyncLocalGuidSeedRandomProvider()
        {
        }

        public static AsyncLocalGuidSeedRandomProvider Instance { get; } = new AsyncLocalGuidSeedRandomProvider();

        private int NextSeed => Interlocked.Increment(ref _seed);

        public Random GetRandom() => _random.GetOrSet(() => new Random(NextSeed));
    }
}
