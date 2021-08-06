using System;
using System.Threading;

namespace CrossDomainTest
{
    public static class AppDomainHelpers
    {
        // Ensure the remote call takes long enough for the lease manager poll to occur.
        // Even though we reset LifetimeServices.LeaseManagerPollTime to a shorter duration,
        // the default value is 10 seconds so the first poll may not be affected by our modification
        public static void SleepForLeaseManagerPollCallback() => Thread.Sleep(TimeSpan.FromSeconds(12));

        public static void EmptyCallback()
        {
        }
    }
}
