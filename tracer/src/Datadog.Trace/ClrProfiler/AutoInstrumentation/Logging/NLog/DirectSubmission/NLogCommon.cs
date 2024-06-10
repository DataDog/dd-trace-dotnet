// <copyright file="NLogCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    internal static class NLogCommon<TTarget>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NLogCommon<TTarget>));

        // ReSharper disable StaticMemberInGenericType
        // ReSharper disable InconsistentNaming
        private static readonly Type? _targetType;

        private static readonly bool _hasMappedDiagnosticsContext;
        private static readonly bool _isModernMappedDiagnosticsContext;
        private static readonly MappedDiagnosticsProxy? _mdc;
        private static readonly MappedDiagnosticsContextLegacyProxy _mdcLegacy;
        private static readonly bool _hasMappedDiagnosticsLogicalContext;
        private static readonly bool _isModernMappedDiagnosticsLogicalContext;
        private static readonly MappedDiagnosticsProxy? _mdlc;
        private static readonly MappedDiagnosticsLogicalContextLegacyProxy _mdlcLegacy;

        private static readonly object? _targetProxy;
        private static readonly Func<object>? _createLoggingRuleFunc;
        // ReSharper restore InconsistentNaming

        static NLogCommon()
        {
            try
            {
                var nlogAssembly = typeof(TTarget).Assembly;
                _targetType = nlogAssembly.GetType("NLog.Targets.TargetWithContext");
                if (_targetType?.GetProperty("IncludeScopeProperties") is not null)
                {
                    Version = NLogVersion.NLog50;
                    _targetProxy = CreateNLogTargetProxy(new DirectSubmissionNLogV5Target(
                                         TracerManager.Instance.DirectLogSubmission.Sink,
                                         TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel));
                    return;
                }

                if (_targetType is not null)
                {
                    Version = NLogVersion.NLog45;
                    _targetProxy = CreateNLogTargetProxy(new DirectSubmissionNLogTarget(
                                         TracerManager.Instance.DirectLogSubmission.Sink,
                                         TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel));
                    return;
                }

                _targetType = nlogAssembly.GetType("NLog.Targets.Target");

                // Type was added in NLog 4.3, so we can use it to safely determine the version
                var testType = nlogAssembly.GetType("NLog.Config.ExceptionRenderingFormat");
                Version = testType is null ? NLogVersion.NLogPre43 : NLogVersion.NLog43To45;

                TryGetMdcProxy(nlogAssembly, out _hasMappedDiagnosticsContext, out _isModernMappedDiagnosticsContext, out _mdc, out _mdcLegacy);
                TryGetMdlcProxy(nlogAssembly, out _hasMappedDiagnosticsLogicalContext, out _isModernMappedDiagnosticsLogicalContext, out _mdlc, out _mdlcLegacy);

                _targetProxy = CreateNLogTargetProxy(new DirectSubmissionNLogLegacyTarget(
                                                         TracerManager.Instance.DirectLogSubmission.Sink,
                                                         TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel));

                if (Version == NLogVersion.NLogPre43)
                {
                    _createLoggingRuleFunc = CreateLoggingRuleActivator(nlogAssembly);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating NLog target proxies for direct log shipping");
                _targetType = null;
                _targetProxy = null;
            }
        }

        public static NLogVersion Version { get; }

        public static bool AddDatadogTargetToLoggingConfiguration(object loggingConfiguration)
        {
            if (_targetProxy is null)
            {
                return false;
            }

            return Version switch
            {
                NLogVersion.NLog50 => AddDatadogTargetNLog50(loggingConfiguration, _targetProxy),
                NLogVersion.NLog45 => AddDatadogTargetNLog45(loggingConfiguration, _targetProxy),
                NLogVersion.NLog43To45 => AddDatadogTargetNLog43To45(loggingConfiguration, _targetProxy),
                _ => AddDatadogTargetNLogPre43(loggingConfiguration, _targetProxy)
            };
        }

        public static bool AddDatadogTargetToLoggingRulesList<TLoggingRuleList>(TLoggingRuleList loggingRules)
        {
            if (_targetProxy is null || loggingRules is not IList list)
            {
                return false;
            }

            // TODO: Move this to static helper?
            // It shouldn't be called much, so probably unnecessary...
            var loggingRuleType = typeof(TLoggingRuleList).GetGenericArguments()[0];
            var logLevelType = loggingRuleType.Assembly.GetType("NLog.LogLevel", throwOnError: false);
            if (logLevelType is null)
            {
                Log.Warning("Unable to enable direct log submission via NLog 5.0+ - could not find LogLevel type");
                return false;
            }

            var proxyResult = DuckType.GetOrCreateProxyType(typeof(LogLevelStaticsProxy), logLevelType);
            if (!proxyResult.Success)
            {
                Log.Warning("Unable to enable direct log submission via NLog 5.0+ - could not create LogLevel proxy");
                return false;
            }

            var logLevelStaticsProxy = proxyResult.CreateInstance<LogLevelStaticsProxy>(null);

            // we should already have checked that we haven't added our target
            // and we know this is a List<LoggingRule>
            var loggingRule = Activator.CreateInstance(loggingRuleType);
            if (loggingRule is null || loggingRule.DuckCast<Proxies.ILoggingRuleProxy>() is not { } proxy)
            {
                Log.Warning("Unable to enable direct log submission via NLog 5.0+ - new LoggingRule() was null");
                return false;
            }

            proxy.Targets.Add(_targetProxy);
            proxy.LoggerNamePattern = "**";
            proxy.EnableLoggingForLevels(logLevelStaticsProxy.MinLevel, logLevelStaticsProxy.MaxLevel);
            proxy.Final = true;

            list.Add(loggingRule);
            Log.Information("Direct log submission via NLog 5.0+ enabled");
            return true;
        }

        public static IDictionary<string, object?>? GetContextProperties()
        {
            IDictionary<string, object?>? properties = null;
            if (_hasMappedDiagnosticsContext)
            {
                if (_isModernMappedDiagnosticsContext)
                {
                    var names = _mdc!.GetNames(); // C# isn't clever enough to figure that this is never null here
                    properties = new Dictionary<string, object?>(names.Count);
                    foreach (var name in names)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            // could be a <string, string> or a <string, object>, depending on NLog version
                            properties[name] = _mdc.GetObject(name);
                        }
                    }
                }
                else if (_mdcLegacy.ThreadDictionary is { } dict)
                {
                    properties = new Dictionary<string, object?>(dict.Count);
                    foreach (string? name in dict.Keys)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            // could be a <string, string> or a <string, object>, depending on NLog version
                            properties[name!] = dict[name];
                        }
                    }
                }
            }

            if (_hasMappedDiagnosticsLogicalContext)
            {
                if (_isModernMappedDiagnosticsLogicalContext)
                {
                    var names = _mdlc!.GetNames(); // C# isn't clever enough to figure that this is never null here
                    properties ??= new Dictionary<string, object?>(names.Count);
                    foreach (var name in names)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            // could be a <string, string> or a <string, object>, depending on NLog version
                            properties[name] = _mdlc.GetObject(name);
                        }
                    }
                }
                else if (_mdlcLegacy.LogicalThreadDictionary is { } dict)
                {
                    properties ??= new Dictionary<string, object?>(dict.Count);
                    foreach (string? name in dict.Keys)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            // could be a <string, string> or a <string, object>, depending on NLog version
                            properties[name!] = dict[name];
                        }
                    }
                }
            }

            return properties;
        }

        // internal for testing
        internal static bool AddDatadogTargetNLog50(object loggingConfiguration, object targetProxy)
        {
            // Could also do the duck cast in the method signature, but this avoids the allocation in the instrumentation
            // if not enabled.
            var loggingConfigurationProxy = loggingConfiguration.DuckCast<ILoggingConfigurationProxy>();
            if (loggingConfigurationProxy.ConfiguredNamedTargets is not null)
            {
                foreach (var target in loggingConfigurationProxy.ConfiguredNamedTargets)
                {
                    if (target is IDuckType { Instance: DirectSubmissionNLogV5Target })
                    {
                        // already added
                        return false;
                    }
                }
            }

            // need to add the new target to the logging configuraiton
            loggingConfigurationProxy.AddTarget(NLogConstants.DatadogTargetName, targetProxy);
            loggingConfigurationProxy.AddRuleForAllLevels(targetProxy, "**", final: true);

            Log.Information("Direct log submission via NLog 5.0+ enabled");
            return true;
        }

        // internal for testing
        internal static bool AddDatadogTargetNLog45(object loggingConfiguration, object targetProxy)
        {
            // Could also do the duck cast in the method signature, but this avoids the allocation in the instrumentation
            // if not enabled.
            var loggingConfigurationProxy = loggingConfiguration.DuckCast<ILoggingConfigurationProxy>();
            if (loggingConfigurationProxy.ConfiguredNamedTargets is not null)
            {
                foreach (var target in loggingConfigurationProxy.ConfiguredNamedTargets)
                {
                    if (target is IDuckType { Instance: DirectSubmissionNLogTarget })
                    {
                        // already added
                        return false;
                    }
                }
            }

            // need to add the new target to the logging configuraiton
            loggingConfigurationProxy.AddTarget(NLogConstants.DatadogTargetName, targetProxy);
            loggingConfigurationProxy.AddRuleForAllLevels(targetProxy, "**", final: true);

            Log.Information("Direct log submission via NLog 4.5+ enabled");
            return true;
        }

        // internal for testing
        internal static bool AddDatadogTargetNLog43To45(object loggingConfiguration, object targetProxy)
        {
            // Could also do the duck cast in the method signature, but this avoids the allocation in the instrumentation
            // if not enabled.
            var loggingConfigurationProxy = loggingConfiguration.DuckCast<ILoggingConfigurationLegacyProxy>();
            if (loggingConfigurationProxy.ConfiguredNamedTargets is not null)
            {
                foreach (var target in loggingConfigurationProxy.ConfiguredNamedTargets)
                {
                    if (target is IDuckType { Instance: DirectSubmissionNLogLegacyTarget })
                    {
                        // already added
                        return false;
                    }
                }
            }

            // need to add the new target to the logging configuraiton
            loggingConfigurationProxy.AddTarget(NLogConstants.DatadogTargetName, targetProxy);
            loggingConfigurationProxy.AddRuleForAllLevels(targetProxy, "**");

            Log.Information("Direct log submission via NLog 4.3-4.5 enabled");
            return true;
        }

        // internal for testing
        internal static bool AddDatadogTargetNLogPre43(object loggingConfiguration, object targetProxy)
        {
            var loggingConfigurationProxy = loggingConfiguration.DuckCast<ILoggingConfigurationPre43Proxy>();

            if (loggingConfigurationProxy.ConfiguredNamedTargets is not null)
            {
                foreach (var target in loggingConfigurationProxy.ConfiguredNamedTargets)
                {
                    if (target is IDuckType { Instance: DirectSubmissionNLogLegacyTarget })
                    {
                        // already added
                        return false;
                    }
                }
            }

            if (_createLoggingRuleFunc is null)
            {
                // we failed on startup, so should never get to this point
                return false;
            }

            // need to create and add the new target
            loggingConfigurationProxy.AddTarget(NLogConstants.DatadogTargetName, targetProxy);

            // Have to create a logging rule the hard way
            var instance = _createLoggingRuleFunc();
            var ruleProxy = instance.DuckCast<Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43.ILoggingRuleProxy>();

            ruleProxy.LoggerNamePattern = "**";
            ruleProxy.Targets.Add(targetProxy);
            for (var i = 0; i < 6; i++)
            {
                ruleProxy.LogLevels[i] = true;
            }

            ruleProxy.Final = true;
            loggingConfigurationProxy.LoggingRules.Add(instance);

            Log.Information("Direct log submission via NLog <4.3 enabled");
            return true;
        }

        // internal for testing
        internal static object CreateNLogTargetProxy(DirectSubmissionNLogV5Target target)
        {
            if (_targetType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"{nameof(_targetType)} is null");
            }

            // create a new instance of DirectSubmissionNLogTarget
            var reverseProxy = target.DuckImplement(_targetType);
            var targetProxy = reverseProxy.DuckCast<ITargetWithContextV5BaseProxy>();
            target.SetBaseProxy(targetProxy);
            // theoretically this should be called per logging configuration
            // but we don't need to so hack in the call here
            targetProxy.Initialize(null);
            return reverseProxy;
        }

        // internal for testing
        internal static object CreateNLogTargetProxy(DirectSubmissionNLogTarget target)
        {
            if (_targetType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"{nameof(_targetType)} is null");
            }

            // create a new instance of DirectSubmissionNLogTarget
            var reverseProxy = target.DuckImplement(_targetType);
            var targetProxy = reverseProxy.DuckCast<ITargetWithContextBaseProxy>();
            target.SetBaseProxy(targetProxy);
            // theoretically this should be called per logging configuration
            // but we don't need to so hack in the call here
            targetProxy.Initialize(null);
            return reverseProxy;
        }

        // internal for testing
        internal static object CreateNLogTargetProxy(DirectSubmissionNLogLegacyTarget target)
        {
            if (_targetType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"{nameof(_targetType)} is null");
            }

            var reverseProxy = target.DuckImplement(_targetType);
            if (_hasMappedDiagnosticsContext || _hasMappedDiagnosticsLogicalContext)
            {
                target.SetGetContextPropertiesFunc(() => GetContextProperties());
            }

            var targetProxy = reverseProxy.DuckCast<ITargetProxy>();
            // theoretically this should be called per logging configuration
            // but we don't need to so hack in the call here
            targetProxy.Initialize(null);

            return reverseProxy;
        }

        // internal for testing
        internal static void TryGetMdcProxy(
            Assembly nlogAssembly,
            out bool haveMdcProxy,
            out bool isModernMdcProxy,
            out MappedDiagnosticsProxy? mdc,
            out MappedDiagnosticsContextLegacyProxy mdcLegacy)
        {
            var mdcType = nlogAssembly.GetType("NLog.MappedDiagnosticsContext");
            if (mdcType is not null)
            {
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsProxy), mdcType);
                if (createTypeResult.Success)
                {
                    mdc = createTypeResult.CreateInstance<MappedDiagnosticsProxy>(instance: null);
                    mdcLegacy = default;
                    haveMdcProxy = true;
                    isModernMdcProxy = true;
                    return;
                }

                var createLegacyTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsContextLegacyProxy), mdcType);
                if (createLegacyTypeResult.Success)
                {
                    mdcLegacy = createLegacyTypeResult.CreateInstance<MappedDiagnosticsContextLegacyProxy>(instance: null);
                    mdc = default;
                    haveMdcProxy = true;
                    isModernMdcProxy = false;
                    return;
                }
            }

            haveMdcProxy = false;
            isModernMdcProxy = false;
            mdcLegacy = default;
            mdc = default;
        }

        // internal for testing
        internal static void TryGetMdlcProxy(
            Assembly nlogAssembly,
            out bool haveMdlcProxy,
            out bool isModernMdlcProxy,
            out MappedDiagnosticsProxy? mdlc,
            out MappedDiagnosticsLogicalContextLegacyProxy mdlcLegacy)
        {
            var mdclType = nlogAssembly.GetType("NLog.MappedDiagnosticsLogicalContext");
            if (mdclType is not null)
            {
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsProxy), mdclType);
                if (createTypeResult.Success)
                {
                    mdlc = createTypeResult.CreateInstance<MappedDiagnosticsProxy>(instance: null);
                    mdlcLegacy = default;
                    haveMdlcProxy = true;
                    isModernMdlcProxy = true;
                    return;
                }

                var createLegacyTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsLogicalContextLegacyProxy), mdclType);
                if (createLegacyTypeResult.Success)
                {
                    mdlcLegacy = createLegacyTypeResult.CreateInstance<MappedDiagnosticsLogicalContextLegacyProxy>(instance: null);
                    mdlc = null;
                    haveMdlcProxy = true;
                    isModernMdlcProxy = false;
                    return;
                }
            }

            mdlc = null;
            mdlcLegacy = default;
            haveMdlcProxy = false;
            isModernMdlcProxy = false;
        }

        // internal for testing
        internal static Func<object> CreateLoggingRuleActivator(Assembly nlogAssembly)
        {
            var activator = new ActivatorHelper(nlogAssembly.GetType("NLog.Config.LoggingRule")!);
            return () => activator.CreateInstance()!;
        }
    }
}
