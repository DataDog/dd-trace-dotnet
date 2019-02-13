using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal class LockedRandomIdProvider : IIdProvider
    {
        // Being a locked ID access provider, there's basically no reason anyone would ever use a non-singleton random provider...
        private readonly IRandomProvider _randomProvider = SingletonRandomProvider.Instance;

        private readonly object _lockObject = new object();

        internal LockedRandomIdProvider()
        {
        }

        public static LockedRandomIdProvider Instance { get; } = new LockedRandomIdProvider();

        public ulong GetUInt63Id()
        {
            // From https://stackoverflow.com/a/677390
            var buffer = new byte[sizeof(ulong)];

            lock (_lockObject)
            {
                _randomProvider.GetRandom().NextBytes(buffer);
            }

            var nextId = BitConverter.ToUInt64(buffer, startIndex: 0) & (~(1 << 63));

            return nextId;
        }
    }
}
