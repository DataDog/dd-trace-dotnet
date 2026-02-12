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
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using FluentAssertions;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Mvc;
#endif
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SpanCodeOriginTests
    {
        private const string CodeOriginTag = "_dd.code_origin";

        internal static SpanCodeOrigin CreateSpanCodeOrigin(bool isEnable = true, int numberOfFrames = 8, string excludeFromFilter = "Datadog.Trace.Tests")
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, isEnable.ToString() },
                        { ConfigurationKeys.Debugger.CodeOriginMaxUserFrames, numberOfFrames.ToString() },
                        { ConfigurationKeys.Debugger.ThirdPartyDetectionExcludes, excludeFromFilter }
                    }),
                NullConfigurationTelemetry.Instance);

            return new SpanCodeOrigin(settings);
        }

        public class EntrySpanTests
        {
            [Fact]
            public void SetCodeOriginForEntrySpan_WhenSpanIsNull_ShouldNotThrow()
            {
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var action = () => spanCodeOrigin.SetCodeOriginForEntrySpan(null, typeof(string), typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes));

                action.Should().NotThrow();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WhenTypeIsNull_ShouldNotThrow()
            {
                var span = CreateSpan();
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var action = () => spanCodeOrigin.SetCodeOriginForEntrySpan(span, null, typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes));

                action.Should().NotThrow();
                span.GetTag($"{CodeOriginTag}.type").Should().BeNull();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WhenMethodIsNull_ShouldNotThrow()
            {
                var span = CreateSpan();
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var action = () => spanCodeOrigin.SetCodeOriginForEntrySpan(span, typeof(string), null);

                action.Should().NotThrow();
                span.GetTag($"{CodeOriginTag}.type").Should().BeNull();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WhenSpanAlreadyHasCodeOrigin_ShouldNotModifyTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                span.SetTag($"{CodeOriginTag}.type", "existing");

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, GetType(), GetType().GetMethod(nameof(SetCodeOriginForEntrySpan_WhenSpanAlreadyHasCodeOrigin_ShouldNotModifyTags)));

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().BeEquivalentTo("existing");
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WithValidInputs_ShouldSetCorrectTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var type = GetType();
                var method = type.GetMethod(nameof(TestMethod), BindingFlags.Instance | BindingFlags.NonPublic);

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().Be("entry");
                span.GetTag($"{CodeOriginTag}.frames.0.index").Should().Be("0");
                span.GetTag($"{CodeOriginTag}.frames.0.method").Should().Be(nameof(TestMethod));
                span.GetTag($"{CodeOriginTag}.frames.0.type").Should().Be(type.FullName);
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WithWebTags_ShouldSetCorrectTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateWebSpan();
                var type = GetType();
                var method = type.GetMethod(nameof(TestMethod), BindingFlags.Instance | BindingFlags.NonPublic);

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().Be("entry");
                span.GetTag($"{CodeOriginTag}.frames.0.index").Should().Be("0");
                span.GetTag($"{CodeOriginTag}.frames.0.method").Should().Be(nameof(TestMethod));
                span.GetTag($"{CodeOriginTag}.frames.0.type").Should().Be(type.FullName);
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_WithThirdPartyAssembly_ShouldNotSetTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
                var type = method.DeclaringType;

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().BeNull();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_ForNonControllerAction_ShouldNotSetTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var type = GetType();
                var method = type.GetMethod(nameof(TestMethod), BindingFlags.Instance | BindingFlags.NonPublic);

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().Be("entry");
                span.GetTag($"{CodeOriginTag}.frames.0.method").Should().Be(nameof(TestMethod));
                span.GetTag($"{CodeOriginTag}.frames.{0}.file").Should().BeNull();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_ForNonControllerAction_WithAsyncMethod_ShouldSetCorrectTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var method = GetType().GetMethod(nameof(AsyncTestMethod), BindingFlags.Instance | BindingFlags.NonPublic);
                var type = method.DeclaringType;

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().Be("entry");
                span.GetTag($"{CodeOriginTag}.frames.0.method").Should().Be(nameof(AsyncTestMethod));
                span.GetTag($"{CodeOriginTag}.frames.{0}.file").Should().BeNull();
            }

            [Fact]
            public void SetCodeOriginForEntrySpan_ForNonControllerAction_WithGenericMethod_ShouldSetCorrectTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var method = GetType().GetMethod(nameof(GenericTestMethod), BindingFlags.Instance | BindingFlags.NonPublic);
                var type = method.DeclaringType;

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, type, method);

                // Assert
                span.GetTag($"{CodeOriginTag}.type").Should().Be("entry");
                span.GetTag($"{CodeOriginTag}.frames.0.method").Should().Be(nameof(GenericTestMethod));
                span.GetTag($"{CodeOriginTag}.frames.{0}.file").Should().BeNull();
            }

#if !NETFRAMEWORK
            [Fact]
            public void SetCodeOriginForEntrySpan_ForControllerAction_ShouldSetTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                var span = CreateSpan();
                var controllerType = typeof(TestController);
                var method = controllerType.GetMethod(nameof(TestController.Get));

                // Act
                spanCodeOrigin.SetCodeOriginForEntrySpan(span, controllerType, method);

                // Assert
                span.GetTag("_dd.code_origin.type").Should().Be("entry");
                span.GetTag("_dd.code_origin.frames.0.method").Should().Be(nameof(TestController.Get));
                span.GetTag("_dd.code_origin.frames.0.type").Should().Be("Datadog.Trace.Tests.Debugger.SpanCodeOriginTests+TestController");
                span.GetTag($"{CodeOriginTag}.frames.{0}.file").Should().EndWithEquivalentOf("SpanCodeOriginTests.cs");
            }
#endif

            private Span CreateSpan()
            {
                var spanContext = new SpanContext(1234, 5678);
                return new Span(spanContext, DateTimeOffset.UtcNow);
            }

            private Span CreateWebSpan()
            {
                var spanContext = new SpanContext(1234, 5678);
                return new Span(spanContext, DateTimeOffset.UtcNow, new WebTags());
            }

            private int TestMethod() => 42;

            private async Task AsyncTestMethod()
            {
                await Task.Delay(1);
            }

            private T GenericTestMethod<T>(T input)
                where T : class
            {
                return input;
            }
        }

        public class ExitSpanTests
        {
            [Fact]
            public void SetCodeOrigin_WhenSpanIsNull_DoesNotThrow()
            {
                // Should not throw
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();
                spanCodeOrigin.SetCodeOriginForExitSpan(null);
            }

            [Fact]
            public void SetCodeOrigin_WhenEnabled_SetsCorrectTags()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin();

                var span = new Span(new SpanContext(1, 2, SamplingPriority.UserKeep), DateTimeOffset.UtcNow);

                // Act
                TestMethod(spanCodeOrigin, span);

                // Assert
                // Exit span code origin has been disabled since tracer version 3.28.0.
                span.Tags.GetTag($"{CodeOriginTag}.type").Should().BeNull();

                /* Uncomment when exit span will be enabled again
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
                */
            }

            [Fact]
            public void SetCodeOrigin_WithMaxFramesLimit_RespectsLimit()
            {
                // Arrange
                SpanCodeOrigin spanCodeOrigin = CreateSpanCodeOrigin(numberOfFrames: 2);

                var span = new Span(new SpanContext(1, 2, SamplingPriority.UserKeep), DateTimeOffset.UtcNow);

                // Act
                DeepTestMethod1(span, spanCodeOrigin);

                // Assert
                // Exit span code origin has been disabled since tracer version 3.28.0.
                span.Tags.GetTag($"{CodeOriginTag}.type").Should().BeNull();

                /* Uncomment when exit span will be enabled again
                var tags = ((List<KeyValuePair<string, string>>)(typeof(Datadog.Trace.Tagging.TagsList).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(span.Tags))).Select(i => i.Key).ToList();
                tags.Should().Contain(s => s.StartsWith($"{CodeOriginTag}.frames.0"));
                tags.Should().Contain(s => s.StartsWith($"{CodeOriginTag}.frames.1"));
                tags.Should().NotContain(s => s.StartsWith($"{CodeOriginTag}.frames.2"));
                */
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void TestMethod(SpanCodeOrigin instance, Span span)
            {
                instance.SetCodeOriginForExitSpan(span);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void DeepTestMethod1(Span span, SpanCodeOrigin instance)
            {
                DeepTestMethod2(span, instance);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void DeepTestMethod2(Span span, SpanCodeOrigin instance)
            {
                DeepTestMethod3(span, instance);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void DeepTestMethod3(Span span, SpanCodeOrigin instance)
            {
                instance.SetCodeOriginForExitSpan(span);
            }
        }

        internal class GenericType<T>
        {
        }

#if !NETFRAMEWORK
        internal class TestController : Microsoft.AspNetCore.Mvc.ControllerBase
        {
            [HttpGet]
            public object Get() => null;
        }
#endif
    }
}
