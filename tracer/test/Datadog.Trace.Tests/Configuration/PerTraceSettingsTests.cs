// <copyright file="PerTraceSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class PerTraceSettingsTests
    {
        [Fact]
        public void ApplyServiceMappingToNewTraces()
        {
            Tracer.Configure(TracerSettings.Create(new()
            {
                [ConfigurationKeys.ServiceNameMappings] = "test:before"
            }));

            var scope = Tracer.Instance.StartActive("Trace1");

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be("before");

            Tracer.Configure(TracerSettings.Create(new()
            {
                [ConfigurationKeys.ServiceNameMappings] = "test:after"
            }));

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be("before", "the old configuration should be used inside of the active trace");

            scope.Close();

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be("after", "the new configuration should be used outside of the active trace");
        }
    }
}
