// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Adapter for extracting trace context from MassTransit Headers (consume side)
    /// </summary>
    internal readonly struct ContextPropagation : IHeadersCollection
    {
        private readonly IHeaders? _headers;

        public ContextPropagation(IHeaders? headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers?.TryGetHeader(name, out var value) == true && value != null)
            {
                yield return value.ToString() ?? string.Empty;
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
    /// Adapter for injecting trace context into MassTransit SendHeaders (send side)
    /// </summary>
    internal readonly struct SendContextPropagation : IHeadersCollection
    {
        private readonly ISendHeaders? _headers;

        public SendContextPropagation(ISendHeaders? headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // SendHeaders is write-only for our purposes
            yield break;
        }

        public void Set(string name, string value)
        {
            _headers?.Set(name, value, true);
        }

        public void Add(string name, string value)
        {
            // Set replaces existing values, which is the desired behavior
            _headers?.Set(name, value, true);
        }

        public void Remove(string name)
        {
            // MassTransit SendHeaders doesn't have a Remove method
        }
    }

    /// <summary>
    /// Adapter for injecting trace context into MassTransit SendHeaders using reflection
    /// This bypasses duck typing to directly invoke the Set method
    /// </summary>
    internal readonly struct ReflectionSendHeadersAdapter : IHeadersCollection
    {
        private readonly object _headers;
        private readonly MethodInfo _setMethod;

        public ReflectionSendHeadersAdapter(object headers, MethodInfo setMethod)
        {
            _headers = headers;
            _setMethod = setMethod;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // SendHeaders is write-only for our purposes
            yield break;
        }

        public void Set(string name, string value)
        {
            // Call Set(string key, object value, bool overwrite)
            _setMethod.Invoke(_headers, new object[] { name, value, true });
        }

        public void Add(string name, string value)
        {
            // Set replaces existing values, which is the desired behavior
            _setMethod.Invoke(_headers, new object[] { name, value, true });
        }

        public void Remove(string name)
        {
            // MassTransit SendHeaders doesn't have a Remove method
        }
    }

    /// <summary>
    /// Adapter for extracting trace context from MassTransit Headers using reflection
    /// This bypasses duck typing to directly invoke the TryGetHeader method
    /// </summary>
    internal readonly struct ReflectionHeadersAdapter : IHeadersCollection
    {
        private readonly object _headers;
        private readonly MethodInfo _tryGetHeaderMethod;

        public ReflectionHeadersAdapter(object headers, MethodInfo tryGetHeaderMethod)
        {
            _headers = headers;
            _tryGetHeaderMethod = tryGetHeaderMethod;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // Call TryGetHeader(string key, out object value)
            var parameters = new object?[] { name, null };
            var result = (bool)_tryGetHeaderMethod.Invoke(_headers, parameters)!;
            if (result && parameters[1] != null)
            {
                yield return parameters[1]!.ToString() ?? string.Empty;
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
