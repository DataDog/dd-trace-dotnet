// <copyright file="HttpBypassTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.PlatformHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class HttpBypassTests
    {
        [Theory]
        [InlineData("http://dd-netcore31-junkyard-parallel-development.azurewebsites.net/admin/warmup", true)]
        [InlineData("https://rt.services.visualstudio.com/QuickPulseService.svc/ping", true)]
        [InlineData("https://eastus2-3.in.applicationinsights.azure.com/v2/track", true)]
        [InlineData("https://EASTUS2-3.IN.APPLICATIONINSIGHTS.AZURE.COM/V2/TRACK", true)]
        [InlineData("https://apmjunkyardstorage.blob.core.windows.net/azure-webjobs-hosts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", true)]
        [InlineData("https://apmjunkyardstorage.blob.core.windows.net/azure-WeBJobS-hoSts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", true)]
        [InlineData("https://apmjunkyardstorage.blob.core.windows.net/azure-wbjobs-hosts/locks/dd-netcore31/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", false)]
        [InlineData("https://apmjunkyardstorage.blob.core.linux.net/webjobs-hosts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", false)]
        [InlineData("https://apmjunkyardstorage.blob.core.LINUX.net/webjobs-hosts/locks/dd-netcore31-junkyard-parallel-d/JunkyardLoad.JunkyardLoad.JunkyardNetcore31CallTargetFull.Listener", false)]
        [InlineData("https://dd-netcore31-junkyard-baseline.azurewebsites.net/", false)]
        [InlineData("https://DD-NETCORE31-JUNKYARD-BASELINE.AZUREWEBSITES.nNET/", false)]
        [InlineData("https://www.datadoghq.com", false)]
        public void ShouldBypassUrlInAzureAppService(string url, bool shouldBypass)
        {
            var exclusions = AzureAppServices.Metadata.DefaultHttpClientExclusions.Replace(" ", string.Empty).Split(',');
            var didBypass = HttpBypassHelper.UriContainsAnyOf(new Uri(url), exclusions);
            Assert.Equal(expected: shouldBypass, actual: didBypass);
        }
    }
}
