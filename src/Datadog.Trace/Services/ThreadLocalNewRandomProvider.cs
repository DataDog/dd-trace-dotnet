using System;
using System.Threading;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class ThreadLocalNewRandomProvider : IRandomProvider
    {
        // Existing/currently in-use random provider which relies on ThreadLocal storage as well as new-ing up a
        // Random() with the default seed for each ThreadLocal structure
        private readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

        private ThreadLocalNewRandomProvider()
        {
        }

        public static ThreadLocalNewRandomProvider Instance { get; } = new ThreadLocalNewRandomProvider();

        public Random GetRandom() => _random.Value;
    }
}
