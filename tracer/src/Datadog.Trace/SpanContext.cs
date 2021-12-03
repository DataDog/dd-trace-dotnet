// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
    /// </summary>
    public class SpanContext : ISpanContext, IReadOnlyDictionary<string, string>
    {
        private static readonly string[] KeyNames =
        {
            HttpHeaderNames.TraceId,
            HttpHeaderNames.ParentId,
            HttpHeaderNames.SamplingPriority,
            HttpHeaderNames.Origin,
            HttpHeaderNames.DatadogTags,
        };

        /// <summary>
        /// An <see cref="ISpanContext"/> with default values. Can be used as the value for
        /// <see cref="SpanCreationSettings.Parent"/> in <see cref="Tracer.StartActive(string, SpanCreationSettings)"/>
        /// to specify that the new span should not inherit the currently active scope as its parent.
        /// </summary>
        public static readonly ISpanContext None = new ReadOnlySpanContext(traceId: 0, spanId: 0, serviceName: null);

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// from a propagated context. <see cref="Parent"/> will be null
        /// since this is a root context locally.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        public SpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority = null, string serviceName = null)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = (int?)samplingPriority;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// from a propagated context. <see cref="Parent"/> will be null
        /// since this is a root context locally.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        /// <param name="origin">The propagated origin of the trace.</param>
        internal SpanContext(ulong? traceId, ulong spanId, int? samplingPriority, string serviceName, string origin)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// that is the child of the specified parent context.
        /// </summary>
        /// <param name="parent">The parent context.</param>
        /// <param name="traceContext">The trace context.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        /// <param name="traceId">Override the trace id if there's no parent.</param>
        /// <param name="spanId">The propagated span id.</param>
        internal SpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, ulong? traceId = null, ulong? spanId = null)
            : this(parent?.TraceId ?? traceId, serviceName)
        {
            SpanId = spanId ?? SpanIdGenerator.ThreadInstance.CreateNew();
            Parent = parent;
            TraceContext = traceContext;
            if (parent is SpanContext spanContext)
            {
                Origin = spanContext.Origin;
            }
        }

        private SpanContext(ulong? traceId, string serviceName)
        {
            TraceId = traceId > 0
                          ? traceId.Value
                          : SpanIdGenerator.ThreadInstance.CreateNew();

            ServiceName = serviceName;
        }

        /// <summary>
        /// Gets the parent context.
        /// </summary>
        public ISpanContext Parent { get; }

        /// <summary>
        /// Gets the trace id
        /// </summary>
        public ulong TraceId { get; }

        /// <summary>
        /// Gets the span id of the parent span
        /// </summary>
        public ulong? ParentId => Parent?.SpanId;

        /// <summary>
        /// Gets the span id
        /// </summary>
        public ulong SpanId { get; }

        /// <summary>
        /// Gets or sets the service name to propagate to child spans.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the origin of the trace
        /// </summary>
        internal string Origin { get; set; }

        /// <summary>
        /// Gets or sets a collection of propagated internal Datadog tags,
        /// formatted as "key1=value1,key2=value2".
        /// </summary>
        /// <remarks>
        /// We're keeping this as the string representation to avoid having to parse.
        /// For now, it's relatively easy to append new values when needed.
        /// </remarks>
        internal string DatadogTags { get; set; }

        /// <summary>
        /// Gets the trace context.
        /// Returns null for contexts created from incoming propagated context.
        /// </summary>
        internal TraceContext TraceContext { get; }

        /// <summary>
        /// Gets the sampling priority for contexts created from incoming propagated context.
        /// Returns null for local contexts.
        /// </summary>
        internal int? SamplingPriority { get; }

        /// <inheritdoc/>
        int IReadOnlyCollection<KeyValuePair<string, string>>.Count => KeyNames.Length;

        /// <inheritdoc />
        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => KeyNames;

        /// <inheritdoc/>
        IEnumerable<string> IReadOnlyDictionary<string, string>.Values
        {
            get
            {
                foreach (var key in KeyNames)
                {
                    yield return ((IReadOnlyDictionary<string, string>)this)[key];
                }
            }
        }

        /// <inheritdoc/>
        string IReadOnlyDictionary<string, string>.this[string key]
        {
            get
            {
                if (((IReadOnlyDictionary<string, string>)this).TryGetValue(key, out var value))
                {
                    return value;
                }

                ThrowHelper.ThrowKeyNotFoundException($"Key not found: {key}");
                return default;
            }
        }

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            var dictionary = (IReadOnlyDictionary<string, string>)this;

            foreach (var key in KeyNames)
            {
                yield return new KeyValuePair<string, string>(key, dictionary[key]);
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IReadOnlyDictionary<string, string>)this).GetEnumerator();
        }

        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, string>.ContainsKey(string key)
        {
            foreach (var k in KeyNames)
            {
                if (k == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value)
        {
            var invariant = CultureInfo.InvariantCulture;

            switch (key)
            {
                case HttpHeaderNames.TraceId:
                    value = TraceId.ToString(invariant);
                    return true;

                case HttpHeaderNames.ParentId:
                    value = SpanId.ToString(invariant);
                    return true;

                case HttpHeaderNames.SamplingPriority:
                    value = SamplingPriority?.ToString(invariant);
                    return true;

                case HttpHeaderNames.Origin:
                    value = Origin;
                    return true;

                case HttpHeaderNames.DatadogTags:
                    value = DatadogTags;
                    return true;

                default:
                    value = null;
                    return false;
            }
        }
    }
}
