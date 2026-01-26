// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Adapter for extracting trace context from MassTransit Headers (consume side)
    /// JsonTransportHeaders has: GetAll() which returns IEnumerable[HeaderValue]
    /// The Get method has a constraint (T : struct) so we use GetAll instead
    /// </summary>
    internal readonly struct ContextPropagation : IHeadersCollection
    {
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
}
