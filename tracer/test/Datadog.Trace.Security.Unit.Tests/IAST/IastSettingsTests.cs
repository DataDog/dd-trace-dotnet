// <copyright file="IastSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class IastSettingsTests
{
    [Fact]
    public void GivenIastSettings_WhenSetRequestSamplingTo50_RequestSamplingIs50()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 50 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(50, iastSettings.RequestSampling);
    }

    [Fact]
    public void GivenIastSettings_WhenSetRequestSamplingTo150_RequestSamplingIsDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 150 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(IastSettings.RequestSamplingDefault, iastSettings.RequestSampling);
    }

    [Fact]
    public void GivenIastSettings_WhenSetRequestSamplingToMinus1_RequestSamplingIsDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, -1 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(IastSettings.RequestSamplingDefault, iastSettings.RequestSampling);
    }

    [Fact]
    public void GivenIastSettings_WhenMaxConcurrentRequestsTo5_MaxConcurrentRequestsIsDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.MaxConcurrentRequests, 5 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(IastSettings.RequestSamplingDefault, iastSettings.RequestSampling);
    }

    [Fact]
    public void GivenIastSettings_WhenSetMaxConcurrentRequestsToMinus1_MaxConcurrentRequestsIsDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.MaxConcurrentRequests, -1 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(IastSettings.MaxConcurrentRequestDefault, iastSettings.MaxConcurrentRequests);
    }

    [Fact]
    public void GivenIastSettings_WhenVulnerabilitiesPerRequestTo5_VulnerabilitiesPerRequestDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.VulnerabilitiesPerRequest, 5 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(5, iastSettings.VulnerabilitiesPerRequest);
    }

    [Fact]
    public void GivenIastSettings_WhenSetVulnerabilitiesPerRequestToMinus1_VulnerabilitiesPerRequestIsDefaultValue()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.VulnerabilitiesPerRequest, -1 }
        });
        var iastSettings = new IastSettings(settings);
        Assert.Equal(IastSettings.VulnerabilitiesPerRequestDefault, iastSettings.VulnerabilitiesPerRequest);
    }
}
