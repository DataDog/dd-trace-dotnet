// <copyright file="IastSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class IastSettingsTests : SettingsTestsBase
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

    [Theory]
    [MemberData(nameof(StringTestCases), IastSettings.WeakCipherAlgorithmsDefault, Strings.AllowEmpty)]
    public void WeakCipherAlgorithms(string value, string expected)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.Iast.WeakCipherAlgorithms, value));
        var settings = new IastSettings(source);

        settings.WeakCipherAlgorithms.Should().Be(expected);
        settings.WeakCipherAlgorithmsArray.Should().BeEquivalentTo(expected.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [MemberData(nameof(StringTestCases), IastSettings.WeakHashAlgorithmsDefault, Strings.AllowEmpty)]
    public void WeakHashAlgorithms(string value, string expected)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.Iast.WeakHashAlgorithms, value));
        var settings = new IastSettings(source);

        settings.WeakHashAlgorithms.Should().Be(expected);
        settings.WeakHashAlgorithmsArray.Should().BeEquivalentTo(expected.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [MemberData(nameof(BooleanTestCases), false)]
    public void Enabled(string value, bool expected)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.Iast.Enabled, value));
        var settings = new IastSettings(source);

        settings.Enabled.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(BooleanTestCases), true)]
    public void DeduplicationEnabled(string value, bool expected)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.Iast.IsIastDeduplicationEnabled, value));
        var settings = new IastSettings(source);

        settings.DeduplicationEnabled.Should().Be(expected);
    }
}
