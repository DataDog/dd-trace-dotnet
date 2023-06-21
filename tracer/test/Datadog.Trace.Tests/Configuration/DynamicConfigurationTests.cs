// <copyright file="DynamicConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class DynamicConfigurationTests
    {
        [Fact(Skip = "Disabled until service mapping is re-implemented in dynamic config")]
        public void ApplyServiceMappingToNewTraces()
        {
            var scope = Tracer.Instance.StartActive("Trace1");

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be($"{Tracer.Instance.DefaultServiceName}-test");

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig((ConfigurationKeys.ServiceNameMappings, "test:ok")));

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be($"{Tracer.Instance.DefaultServiceName}-test", "the old configuration should be used inside of the active trace");

            scope.Close();

            Tracer.Instance.CurrentTraceSettings.GetServiceName(Tracer.Instance, "test")
               .Should().Be("ok", "the new configuration should be used outside of the active trace");
        }

        [Fact]
        public void ApplyConfigurationTwice()
        {
            var tracer = TracerManager.Instance;

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig((ConfigurationKeys.GlobalSamplingRate, "0.4")));

            var newTracer = TracerManager.Instance;

            newTracer.Should().NotBeSameAs(tracer);

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig((ConfigurationKeys.GlobalSamplingRate, "0.4")));

            TracerManager.Instance.Should().BeSameAs(newTracer);
        }

        private static ConfigurationBuilder CreateConfig(params (string Key, string Value)[] settings)
        {
            var values = new NameValueCollection();

            foreach (var (key, value) in settings)
            {
                values[key] = value;
            }

            var configurationSource = new NameValueConfigurationSource(values);

            return new ConfigurationBuilder(configurationSource, Mock.Of<IConfigurationTelemetry>());
        }
    }
}
