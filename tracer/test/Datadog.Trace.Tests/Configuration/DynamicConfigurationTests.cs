// <copyright file="DynamicConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
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

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_service_mapping", "'test:ok'")));

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

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_sampling_rate", "0.4")));

            var newTracer = TracerManager.Instance;

            newTracer.Should().NotBeSameAs(tracer);

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_sampling_rate", "0.4")));

            TracerManager.Instance.Should().BeSameAs(newTracer);
        }

        [Fact]
        public void ApplyTagsToDirectLogs()
        {
            var tracerSettings = new TracerSettings();

            tracerSettings.GlobalTagsInternal.Add("key1", "value1");

            TracerManager.ReplaceGlobalManager(new ImmutableTracerSettings(tracerSettings), TracerManagerFactory.Instance);

            TracerManager.Instance.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_tags", "['key2:value2']")));

            TracerManager.Instance.DirectLogSubmission.Formatter.Tags.Should().Be("key2:value2");
        }

        [Fact]
        public void DoesNotOverrideDirectLogsTags()
        {
            var tracerSettings = new TracerSettings();
            tracerSettings.LogSubmissionSettings.DirectLogSubmissionGlobalTags.Add("key1", "value1");
            tracerSettings.LogSubmissionSettings.DirectLogSubmissionEnabledIntegrations.Add("test");

            tracerSettings.GlobalTagsInternal.Add("key2", "value2");

            TracerManager.ReplaceGlobalManager(new ImmutableTracerSettings(tracerSettings), TracerManagerFactory.Instance);

            TracerManager.Instance.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_tags", "['key3:value3']")));

            TracerManager.Instance.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");
        }

        [Fact]
        public void EnableTracing()
        {
            var tracerSettings = new TracerSettings();
            TracerManager.ReplaceGlobalManager(new ImmutableTracerSettings(tracerSettings), TracerManagerFactory.Instance);

            TracerManager.Instance.Settings.TraceEnabled.Should().BeTrue();

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_enabled", "false")));
            TracerManager.Instance.Settings.TraceEnabled.Should().BeFalse();

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_enabled", "true")));
            TracerManager.Instance.Settings.TraceEnabled.Should().BeTrue();
        }

        private static ConfigurationBuilder CreateConfig(params (string Key, string Value)[] settings)
        {
            var jsonBuilder = new StringBuilder();

            jsonBuilder.AppendLine("{");
            jsonBuilder.AppendLine("\"lib_config\":");
            jsonBuilder.AppendLine("{");

            foreach (var (key, value) in settings)
            {
                jsonBuilder.AppendLine($"\"{key}\": \"{value}\",");
            }

            jsonBuilder.AppendLine("}");
            jsonBuilder.AppendLine("}");

            var configurationSource = new DynamicConfigConfigurationSource(jsonBuilder.ToString(), ConfigurationOrigins.RemoteConfig);
            return new ConfigurationBuilder(configurationSource, Mock.Of<IConfigurationTelemetry>());
        }
    }
}
