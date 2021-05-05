using System;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
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
        [InlineData("https://apmjunkyardstorage.blob.core.windows.net/azure-wbjobs-hosts/locks/dd-netcore31/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", false)]
        [InlineData("https://apmjunkyardstorage.blob.core.linux.net/webjobs-hosts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", false)]
        [InlineData("https://dd-netcore31-junkyard-baseline.azurewebsites.net/", false)]
        [InlineData("https://www.google.com", false)]
        public void ShouldBypassUrl(string url, bool shouldBypass)
        {
            var settings = new TracerSettings();
            var didBypass = HttpBypassHelper.ShouldSkipResource(url, settings.HttpUrlPatternSkips);
            Assert.Equal(expected: shouldBypass, actual: didBypass);
        }
    }
}
