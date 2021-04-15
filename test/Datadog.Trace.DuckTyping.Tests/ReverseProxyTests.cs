using System;
using System.IO;
using System.Threading;
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
#if NET452
            Assert.Throws<DuckTypeTypeIsNotPublicException>(() =>
            {
                instance.DuckCast(iLogEventEnricherType);
            });
#else
            var proxy = instance.DuckCast(iLogEventEnricherType);

            var log = new Vendors.Serilog.LoggerConfiguration()
                .Enrich.With((Vendors.Serilog.Core.ILogEventEnricher)proxy)
                .MinimumLevel.Debug()
                .WriteTo.Sink(new Vendors.Serilog.Sinks.File.NullSink())
                .CreateLogger();

            log.Information("Hello world");

            Assert.True(resetEvent.Wait(5_000));
#endif
        }

        [Fact]
        public void PrivateAbstractClassReverseProxyTest()
        {
            var resetEvent = new ManualResetEventSlim();

            var eventInstance = new LogEventPropertyValueImpl(resetEvent);

            var type = typeof(Datadog.Trace.Vendors.Serilog.Events.LogEventPropertyValue);
#if NET452
            Assert.Throws<DuckTypeTypeIsNotPublicException>(() =>
            {
                eventInstance.DuckCast(type);
            });
#else
            var proxy2 = eventInstance.DuckCast(type);
            eventInstance.SetBaseInstance(proxy2);

            ((Datadog.Trace.Vendors.Serilog.Events.LogEventPropertyValue)proxy2).ToString("Hello world", null);

            Assert.True(resetEvent.Wait(5_000));
#endif
        }

        [Fact]
        public void PublicInterfaceReverseProxyTest()
        {
            Type iLogEventEnricherType = typeof(Serilog.Core.ILogEventEnricher);

            var resetEvent = new ManualResetEventSlim();

            var instance = new PublicLogEventEnricherImpl(resetEvent);

            var proxy = instance.DuckCast(iLogEventEnricherType);
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

            var proxy2 = eventInstance.DuckCast(type);
            eventInstance.SetBaseInstance(proxy2);

            ((Serilog.Events.LogEventPropertyValue)proxy2).ToString("Hello world", null);

            Assert.True(resetEvent.Wait(5_000));
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

            [DuckReverseMethod("Datadog.Trace.Vendors.Serilog.Events.LogEvent", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory")]
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

            [DuckReverseMethod("Serilog.Events.LogEvent", "Serilog.Core.ILogEventPropertyFactory")]
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
        // Common Types
        // ***

        public interface ILogEvent
        {
            public DateTimeOffset Timestamp { get; }

            public LogEventLevel Level { get; }

            public IMessageTemplate MessageTemplate { get; }

            public Exception Exception { get; }
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
