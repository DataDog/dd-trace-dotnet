// <copyright file="ReverseProxyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading;
using FluentAssertions;
using Serilog;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1602 // Enumeration items should be documented

namespace Datadog.Trace.DuckTyping.Tests
{
    public class ReverseProxyTests
    {
        [Fact]
        public void PrivateInterfaceReverseProxyTest()
        {
            Type iLogEventEnricherType = typeof(Datadog.Trace.Vendors.Serilog.Core.ILogEventEnricher);

            var resetEvent = new ManualResetEventSlim();

            var instance = new InternalLogEventEnricherImpl(resetEvent);

            var proxy = instance.DuckImplement(iLogEventEnricherType);

            var log = new Vendors.Serilog.LoggerConfiguration()
                .Enrich.With((Vendors.Serilog.Core.ILogEventEnricher)proxy)
                .MinimumLevel.Debug()
                .WriteTo.Sink(new Vendors.Serilog.Sinks.File.NullSink())
                .CreateLogger();

            log.Information("Hello world");

            Assert.True(resetEvent.Wait(5_000));
        }

        [Fact]
        public void PrivateAbstractClassReverseProxyTest()
        {
            var resetEvent = new ManualResetEventSlim();

            var eventInstance = new LogEventPropertyValueImpl(resetEvent);

            var type = typeof(Datadog.Trace.Vendors.Serilog.Events.LogEventPropertyValue);
            var proxy2 = eventInstance.DuckImplement(type);
            eventInstance.SetBaseInstance(proxy2);

            ((Datadog.Trace.Vendors.Serilog.Events.LogEventPropertyValue)proxy2).ToString("Hello world", null);

            Assert.True(resetEvent.Wait(5_000));
        }

        [Fact]
        public void PublicInterfaceReverseProxyTest()
        {
            Type iLogEventEnricherType = typeof(Serilog.Core.ILogEventEnricher);

            var resetEvent = new ManualResetEventSlim();

            var instance = new PublicLogEventEnricherImpl(resetEvent);

            var proxy = instance.DuckImplement(iLogEventEnricherType);
            var log = new Serilog.LoggerConfiguration()
                .Enrich.With((Serilog.Core.ILogEventEnricher)proxy)
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            log.Information("Hello world");

            Assert.True(resetEvent.Wait(5_000));
        }

        [Fact]
        public void PublicAbstractClassReverseProxyTest()
        {
            var resetEvent = new ManualResetEventSlim();

            var eventInstance = new LogEventPropertyValueImpl(resetEvent);

            var type = typeof(Serilog.Events.LogEventPropertyValue);

            var proxy2 = eventInstance.DuckImplement(type);
            eventInstance.SetBaseInstance(proxy2);

            ((Serilog.Events.LogEventPropertyValue)proxy2).ToString("Hello world", null);

            Assert.True(resetEvent.Wait(5_000));
        }

        [Fact]
        public void InternalClassWithVirtualMembersReverseProxyTest()
        {
            var expected = "Some random string";

            var formatterInstance = new JsonValueFormatterImpl(expected);

            var type = typeof(Datadog.Trace.Vendors.Serilog.Formatting.Json.JsonValueFormatter);

            var proxy2 = formatterInstance.DuckImplement(type);

            var value = new Vendors.Serilog.Events.ScalarValue("original");

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            ((Datadog.Trace.Vendors.Serilog.Formatting.Json.JsonValueFormatter)proxy2).Format(value, sw);

            var actual = sb.ToString();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PublicClassWithVirtualMembersReverseProxyTest()
        {
            var expected = nameof(PublicClassWithVirtualMembers);

            var instance = new PublicClassWithVirtualMembers();

            var type = typeof(PublicAbstractClassWithProperties);

            var proxy2 = instance.DuckImplement(type);

            var actual = ((PublicAbstractClassWithProperties)proxy2).GetClassName();
            var syncRoot = ((PublicAbstractClassWithProperties)proxy2).GetSyncRoot();

            Assert.Equal(expected, actual);
            Assert.NotNull(syncRoot);
        }

        [Fact]
        public void ReverseProxyInvokesBaseConstructorTest()
        {
            var instance = new AbstractBaseWithConstructorClassDuck();
            var proxy = (AbstractBaseWithConstructorClass)instance.DuckImplement(typeof(AbstractBaseWithConstructorClass));

            proxy.PrivateStaticReadonly.Should().NotBeNull();
            proxy.PrivateReadonly.Should().NotBeNull();
            proxy.Private.Should().NotBeNull();
            proxy.ConstructorValue.Should().NotBeNull();
            proxy.PropertyWithBackingField.Should().NotBeNull();
        }

        [Fact]
        public void ReverseProxyWithPropertiesTest()
        {
            var instance = new ReverseProxyWithPropertiesTestClass();
            var proxy = (IReverseProxyWithPropertiesTest)instance.DuckImplement(typeof(IReverseProxyWithPropertiesTest));

            proxy.Value.Should().Be(instance.Value);
        }

        [Fact]
        public void ReverseProxyWithDuckChainingPropertiesTest()
        {
            var instance = new ReverseProxyWithDuckChainingPropertiesTestClass();
            var proxy = (IReverseProxyWithDuckChainingPropertiesTest)instance.DuckImplement(typeof(IReverseProxyWithDuckChainingPropertiesTest));

            var testValue = new TestValue();

            proxy.Value = testValue;

            instance.Value.Should()
                    .BeAssignableTo<IDuckType>()
                    .Which.Instance.Should()
                    .Be(testValue);
        }

        // ************************************************************************************
        // Types for InterfaceReverseProxyTest
        // ***

        public class InternalLogEventEnricherImpl
        {
            private ManualResetEventSlim _manualResetEventSlim;

            public InternalLogEventEnricherImpl(ManualResetEventSlim manualResetEventSlim)
            {
                _manualResetEventSlim = manualResetEventSlim;
            }

            [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.Vendors.Serilog.Events.LogEvent, Datadog.Trace", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory, Datadog.Trace" })]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                Assert.NotNull(logEvent);
                Assert.NotNull(propertyFactory);

                Assert.Equal(LogEventLevel.Information, logEvent.Level);
                Assert.NotEqual(DateTimeOffset.MinValue, logEvent.Timestamp);
                Assert.Null(logEvent.Exception);
                Assert.Equal("Hello world", logEvent.MessageTemplate.Text);

                _manualResetEventSlim.Set();
            }
        }

        // ************************************************************************************
        // Types for PublicInterfaceReverseProxyTest
        // ***

        public class PublicLogEventEnricherImpl
        {
            private ManualResetEventSlim _manualResetEventSlim;

            public PublicLogEventEnricherImpl(ManualResetEventSlim manualResetEventSlim)
            {
                _manualResetEventSlim = manualResetEventSlim;
            }

            [DuckReverseMethod(ParameterTypeNames = new[] { "Serilog.Events.LogEvent, Serilog", "Serilog.Core.ILogEventPropertyFactory, Serilog" })]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                Assert.NotNull(logEvent);
                Assert.NotNull(propertyFactory);

                Assert.Equal(LogEventLevel.Information, logEvent.Level);
                Assert.NotEqual(DateTimeOffset.MinValue, logEvent.Timestamp);
                Assert.Null(logEvent.Exception);
                Assert.Equal("Hello world", logEvent.MessageTemplate.Text);

                _manualResetEventSlim.Set();
            }
        }

        // ************************************************************************************
        // Types for InternalClassWithVirtualMembersReverseProxyTest
        // ***

        public class JsonValueFormatterImpl
        {
            private readonly string _valueToWrite;

            public JsonValueFormatterImpl(string valueToWrite)
            {
                _valueToWrite = valueToWrite;
            }

            [DuckReverseMethod(ParameterTypeNames = new[] { "System.IO.TextWriter", "Datadog.Trace.Vendors.Serilog.Events.ScalarValue, Datadog.Trace" })]
            protected bool VisitScalarValue(TextWriter state, object scalar)
            {
                state.Write(_valueToWrite);
                return true;
            }
        }

        // ************************************************************************************
        // Types for PublicClassWithVirtualMembersReverseProxyTest
        // ***

        public abstract class PublicAbstractClassWithProperties
        {
            protected object SyncRoot { get; } = new object();

            public string GetClassName() => GetName();

            public object GetSyncRoot() => SyncRoot;

            protected virtual string GetName()
            {
                return nameof(PublicAbstractClassWithProperties);
            }
        }

        public class PublicClassWithVirtualMembers
        {
            [DuckReverseMethod]
            protected virtual string GetName()
            {
                return nameof(PublicClassWithVirtualMembers);
            }
        }

        /* Types for ReverseProxyInvokesBaseConstructorTest */

        public abstract class AbstractBaseWithConstructorClass
        {
            private static readonly int? _privateStaticReadonly = 1;
            private readonly int? _privateReadonly = 2;
            private int? _private = 3;
            private int? _constructorValue;

            protected AbstractBaseWithConstructorClass()
            {
                _constructorValue = 4;
            }

            public int? PrivateStaticReadonly => _privateStaticReadonly;

            public int? PrivateReadonly => _privateReadonly;

            public int? Private => _private;

            public int? ConstructorValue => _constructorValue;

            public int? PropertyWithBackingField => 5;
        }

        public class AbstractBaseWithConstructorClassDuck
        {
        }

        /* Types for ReverseProxyWithPropertiesTestClass */

        public interface IReverseProxyWithPropertiesTest
        {
            string Value { get; set; }
        }

        public class ReverseProxyWithPropertiesTestClass
        {
            [DuckReverseMethod]
            public string Value { get; set; } = "Datadog";
        }

        /* Types for ReverseProxyWithDuckChainingPropertiesTestClass */

        public interface IReverseProxyWithDuckChainingPropertiesTest
        {
            TestValue Value { get; set; }
        }

        public class ReverseProxyWithDuckChainingPropertiesTestClass
        {
            [DuckReverseMethod]
            public ITestValue Value { get; set; }
        }

        public class TestValue
        {
            public string InnerValue { get; set; }
        }

        public interface ITestValue
        {
            public string InnerValue { get; set; }
        }

        // ************************************************************************************
        // Common Types
        // ***

        public interface ILogEvent
        {
            public DateTimeOffset Timestamp { get; }

            public LogEventLevel Level { get; }

            public IMessageTemplate MessageTemplate { get; }

            public Exception Exception { get; }

            void AddPropertyIfAbsent(ILogEventProperty property);
        }

        public enum LogEventLevel
        {
            Verbose,
            Debug,
            Information,
            Warning,
            Error,
            Fatal
        }

        public interface IMessageTemplate
        {
            public string Text { get; }
        }

        public interface ILogEventPropertyFactory
        {
            ILogEventProperty CreateProperty(string name, object value, bool destructureObjects = false);
        }

        public interface ILogEventProperty
        {
            public string Name { get; }

            public object Value { get; }
        }

        public class LogEventPropertyValueImpl
        {
            private IBaseClass _base;
            private ManualResetEventSlim _manualResetEventSlim;

            public LogEventPropertyValueImpl(ManualResetEventSlim manualResetEventSlim)
            {
                _manualResetEventSlim = manualResetEventSlim;
            }

            [DuckIgnore]
            public void SetBaseInstance(object baseObject)
            {
                _base = baseObject.DuckCast<IBaseClass>();
            }

            [DuckReverseMethod]
            public void Render(TextWriter output, string format = null, IFormatProvider formatProvider = null)
            {
                output.WriteLine(format);

                Assert.NotNull(output);
                Assert.Equal("Hello world", format);

                _manualResetEventSlim.Set();
            }

            public interface IBaseClass
            {
                public string ToString();

                public string ToString(string format, IFormatProvider formatProvider);
            }
        }

        // ************************************************************************************
    }
}
