// <copyright file="NLogDuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP
using System;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using FluentAssertions;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using Xunit;
using LogEventInfo = NLog.LogEventInfo;
using LogLevel = NLog.LogLevel;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.NLog
{
    public class NLogDuckTypingTests
    {
#if (NLOG_45 || NLOG_50)
        [Fact]
        public void CanDuckTypeLoggingConfigurationInModernNlog()
        {
            var instance = new LoggingConfiguration();
            instance.DuckCast<ILoggingConfigurationProxy>();
            instance.TryDuckCast(out ILoggingConfigurationProxy duck).Should().BeTrue();
            duck.Should().NotBeNull();
            duck.ConfiguredNamedTargets.Cast<object>().Should().BeEmpty();
        }

        [Fact]
        public void CanDuckTypeLogInfoInModernNLog()
        {
            var instance = LogEventInfo.Create(
                logLevel: LogLevel.Fatal,
                loggerName: "LoggerName",
                message: "Some message {Value}",
                exception: new InvalidOperationException(),
                formatProvider: null,
                parameters: new object[] { 123 });

            var duck = instance.DuckCast<ILogEventInfoProxy>();
            duck.Should().NotBeNull();
            duck.Level.Should().NotBeNull();
            duck.Level.Ordinal.Should().Be(LogLevel.Fatal.Ordinal);
            duck.FormattedMessage.Should().Be(instance.FormattedMessage);
            duck.Exception.Should().Be(instance.Exception);
            duck.HasProperties.Should().BeTrue();

            var instanceWithoutProperties = LogEventInfo.Create(
                logLevel: LogLevel.Fatal,
                loggerName: "LoggerName",
                message: "Some message");

            instanceWithoutProperties.HasProperties.Should().BeFalse();
            var duckWithoutProperties = instanceWithoutProperties.DuckCast<ILogEventInfoProxy>();

            duckWithoutProperties.HasProperties.Should().BeFalse();

            instanceWithoutProperties.Properties["TestKey"] = "TestValue";
            duckWithoutProperties.HasProperties.Should().BeTrue();
            duckWithoutProperties.Properties.Should().ContainKey("TestKey").WhoseValue.Should().Be("TestValue");
        }
#elif NLOG_43
        [Fact]
        public void CanDuckTypeLoggingConfigurationLegacyInLegacyNlog()
        {
            var instance = new LoggingConfiguration();
            instance.DuckCast<LoggingConfigurationLegacyProxy>();
            instance.TryDuckCast(out LoggingConfigurationLegacyProxy duck).Should().BeTrue();
            duck.Should().NotBeNull();
            duck.ConfiguredNamedTargets.Should().BeEmpty();
        }
#elif NLOG_2 || NLOG_30
        [Fact]
        public void CanDuckTypeLoggingConfigurationLegacyPre43InAncientNlog()
        {
            var instance = new LoggingConfiguration();
            instance.LoggingRules.Should().BeEmpty();
            instance.DuckCast<LoggingConfigurationPre43Proxy>();
            instance.TryDuckCast(out LoggingConfigurationPre43Proxy duck).Should().BeTrue();
            duck.Should().NotBeNull();
            duck.ConfiguredNamedTargets.Should().BeEmpty();
            var rule = new LoggingRule();
            duck.LoggingRules.Add(rule);
            instance.LoggingRules.Should().ContainSingle().Which.Should().Be(rule);
        }

        [Fact]
        public void CanDuckTypeLoggingRuleInPre43()
        {
            var rule = new LoggingRule();
            var proxy = rule.DuckCast<LoggingRuleProxy>();

            proxy.LoggerNamePattern = "TEST";
            for (int i = 0; i < 6; i++)
            {
                proxy.LogLevels[i] = true;
            }

            rule.LoggerNamePattern.Should().Be("TEST");
            rule.Levels
               ?.Select(x => x.Ordinal)
                .Should()
                .NotBeNull()
                .And.ContainInOrder(0, 1, 2, 3, 4, 5);
        }
#endif

        [Fact]
        public void CanReverseDuckTypeTarget()
        {
            var targetType = typeof(Target);
            var target = NLogHelper.CreateTarget(new NullDirectSubmissionLogSink(), DirectSubmissionLogLevel.Debug);
            var proxy = NLogCommon<Target>.CreateNLogTargetProxy(target);

            proxy.Should().NotBeNull();
            proxy.GetType().Should().BeDerivedFrom(targetType);

            var message = "the message";
            var logInfo = new AsyncLogEventInfo(LogEventInfo.Create(LogLevel.Error, "test", message), _ => { });
            var typedProxy = ((Target)proxy);
            typedProxy.WriteAsyncLogEvent(logInfo); // should not throw

#if (NLOG_50)
            var proxyOfProxy = proxy.DuckCast<ITargetWithContextV5BaseProxy>();
            proxyOfProxy.Should().NotBeNull();

            var results = proxyOfProxy.GetAllProperties(logInfo.LogEvent.DuckCast<ILogEventInfoProxy>());
            results.Should().NotBeNull();

            target.SetBaseProxy(proxyOfProxy);
#elif (NLOG_45)
            var proxyOfProxy = proxy.DuckCast<ITargetWithContextBaseProxy>();
            proxyOfProxy.Should().NotBeNull();

            var results = proxyOfProxy.GetAllProperties(logInfo.LogEvent.DuckCast<ILogEventInfoProxy>());
            results.Should().NotBeNull();

            target.SetBaseProxy(proxyOfProxy);
#endif
        }

#if (!NLOG_45 && !NLOG_50)
        [Fact]
        public void CanDuckTypeLogInfoInLegacyNLog()
        {
#if NLOG_2
            var instance = new LogEventInfo(
                level: LogLevel.Fatal,
                loggerName: "LoggerName",
                message: "Some message {0}",
                exception: new InvalidOperationException(),
                formatProvider: null,
                parameters: new object[] { 123 });
#else
            var instance = LogEventInfo.Create(
                logLevel: LogLevel.Fatal,
                loggerName: "LoggerName",
                message: "Some message {0}",
                exception: new InvalidOperationException(),
                formatProvider: null,
                parameters: new object[] { 123 });
#endif
            var duck = instance.DuckCast<LogEventInfoLegacyProxy>();
            duck.Should().NotBeNull();
            duck.Level.Should().NotBeNull();
            duck.Level.Ordinal.Should().Be(LogLevel.Fatal.Ordinal);
            duck.LoggerName.Should().Be(instance.LoggerName);
            duck.FormattedMessage.Should().Be(instance.FormattedMessage);
            duck.Exception.Should().Be(instance.Exception);
            duck.HasProperties.Should().BeFalse();

            instance.Properties["TestKey"] = "TestValue";
            duck.HasProperties.Should().BeTrue();
            duck.Properties.Should().ContainKey("TestKey").WhichValue.Should().Be("TestValue");
        }

        [Fact]
        public void CanDuckTypeMdc()
        {
            var assembly = typeof(Target).Assembly;
            NLogCommon<Target>.TryGetMdcProxy(
                assembly,
                out var haveProxy,
                out var isModernMdcProxy,
                out var mdc,
                out var mdcLegacy);
            haveProxy.Should().BeTrue();
            if (isModernMdcProxy)
            {
                mdc.Should().NotBeNull();
                mdc.GetNames().Should().BeEmpty();
            }
            else
            {
                mdcLegacy.ThreadDictionary.Should().BeEmpty();
            }

            var key = "mykey";
            var value = "myvalue";
            global::NLog.MappedDiagnosticsContext.Set(key, value);
            if (isModernMdcProxy)
            {
                var containsKey = mdc.GetNames().Should().NotBeNull().And.ContainSingle().Subject;
                mdc.GetObject(containsKey)
                   .Should()
                   .NotBeNull()
                   .And.Be(value);

                global::NLog.MappedDiagnosticsContext.Remove(key);
                mdc.GetNames().Should().BeEmpty();
            }
            else
            {
                mdcLegacy.ThreadDictionary.Keys.Cast<string>().Should().ContainSingle(key);
                mdcLegacy.ThreadDictionary[key].Should().Be(value);

                global::NLog.MappedDiagnosticsContext.Remove(key);
                mdcLegacy.ThreadDictionary.Should().BeEmpty();
            }
        }

#if !NLOG_2
        [Fact]
        public void CanDuckTypeMdlc()
        {
            var assembly = typeof(Target).Assembly;
            NLogCommon<Target>.TryGetMdlcProxy(
                assembly,
                out var haveProxy,
                out var isModernMdlcProxy,
                out var mdlc,
                out var mdlcLegacy);
            haveProxy.Should().BeTrue();

            if (isModernMdlcProxy)
            {
                mdlc.Should().NotBeNull();
                mdlc.GetNames().Should().BeEmpty();
            }
            else
            {
                mdlcLegacy.LogicalThreadDictionary.Should().BeEmpty();
            }

            var key = "mykey";
            var value = "myvalue";
            global::NLog.MappedDiagnosticsLogicalContext.Set(key, value);
            if (isModernMdlcProxy)
            {
                var containsKey = mdlc.GetNames().Should().NotBeNull().And.ContainSingle().Subject;
                mdlc.GetObject(containsKey)
                    .Should()
                    .NotBeNull()
                    .And.Be(value);

                global::NLog.MappedDiagnosticsLogicalContext.Remove(key);
                mdlc.GetNames().Should().BeEmpty();
            }
            else
            {
                mdlcLegacy.LogicalThreadDictionary.Keys.Cast<string>().Should().ContainSingle(key);
                mdlcLegacy.LogicalThreadDictionary[key].Should().Be(value);

                global::NLog.MappedDiagnosticsLogicalContext.Remove(key);
                mdlcLegacy.LogicalThreadDictionary.Should().BeEmpty();
            }
        }
#endif
#endif
    }
}
#endif
