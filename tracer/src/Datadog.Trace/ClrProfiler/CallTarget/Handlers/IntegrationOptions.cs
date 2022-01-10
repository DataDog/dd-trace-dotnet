// <copyright file="IntegrationOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class IntegrationOptions<TIntegration, TTarget>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntegrationOptions<TIntegration, TTarget>));

        private static readonly Lazy<IntegrationId?> _integrationId = new(() => GetIntegrationId(typeof(TIntegration), typeof(TTarget)));
        private static volatile bool _disableIntegration = false;

        internal static bool IsIntegrationEnabled => !_disableIntegration;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DisableIntegration() => _disableIntegration = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogException(Exception exception, string message = null)
        {
            // ReSharper disable twice ExplicitCallerInfoArgument
            Log.Error(exception, message ?? exception?.Message);
            if (exception is DuckTypeException or TargetInvocationException { InnerException: DuckTypeException })
            {
                Log.Warning($"DuckTypeException has been detected, the integration <{typeof(TIntegration)}, {typeof(TTarget)}> will be disabled.");
                if (_integrationId.Value is not null)
                {
                    Tracer.Instance.TracerManager.Telemetry.IntegrationDisabledDueToError(_integrationId.Value.Value, nameof(DuckTypeException));
                }

                _disableIntegration = true;
            }
            else if (exception is CallTargetInvokerException)
            {
                Log.Warning($"CallTargetInvokerException has been detected, the integration <{typeof(TIntegration)}, {typeof(TTarget)}> will be disabled.");
                if (_integrationId.Value is not null)
                {
                    Tracer.Instance.TracerManager.Telemetry.IntegrationDisabledDueToError(_integrationId.Value.Value, nameof(CallTargetInvokerException));
                }

                _disableIntegration = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RecordTelemetry()
        {
            if (_integrationId.Value is not null)
            {
                Tracer.Instance.TracerManager.Telemetry.IntegrationRunning(_integrationId.Value.Value);
            }
        }

        // internal for testing
        internal static IntegrationId? GetIntegrationId(Type integrationType, Type targetType)
        {
            try
            {
                var attributes = integrationType.GetCustomAttributes(typeof(InstrumentMethodAttribute), inherit: true);
                if (attributes.Length > 0)
                {
                    var name = ((InstrumentMethodAttribute)attributes[0]).IntegrationName;
                    if (string.IsNullOrEmpty(name))
                    {
                        // shouldn't happen?
                        return null;
                    }

                    return IntegrationRegistry.TryGetIntegrationId(name, out var integrationId) ? integrationId : null;
                }

                // probably assembly attribute
                var assemblyAttributes = integrationType.Assembly.GetCustomAttributes(typeof(InstrumentMethodAttribute), inherit: true);
                if (assemblyAttributes.Length == 0)
                {
                    // can't work out which integration this is, shouldn't happen?
                    return null;
                }

                var targetTypeName = targetType.FullName;
                var targetAssemblyName = targetType.Assembly.GetName().Name;

                foreach (InstrumentMethodAttribute assemblyAttribute in assemblyAttributes)
                {
                    if (assemblyAttribute.TypeName == targetTypeName)
                    {
                        if (assemblyAttribute.AssemblyNames is not null)
                        {
                            foreach (var assemblyName in assemblyAttribute.AssemblyNames)
                            {
                                if (assemblyName == targetAssemblyName && assemblyAttribute.CallTargetType.FullName == integrationType.FullName)
                                {
                                    return IntegrationRegistry.TryGetIntegrationId(assemblyAttribute.IntegrationName, out var integrationId) ? integrationId : null;
                                }
                            }
                        }

                        if (assemblyAttribute.AssemblyName is not null
                         && assemblyAttribute.AssemblyName == targetAssemblyName
                         && assemblyAttribute.CallTargetType.FullName == integrationType.FullName)
                        {
                            return IntegrationRegistry.TryGetIntegrationId(assemblyAttribute.IntegrationName, out var integrationId) ? integrationId : null;
                        }
                    }
                }

                // not found
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error determining associated integration name for CallTarget error");
                return null;
            }
        }
    }
}
