// <copyright file="ContextPropagationInjectAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
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

        // Cache of resolved Set methods per concrete headers type. The MassTransit headers type space
        // is small (DictionarySendHeaders and a handful of transport-specific subclasses), so unbounded
        // growth is not a concern.
        private static readonly ConcurrentDictionary<Type, SetMethods> SetMethodCache = new();
        private static readonly Func<Type, SetMethods> ResolveSetMethodsDelegate = ResolveSetMethods;

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
                var resolved = SetMethodCache.GetOrAdd(headers.GetType(), ResolveSetMethodsDelegate);
                _setStringMethod = resolved.SetString;
                _setObjectMethod = resolved.SetObject;
            }
        }

        private static SetMethods ResolveSetMethods(Type headersType)
        {
            // MassTransit's DictionarySendHeaders implements the SendHeaders interface
            // The Set methods may be implemented via the interface, so we need to search
            // both the concrete type and its interfaces

            // Try Set(string, string) first - most efficient
            var setStringMethod = FindSetMethod(headersType, typeof(string), typeof(string));
            MethodInfo? setObjectMethod = null;

            // Fall back to Set(string, object) or Set(string, object, bool)
            if (setStringMethod == null)
            {
                setObjectMethod = FindSetMethod(headersType, typeof(string), typeof(object));
            }

            if (setStringMethod == null && setObjectMethod == null)
            {
                Log.Debug(
                    "ContextPropagationInjectAdapter: Could not find suitable Set method on {HeadersType}",
                    headersType.FullName);
            }
            else
            {
                Log.Debug<string?, bool, bool>(
                    "ContextPropagationInjectAdapter: Found Set method on {HeadersType}: StringMethod={HasString}, ObjectMethod={HasObject}",
                    headersType.FullName,
                    setStringMethod != null,
                    setObjectMethod != null);
            }

            return new SetMethods(setStringMethod, setObjectMethod);
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
            // Not used - this adapter only injects (writes) headers via Set(), never extracts (reads) them.
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
            // Not used - header removal is not needed for trace context injection.
            // We only add/set headers, never remove them.
        }

        private readonly struct SetMethods
        {
            public SetMethods(MethodInfo? setString, MethodInfo? setObject)
            {
                SetString = setString;
                SetObject = setObject;
            }

            public MethodInfo? SetString { get; }

            public MethodInfo? SetObject { get; }
        }
    }
}
