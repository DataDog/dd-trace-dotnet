using System;
using System.IO;
using System.Threading;
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

            var instance = new LogEventEnricherImpl(resetEvent);
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

        // ************************************************************************************
        // Types for InterfaceReverseProxyTest
        // ***

        public class LogEventEnricherImpl
        {
            private ManualResetEventSlim _manualResetEventSlim;

            public LogEventEnricherImpl(ManualResetEventSlim manualResetEventSlim)
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

        // ************************************************************************************

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

        // ************************************************************************************
        // Types for AbstractClassReverseProxyTest
        // ***

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

        [Fact]
        public void PublicInterfaceReverseProxyTest()
        {
            var instance = new PublicBaseInterface();
            var proxyInstance = instance.DuckCast(typeof(IPublicBaseInterface));

            Assert.Equal("Hello world", ((IPublicBaseInterface)proxyInstance).SayHello("world"));
        }

        public interface IPublicBaseInterface
        {
            public string SayHello(string value);
        }

        public class PublicBaseInterface
        {
            [DuckReverseMethod("System.String")]
            public string SayHello(object value)
            {
                return "Hello " + value;
            }
        }
    }
}
