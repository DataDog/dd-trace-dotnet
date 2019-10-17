using System;
using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Moq;
using Serilog.Formatting.Display;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    internal static class LoggingProviderTestHelpers
    {
        internal static readonly string CustomPropertyName = "custom";
        internal static readonly int CustomPropertyValue = 1;
        internal static readonly string LogPrefix = "[Datadog.Trace.Tests.Logging]";

        private const string Log4NetExpectedStringFormat = "\"{0}\":\"{1}\"";
        private const string SerilogExpectedStringFormat = "{0}: \"{1}\"";

        internal static Tracer InitializeTracer(bool enableLogsInjection)
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            settings.LogsInjectionEnabled = enableLogsInjection;

            return new Tracer(settings, writerMock.Object, samplerMock.Object, null);
        }

        internal static void PerformParentChildScopeSequence(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out Scope parentScope, out Scope childScope)
        {
            parentScope = tracer.StartActive("parent");
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Started and activated parent scope.");

            var customPropertyContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false);
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Added custom property to MDC");

            childScope = tracer.StartActive("child");
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Started and activated child scope.");

            childScope.Close();
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope and reactivated parent scope.");

            customPropertyContext.Dispose();
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Removed custom property from MDC");

            parentScope.Close();
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope so there is no active scope.");
        }

        internal static void Contains(this log4net.Core.LoggingEvent logEvent, Scope scope)
        {
            logEvent.Contains(scope.Span.TraceId, scope.Span.SpanId);
        }

        internal static void Contains(this log4net.Core.LoggingEvent logEvent, ulong traceId, ulong spanId)
        {
            // First, verify that the properties are attached to the LogEvent
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(spanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));

            // Second, verify that the message formatting correctly encloses the
            // values in quotes, since they are string values
            var layout = new Log4NetLogProviderTests.Log4NetJsonLayout();
            string formattedMessage = layout.Format(logEvent);
            Assert.Contains(string.Format(Log4NetExpectedStringFormat, CorrelationIdentifier.TraceIdKey, traceId), formattedMessage);
            Assert.Contains(string.Format(Log4NetExpectedStringFormat, CorrelationIdentifier.SpanIdKey, spanId), formattedMessage);
        }

        internal static void Contains(this Serilog.Events.LogEvent logEvent, Scope scope)
        {
            logEvent.Contains(scope.Span.TraceId, scope.Span.SpanId);
        }

        internal static void Contains(this Serilog.Events.LogEvent logEvent, ulong traceId, ulong spanId)
        {
            // First, verify that the properties are attached to the LogEvent
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString().Trim(new[] { '\"' })));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(spanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString().Trim(new[] { '\"' })));

            // Second, verify that the message formatting correctly encloses the
            // values in quotes, since they are string values

            // Use the built-in formatting to render the message like the console output would,
            // but this must write to a TextWriter so use a StringWriter/StringBuilder to shuttle
            // the message to our in-memory list
            const string OutputTemplate = "{Message}|{Properties}";
            var textFormatter = new MessageTemplateTextFormatter(OutputTemplate, CultureInfo.InvariantCulture);
            var sw = new StringWriter(new StringBuilder());
            textFormatter.Format(logEvent, sw);
            var formattedMessage = sw.ToString();

            Assert.Contains(string.Format(SerilogExpectedStringFormat, CorrelationIdentifier.TraceIdKey, traceId), formattedMessage);
            Assert.Contains(string.Format(SerilogExpectedStringFormat, CorrelationIdentifier.SpanIdKey, spanId), formattedMessage);
        }
    }
}
