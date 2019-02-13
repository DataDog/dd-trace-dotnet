using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class SingletonRandomProvider : IRandomProvider
    {
        // Simple provider that returns reference to a singleton Random (which is not thread-safe)
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());

        private SingletonRandomProvider()
        {
        }

        public static SingletonRandomProvider Instance { get; } = new SingletonRandomProvider();

        public Random GetRandom() => _random;
    }
}
