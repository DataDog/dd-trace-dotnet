using System;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class HttpBypassTests
    {
        [Theory]
        [InlineData("http://dd-netcore31-junkyard-parallel-development.azurewebsites.net/admin/warmup", true)]
        [InlineData("https://rt.services.visualstudio.com/QuickPulseService.svc/ping", true)]
        [InlineData("https://eastus2-3.in.applicationinsights.azure.com/v2/track", true)]
        [InlineData("https://apmjunkyardstorage.blob.core.windows.net/azure-webjobs-hosts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", true)]
        [InlineData("https://dd-netcore31-junkyard-baseline.azurewebsites.net/", false)]
        [InlineData("https://www.google.com", false)]
        public void ShouldBypassUrl(string url, bool shouldBypass)
        {
            var didBypass = HttpBypassHelper.ShouldSkipResource(new Uri(url));
            Assert.Equal(expected: shouldBypass, actual: didBypass);
        }
    }
}
