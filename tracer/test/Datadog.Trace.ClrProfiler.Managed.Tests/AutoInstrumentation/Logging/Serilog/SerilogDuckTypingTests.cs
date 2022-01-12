// <copyright file="SerilogDuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission.Formatting;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Parsing;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using static Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog.SerilogHelper;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog
{
    public class SerilogDuckTypingTests
    {
        [Fact]
        public void CanDuckTypeMessageTemplate()
        {
            var instance = new Vendors.Serilog.Events.MessageTemplate("Some text", Enumerable.Empty<MessageTemplateToken>());
            instance.TryDuckCast(out MessageTemplateProxy duck).Should().BeTrue();
            duck.Should().NotBeNull();
            duck.Text.Should().Be(instance.Text);
        }

        [Fact]
        public void CanDuckTypeLogEvent()
        {
            var instance = new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Error,
                new Exception(),
                new Vendors.Serilog.Events.MessageTemplate("Some text", Enumerable.Empty<MessageTemplateToken>()),
                new[] { new LogEventProperty("SomeProp", new ScalarValue(123)) });

            instance.TryDuckCast(out ILogEvent duck).Should().BeTrue();
            var intLevel = (int)instance.Level;
            var intLevel2 = (LogEventLevelDuck)duck.Level;
            duck.Should().NotBeNull();
            duck.Exception.Should().Be(instance.Exception);
            intLevel2.Should().Be(intLevel);
            duck.Timestamp.Should().Be(instance.Timestamp);
            duck.MessageTemplate.Text.Should().Be(instance.MessageTemplate.Text);
            var properties = new List<KeyValuePairStringStruct>();

            foreach (var duckProperty in duck.Properties)
            {
                properties.Add(duckProperty.DuckCast<KeyValuePairStringStruct>());
            }

            foreach (var property in instance.Properties)
            {
                properties.Should()
                    .ContainSingle(
                         x => x.Key == property.Key
                           && x.Value.ToString() == property.Value.ToString());
            }
        }

        [Fact]
        public void CanDuckTypeLoggerConfiguration()
        {
            var config = new LoggerConfiguration();

            config.TryDuckCast(out ILoggerConfiguration duckConfig).Should().BeTrue();
            duckConfig.Should().NotBeNull();
            duckConfig.LogEventSinks.Should().BeEmpty();

            Type sinkType = typeof(ILogEventSink);
            var sink = new TestSerilogSink();

            var duckSink = sink.DuckImplement(sinkType);
            duckConfig.LogEventSinks.Add(duckSink);

            var logger = config.CreateLogger();
            var message = "This is a test";
            logger.Information(message);

            sink.Logs.Should().ContainSingle(x => x == message);
        }

        [Fact]
        public void CanReverseDuckTypeSerilogSink()
        {
            var sink = new DirectSubmissionSerilogSink(
                new TestSink(),
                DirectSubmissionLogLevel.Information);

            Type sinkType = typeof(ILogEventSink);
            var duckSink = (ILogEventSink)sink.DuckImplement(sinkType);

            GetSerilogMessageProcessor()
                         .Process("This is {SomeValue}", new object[] { "someValue" }, out var parsedTemplate, out var boundProperties);
            var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, exception: null, parsedTemplate, boundProperties);

            duckSink.Emit(logEvent);
        }

        [Theory]
        [InlineData(123)]
        [InlineData("test")]
        [InlineData(1.23)]
        public void CanDuckTypeScalarLogValue(object value)
        {
            var scalar = new ScalarValue(value);

            scalar.TryDuckCast<ScalarValueDuck>(out var duck).Should().BeTrue();
            duck.Value.Should().Be(value);
        }

        [Theory]
        [InlineData(123, 456, 789)]
        [InlineData("test", "testage")]
        [InlineData(1.23, 4.56)]
        public void CanDuckTypeSequenceValue(params object[] values)
        {
            var seq = new SequenceValue(values.Select(x => new ScalarValue(x)));

            seq.TryDuckCast<SequenceValueDuck>(out var duck).Should().BeTrue();
            duck.Should().NotBeNull();
            using var scope = new AssertionScope();
            var wrappedValues = new object[values.Length];
            var i = 0;
            foreach (var element in duck.Elements)
            {
                element.TryDuckCast<ScalarValueDuck>(out var scalar).Should().BeTrue();
                wrappedValues[i] = scalar.Value;
                i++;
            }

            wrappedValues.Should().ContainInOrder(values);
        }

        [Theory]
        [InlineData(123, 456, 789)]
        [InlineData("test", "testage")]
        [InlineData(1.23, 4.56)]
        public void CanDuckTypeStructureValue(params object[] values)
        {
            var typeTag = "MyTestClass";
            var structure = new StructureValue(
                values.Select((x, i) => new LogEventProperty($"Value{i}", new ScalarValue(x))),
                typeTag);

            structure.TryDuckCast<StructureValueDuck>(out var duck).Should().BeTrue();
            duck.Should().NotBeNull();
            duck.TypeTag.Should().Be(typeTag);

            var duckValues = duck.Properties
                                 .Cast<object>()
                                 .Select(x => x.DuckCast<LogEventPropertyDuck>())
                                 .ToList();

            duckValues.Select(x => x.Name)
                      .Should()
                      .ContainInOrder(values.Select((_, i) => $"Value{i}"));

            duckValues.Select(x => x.Value.DuckCast<ScalarValueDuck>().Value)
                      .Should()
                      .ContainInOrder(values);
        }

        [Theory]
        [InlineData(123, 456, 789)]
        [InlineData("test", "testage")]
        [InlineData(1.23, 4.56)]
        public void CanDuckTypeDictionaryValue(params object[] values)
        {
            var dict = new DictionaryValue(
                values.Select(
                    (x, i) => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        new ScalarValue($"Value{i}"),
                        new ScalarValue(x))));

            dict.TryDuckCast<DictionaryValueDuck>(out var duck).Should().BeTrue();
            duck.Should().NotBeNull();

            var duckValues = duck.Elements
                                 .Cast<object>()
                                 .Select(x => x.DuckCast<KeyValuePairObjectStruct>())
                                 .ToList();

            duckValues.Select(x => x.Key.DuckCast<ScalarValueDuck>().Value)
                      .Should()
                      .ContainInOrder(values.Select((_, i) => $"Value{i}"));

            duckValues.Select(x => x.Value.DuckCast<ScalarValueDuck>().Value)
                      .Should()
                      .ContainInOrder(values);
        }

        internal class TestSerilogSink
        {
            public List<string> Logs { get; } = new();

            [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.Vendors.Serilog.Events.LogEvent, Datadog.Trace" })]
            public void Emit(ILogEvent logEvent)
            {
                Logs.Add(logEvent.RenderMessage());
            }
        }
    }
}
