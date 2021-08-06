using System;
using System.Runtime.Remoting.Services;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;

namespace CrossDomainTest
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            // Arrange
            var prefix = Guid.NewGuid().ToString();
            var cde = new CountdownEvent(2);
            var tracker = new InMemoryRemoteObjectTracker(cde, prefix);
            TrackingServices.RegisterTrackingHandler(tracker);

            Console.WriteLine("Tracking configured, creating app domain");

            // Set the minimum permissions needed to run code in the new AppDomain
            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
            var remote = AppDomain.CreateDomain("Remote", null, AppDomain.CurrentDomain.SetupInformation, permSet);

            Console.WriteLine("App domain created, starting traces");

            // Act
            try
            {
                using (tracer.StartActive($"{prefix}test-span"))
                {
                    remote.DoCallBack(AppDomainHelpers.EmptyCallback);

                    using (tracer.StartActive($"{prefix}test-span-inner"))
                    {
                        remote.DoCallBack(AppDomainHelpers.EmptyCallback);
                    }
                }
            }
            finally
            {
                Console.WriteLine("Unloading app domain");
                AppDomain.Unload(remote);
            }

            Console.WriteLine("Waiting for lease manager...");

            // Ensure that we wait long enough for the lease manager poll to occur.
            // Even though we reset LifetimeServices.LeaseManagerPollTime to a shorter duration,
            // the default value is 10 seconds so the first poll may not be affected by our modification
            bool eventSet = cde.Wait(TimeSpan.FromSeconds(30));

            // Assert
            if (eventSet && tracker.DisconnectCount == 2)
            {
                Console.WriteLine("Expected number of disconnects seen");
                return 0;
            }

            throw new Exception(
                $"Expected 'eventSet' to be true and 'tracer.DisconnectCount' to be 2, " +
                $"but found {eventSet} and {tracker.DisconnectCount} respectively");
        }
    }
}
