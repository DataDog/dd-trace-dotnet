// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Adapter for extracting trace context from MassTransit Headers (consume side).
    /// JsonTransportHeaders has: GetAll() which returns IEnumerable[HeaderValue]
    /// The Get method has a constraint (T : struct) so we use GetAll instead.
    /// </summary>
    internal readonly struct ContextPropagation : IHeadersCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

        private readonly object? _headers;
        private readonly MethodInfo? _getAllMethod;

        public ContextPropagation(object? headers)
        {
            _headers = headers;
            _getAllMethod = null;

            if (headers != null)
            {
                // Find the GetAll method that returns IEnumerable<HeaderValue>
                var headersType = headers.GetType();
                _getAllMethod = headersType.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            }
        }

        public IEnumerable<string> GetValues(string name)
        {
            string? result = null;

            if (_headers != null && _getAllMethod != null)
            {
                try
                {
                    // GetAll returns IEnumerable<HeaderValue> where HeaderValue has Key and Value properties
                    var allHeaders = _getAllMethod.Invoke(_headers, null);
                    if (allHeaders is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var headerValue in enumerable)
                        {
                            if (headerValue == null)
                            {
                                continue;
                            }

                            // HeaderValue is a struct with Key and Value properties
                            var hvType = headerValue.GetType();
                            var keyProp = hvType.GetProperty("Key");
                            var valueProp = hvType.GetProperty("Value");

                            if (keyProp != null && valueProp != null)
                            {
                                var key = keyProp.GetValue(headerValue) as string;
                                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                                {
                                    var value = valueProp.GetValue(headerValue);
                                    if (value != null)
                                    {
                                        result = value.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors reading headers
                }
            }

            if (result != null)
            {
                yield return result;
            }
        }

        public void Set(string name, string value)
        {
            // Headers on consume side are read-only
        }

        public void Add(string name, string value)
        {
            // Headers on consume side are read-only
        }

        public void Remove(string name)
        {
            // Headers on consume side are read-only
        }
    }

    /// <summary>
    /// Adapter for injecting trace context into MassTransit SendContext Headers (producer side).
    /// MassTransit's SendHeaders interface has:
    /// - Set(string key, string value)
    /// - Set(string key, object value, bool overwrite = true)
    /// </summary>
    internal readonly struct SendContextHeadersAdapter : IHeadersCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SendContextHeadersAdapter));

        private readonly object? _headers;
        private readonly MethodInfo? _setStringMethod;
        private readonly MethodInfo? _setObjectMethod;

        public SendContextHeadersAdapter(object? headers)
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
            // Not used for injection
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
                    Log.Debug("SendContextHeadersAdapter: Set header (string) {Name}={Value}", name, value);
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

                    Log.Debug("SendContextHeadersAdapter: Set header (object) {Name}={Value}", name, value);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SendContextHeadersAdapter: Failed to set header {Name}", name);
            }
        }

        public void Add(string name, string value)
        {
            Set(name, value);
        }

        public void Remove(string name)
        {
            // Not typically needed for injection
        }
    }
}
