using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class RandomIdProvider : IIdProvider
    {
        private readonly IRandomProvider _randomProvider;

        internal RandomIdProvider(IRandomProvider randomProvider)
        {
            _randomProvider = randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
        }

        public static RandomIdProvider Instance { get; } = new RandomIdProvider(SimpleDependencyFactory.RandomProvider());

        public ulong GetUInt63Id()
        {
            // From https://stackoverflow.com/a/677390
            var buffer = new byte[sizeof(ulong)];

            _randomProvider.GetRandom().NextBytes(buffer);

            var nextId = BitConverter.ToUInt64(buffer, startIndex: 0) & (~(1 << 63));

            return nextId;
        }
    }
}
