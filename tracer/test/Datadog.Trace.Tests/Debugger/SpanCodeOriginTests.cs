// <copyright file="SpanCodeOriginTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SpanCodeOriginTests
    {
        private const string CodeOriginTag = "_dd.code_origin";

        [Fact]
        public void SetCodeOrigin_WhenSpanIsNull_DoesNotThrow()
        {
            // Should not throw
            SpanCodeOriginManager.Instance.SetCodeOrigin(null);
        }

        [Fact]
        public void SetCodeOrigin_WhenDisabled_DoesNotSetTags()
        {
            // Arrange
            CreateCodeOriginManager();

            var span = new Span(new SpanContext(1, 2, SamplingPriority.UserKeep), DateTimeOffset.UtcNow);

            // Act
            SpanCodeOriginManager.Instance.SetCodeOrigin(span);

            // Assert
            span.Tags.GetTag(CodeOriginTag + ".type").Should().BeNull();
        }

        [Fact]
        public void SetCodeOrigin_WhenEnabled_SetsCorrectTags()
        {
            // Arrange
            CreateCodeOriginManager(true);

            var span = new Span(new SpanContext(1, 2, SamplingPriority.UserKeep), DateTimeOffset.UtcNow);

            // Act
            TestMethod(span);

            // Assert
            var codeOriginType = span.Tags.GetTag($"{CodeOriginTag}.type");
            codeOriginType.Should().Be("exit");
            var frame0Method = span.Tags.GetTag($"{CodeOriginTag}.frames.0.method");
            frame0Method.Should().Be(nameof(TestMethod));
            var frame0Type = span.Tags.GetTag($"{CodeOriginTag}.frames.0.type");
            frame0Type.Should().Be(GetType().FullName);
            var file = span.Tags.GetTag($"{CodeOriginTag}.frames.0.file");
            file.Should().EndWith($"{nameof(SpanCodeOriginTests)}.cs");
            var line = span.Tags.GetTag($"{CodeOriginTag}.frames.0.line");
            line.Should().NotBeNullOrEmpty();
            var column = span.Tags.GetTag($"{CodeOriginTag}.frames.0.column");
            column.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void SetCodeOrigin_WithMaxFramesLimit_RespectsLimit()
        {
            // Arrange
            CreateCodeOriginManager(true, 2);

            var span = new Span(new SpanContext(1, 2, SamplingPriority.UserKeep), DateTimeOffset.UtcNow);

            // Act
            DeepTestMethod1(span);

            // Assert
            var tags = ((List<KeyValuePair<string, string>>)(typeof(Datadog.Trace.Tagging.TagsList).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(span.Tags))).Select(i => i.Key).ToList();
            tags.Should().Contain(s => s.StartsWith($"{CodeOriginTag}.frames.0"));
            tags.Should().Contain(s => s.StartsWith($"{CodeOriginTag}.frames.1"));
            tags.Should().NotContain(s => s.StartsWith($"{CodeOriginTag}.frames.2"));
        }

        private static void CreateCodeOriginManager(bool isEnable = false, int numberOfFrames = 8, string excludeFromFilter = "Datadog.Trace.Tests")
        {
            var overrideSettings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, isEnable.ToString() },
                        { ConfigurationKeys.Debugger.CodeOriginMaxUserFrames, numberOfFrames.ToString() },
                        { ConfigurationKeys.Debugger.ThirdPartyDetectionExcludes, excludeFromFilter }
                    }),
                NullConfigurationTelemetry.Instance);
            var instance = SpanCodeOriginManager.Instance;
            instance.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, overrideSettings);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TestMethod(Span span)
        {
            SpanCodeOriginManager.Instance.SetCodeOrigin(span);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DeepTestMethod1(Span span)
        {
            DeepTestMethod2(span);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DeepTestMethod2(Span span)
        {
            DeepTestMethod3(span);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DeepTestMethod3(Span span)
        {
            SpanCodeOriginManager.Instance.SetCodeOrigin(span);
        }
    }
}
