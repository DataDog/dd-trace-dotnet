// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// SA1649: File name should match first type name
// Justification: This file contains both ContextPropagationExtractAdapter and ContextPropagationInjectAdapter.
// Both types share the ContextPropagation prefix matching the file name.
#pragma warning disable SA1649

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Adapter for EXTRACTING (reading) trace context headers from incoming MassTransit messages (consumer side).
    /// Scenario: When a consumer receives a message, extract distributed tracing headers to continue the trace.
    /// Uses duck typing to read headers via IHeaders.GetAll() which returns IEnumerable[HeaderValue].
    /// Used by: MassTransitCommon.ExtractTraceContext() for distributed tracing propagation.
    /// </summary>
    internal readonly struct ContextPropagationExtractAdapter : IHeadersCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagationExtractAdapter));

        private readonly IHeaders? _headersProxy;

        public ContextPropagationExtractAdapter(object? headers)
        {
            _headersProxy = headers?.DuckCast<IHeaders>();
        }

        public IEnumerable<string> GetValues(string name)
        {
            string? result = null;

            if (_headersProxy != null)
            {
                try
                {
                    var allHeaders = _headersProxy.GetAll();
                    if (allHeaders != null)
                    {
                        foreach (var headerValue in allHeaders)
                        {
                            if (headerValue == null)
                            {
                                continue;
                            }

                            // Duck cast HeaderValue struct to get Key and Value
                            var hv = headerValue.DuckCast<IHeaderValue>();
                            if (string.Equals(hv?.Key, name, StringComparison.OrdinalIgnoreCase))
                            {
                                result = hv?.Value?.ToString();
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ContextPropagationExtractAdapter.GetValues: Error reading headers for '{Name}'", name);
                }
            }

            if (result != null)
            {
                yield return result;
            }
        }

        public void Set(string name, string value)
        {
            // NOT USED - This adapter only EXTRACTS (reads) headers via GetValues(), never INJECTS (writes) them.
            // Incoming message headers are read-only.
        }

        public void Add(string name, string value)
        {
            // NOT USED - Incoming message headers are read-only.
        }

        public void Remove(string name)
        {
            // NOT USED - Incoming message headers are read-only.
        }
    }

    /// <summary>
    /// Adapter for INJECTING (writing) trace context headers into outgoing MassTransit messages (producer side).
    /// Scenario: When a producer sends a message, inject distributed tracing headers to propagate the trace.
    /// Uses reflection to invoke SendHeaders.Set() methods because MassTransit uses explicit interface implementation.
    /// Duck typing cannot be used here - see WHY_DUCK_TYPING_FAILED.md for details.
    /// Used by: MassTransitCommon.InjectTraceContext() for distributed tracing propagation.
    /// </summary>
    internal readonly struct ContextPropagationInjectAdapter : IHeadersCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagationInjectAdapter));

        private readonly object? _headers;
        private readonly MethodInfo? _setStringMethod;
        private readonly MethodInfo? _setObjectMethod;

        public ContextPropagationInjectAdapter(object? headers)
        {
            _headers = headers;
            _setStringMethod = null;
            _setObjectMethod = null;

            if (headers != null)
            {
                var headersType = headers.GetType();

                // MassTransit's DictionarySendHeaders implements the SendHeaders interface
                // The Set methods may be implemented via the interface, so we need to search
                // both the concrete type and its interfaces

                // Try Set(string, string) first - most efficient
                _setStringMethod = FindSetMethod(headersType, typeof(string), typeof(string));

                // Fall back to Set(string, object) or Set(string, object, bool)
                if (_setStringMethod == null)
                {
                    _setObjectMethod = FindSetMethod(headersType, typeof(string), typeof(object));
                }

                if (_setStringMethod == null && _setObjectMethod == null)
                {
                    Log.Debug(
                        "SendContextHeadersAdapter: Could not find suitable Set method on {HeadersType}",
                        headersType.FullName);
                }
                else
                {
                    Log.Debug(
                        "SendContextHeadersAdapter: Found Set method on {HeadersType}: StringMethod={HasString}, ObjectMethod={HasObject}",
                        headersType.FullName,
                        _setStringMethod != null,
                        _setObjectMethod != null);
                }
            }
        }

        private static MethodInfo? FindSetMethod(Type type, Type param1Type, Type param2Type)
        {
            // First try direct lookup on the type
            var method = type.GetMethod(
                "Set",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { param1Type, param2Type },
                null);

            if (method != null)
            {
                return method;
            }

            // Try with 3 parameters (string, object, bool) for the overwrite variant
            if (param2Type == typeof(object))
            {
                method = type.GetMethod(
                    "Set",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { param1Type, param2Type, typeof(bool) },
                    null);

                if (method != null)
                {
                    return method;
                }
            }

            // Search through all interfaces implemented by this type
            foreach (var iface in type.GetInterfaces())
            {
                // Check if interface name contains "SendHeaders" or "Headers"
                if (!iface.Name.Contains("Headers"))
                {
                    continue;
                }

                method = iface.GetMethod(
                    "Set",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { param1Type, param2Type },
                    null);

                if (method != null)
                {
                    // Get the implementation method from the concrete type
                    try
                    {
                        var map = type.GetInterfaceMap(iface);
                        for (var i = 0; i < map.InterfaceMethods.Length; i++)
                        {
                            if (map.InterfaceMethods[i] == method)
                            {
                                return map.TargetMethods[i];
                            }
                        }
                    }
                    catch
                    {
                        // Fall back to calling through the interface method
                        return method;
                    }
                }

                // Try with 3 parameters for interface
                if (param2Type == typeof(object))
                {
                    method = iface.GetMethod(
                        "Set",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { param1Type, param2Type, typeof(bool) },
                        null);

                    if (method != null)
                    {
                        try
                        {
                            var map = type.GetInterfaceMap(iface);
                            for (var i = 0; i < map.InterfaceMethods.Length; i++)
                            {
                                if (map.InterfaceMethods[i] == method)
                                {
                                    return map.TargetMethods[i];
                                }
                            }
                        }
                        catch
                        {
                            return method;
                        }
                    }
                }
            }

            return null;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // NOT USED - This adapter only INJECTS (writes) headers via Set(), never EXTRACTS (reads) them.
            // The SpanContextPropagator.Inject() method only calls Set(), not GetValues().
            yield break;
        }

        public void Set(string name, string value)
        {
            if (_headers == null)
            {
                return;
            }

            try
            {
                if (_setStringMethod != null)
                {
                    _setStringMethod.Invoke(_headers, new object[] { name, value });
                }
                else if (_setObjectMethod != null)
                {
                    // Set(string, object, bool) - pass true for overwrite
                    var parameters = _setObjectMethod.GetParameters();
                    if (parameters.Length == 3)
                    {
                        _setObjectMethod.Invoke(_headers, new object[] { name, value, true });
                    }
                    else
                    {
                        _setObjectMethod.Invoke(_headers, new object[] { name, value });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ContextPropagationInjectAdapter.Set: Failed to set header {Name}", name);
            }
        }

        public void Add(string name, string value)
        {
            // Delegates to Set() - both are used for injecting headers
            Set(name, value);
        }

        public void Remove(string name)
        {
            // NOT USED - Header removal is not needed for trace context injection.
            // We only add/set headers, never remove them.
        }
    }
}
