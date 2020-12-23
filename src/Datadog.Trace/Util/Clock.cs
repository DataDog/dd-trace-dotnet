using System;

namespace Datadog.Trace.Util
{
    internal class Clock
    {
        /// <summary>
        /// Flag used to avoid checking the threadstatic field when running outside of tests
        /// </summary>
        private static bool _overrideClock;

        [ThreadStatic]
        private static IClock _customClock;

        public static DateTime UtcNow
        {
            get
            {
                if (!_overrideClock)
                {
                    return DateTime.UtcNow;
                }

                return _customClock?.UtcNow ?? DateTime.UtcNow;
            }
        }

        internal static void Reset()
        {
            _overrideClock = false;
        }

        /// <summary>
        /// Overrides the clock used by the current thread.
        /// This method should be called only from unit tests.
        /// </summary>
        /// <param name="customClock">Fake clock</param>
        /// <returns>Lease to dispose to restore the original state</returns>
        internal static IDisposable SetForCurrentThread(IClock customClock)
        {
            _overrideClock = true;
            _customClock = customClock;
            return new Lease();
        }

        private class Lease : IDisposable
        {
            public void Dispose()
            {
                _customClock = null;
            }
        }
    }
}
