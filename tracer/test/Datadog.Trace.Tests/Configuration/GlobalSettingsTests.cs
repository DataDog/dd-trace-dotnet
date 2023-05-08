// <copyright file="GlobalSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class GlobalSettingsTests : SettingsTestsBase
    {
        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void DebugEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DebugEnabled, value));
            var settings = new GlobalSettings(source);

            settings.DebugEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void DiagnosticSourceEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DiagnosticSourceEnabled, value));
            var settings = new GlobalSettings(source);

            settings.DiagnosticSourceEnabled.Should().Be(expected);
        }
    }
}
