// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.DataStreamsMonitoring;
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
            Keys.TraceId,
            Keys.ParentId,
            Keys.SamplingPriority,
            Keys.Origin,
            Keys.RawTraceId,
            Keys.RawSpanId,
            Keys.PropagatedTags,
            Keys.AdditionalW3CTraceState,

            // For mismatch version support we need to keep supporting old keys.
            HttpHeaderNames.TraceId,
            HttpHeaderNames.ParentId,
            HttpHeaderNames.SamplingPriority,
            HttpHeaderNames.Origin,
        };

        /// <summary>
        /// An <see cref="ISpanContext"/> with default values. Can be used as the value for
        /// <see cref="SpanCreationSettings.Parent"/> in <see cref="Tracer.StartActive(string, SpanCreationSettings)"/>
        /// to specify that the new span should not inherit the currently active scope as its parent.
        /// </summary>
        public static readonly ISpanContext None = new ReadOnlySpanContext(traceId: 0, spanId: 0, serviceName: null);

        private readonly ulong _traceId;
        private readonly string _origin;
        private readonly int? _samplingPriority;
        private readonly string _rawTraceId;
        private readonly string _additionalW3CTraceState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class from a propagated context.
        /// This SpanContext should only be used as the parent of local root Span in
        /// <see cref="Tracer.StartActive(string)"/> or  <see cref="Tracer.StartActive(string,SpanCreationSettings)"/>.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        public SpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority = null, string serviceName = null)
            : this(
                traceId ?? RandomIdGenerator.Shared.NextSpanId(),
                spanId,
                (int?)samplingPriority,
                origin: null,
                rawTraceId: null,
                rawSpanId: null,
                propagatedTags: null,
                additionalW3CTraceState: null)
        {
            // NOTE: this is a public ctor so we must keep accepting deprecated values
            // - SamplingPriority samplingPriority -> convert to Nullable<int>
            // - [COMING SOON] string serviceName (we should move this to Span)
            // - [COMING SOON] ulong? traceId -> convert to TraceId for 128-bit support
            ServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class from a propagated context.
        /// This SpanContext should only be used as the parent of local root Span in
        /// methods like <see cref="Tracer.StartActiveInternal"/>.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="origin">The propagated origin of the trace.</param>
        /// <param name="rawTraceId">The raw propagated trace id</param>
        /// <param name="rawSpanId">The raw propagated span id</param>
        /// <param name="propagatedTags">The value of the propagated tags header "x-datadog-tags".</param>
        /// <param name="additionalW3CTraceState">Additional values found in the W3C "tracestate" header.</param>
        internal SpanContext(
            ulong traceId,
            ulong spanId,
            int? samplingPriority,
            string origin = null,
            string rawTraceId = null,
            string rawSpanId = null,
            string propagatedTags = null,
            string additionalW3CTraceState = null)
        {
            // properties than can delegate to TraceContext need a private field, the rest can be auto-properties
            _traceId = traceId;
            SpanId = spanId;
            _samplingPriority = samplingPriority;
            _origin = origin;
            _rawTraceId = rawTraceId;
            RawSpanId = rawSpanId;
            PropagatedTags = propagatedTags;
            _additionalW3CTraceState = additionalW3CTraceState;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// that is the child of the specified parent context.
        /// </summary>
        /// <param name="parent">The parent context.</param>
        /// <param name="traceContext">The trace context.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="rawSpanId">Raw span id value</param>
        internal SpanContext(
            ISpanContext parent,
            TraceContext traceContext,
            string serviceName = null,
            ulong? spanId = null,
            string rawSpanId = null)
        {
            Parent = parent;
            TraceContext = traceContext;
            ServiceName = serviceName;

            // use the specified spanId or generate a random value
            SpanId = spanId ?? RandomIdGenerator.Shared.NextSpanId();

            if (parent is SpanContext spanContext)
            {
                _rawTraceId = spanContext.RawTraceId ?? rawTraceId;
                PathwayContext = spanContext.PathwayContext;
            }
            else
            {
                _rawTraceId = rawTraceId;
            }

            RawSpanId = rawSpanId;
        }

        /// <summary>
        /// Gets the parent context.
        /// </summary>
        public ISpanContext Parent { get; }

        /// <summary>
        /// Gets the lower-order 64 bits of the 128-bit trace id.
        /// </summary>
        /// <remarks>
        /// For local contexts, this property delegates to TraceContext.
        /// For remote contexts, it returns the value extracted from upstream headers.
        /// </remarks>
        public ulong TraceId => TraceContext?.TraceId ?? _traceId;

        /// <summary>
        /// Gets the span id of the parent span.
        /// </summary>
        public ulong? ParentId => Parent?.SpanId;

        /// <summary>
        /// Gets the span id.
        /// </summary>
        public ulong SpanId { get; }

        /// <summary>
        /// Gets or sets the service name to propagate to child spans.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets the origin of the trace.
        /// </summary>
        /// <remarks>
        /// For local contexts, this property delegates to TraceContext.
        /// For remote contexts, it contains the value extracted from upstream headers.
        /// This is a temporary work around because we use SpanContext
        /// for both local spans and for propagation.
        /// </remarks>
        internal string Origin => TraceContext?.Origin ?? _origin;

        /// <summary>
        /// Gets the header value that contains the propagated trace tags,
        /// formatted as "key1=value1,key2=value2".
        /// </summary>
        internal string PropagatedTags { get; }

        /// <summary>
        /// Gets the trace context.
        /// Returns null for contexts created from incoming propagated context.
        /// </summary>
        internal TraceContext TraceContext { get; }

        /// <summary>
        /// Gets the sampling priority of the trace.
        /// </summary>
        /// <remarks>
        /// For local contexts, this property delegates to TraceContext.
        /// For remote contexts, it contains the value extracted from upstream headers.
        /// This is a temporary work around because we use SpanContext
        /// for both local spans and for propagation.
        /// </remarks>
        internal int? SamplingPriority => TraceContext?.SamplingPriority ?? _samplingPriority;

        /// <summary>
        /// Gets the raw trace id string (usually hexadecimal) extracted from an upstream distributed context.
        /// </summary>
        /// <remarks>
        /// For local contexts, this property delegates to TraceContext.
        /// For remote contexts, it contains the value extracted from upstream headers.
        /// This is a temporary work around because we use SpanContext
        /// for both local spans and for propagation.
        /// </remarks>
        internal string RawTraceId => TraceContext?.RawTraceId ?? _rawTraceId;

        /// <summary>
        /// Gets the raw span id string (usually hexadecimal) extracted from an upstream distributed context.
        /// </summary>
        internal string RawSpanId { get; }

        /// <summary>
        /// Gets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
        /// This value will _not_ include the "dd" key, which is parsed out into other individual values
        /// (e.g. sampling priority, origin, propagates tags, etc).
        /// </summary>
        /// <remarks>
        /// For local contexts, this property delegates to TraceContext.
        /// For remote contexts, it contains the value extracted from upstream headers.
        /// This is a temporary work around because we use SpanContext
        /// for both local spans and for propagation.
        /// </remarks>
        internal string AdditionalW3CTraceState => TraceContext?.AdditionalW3CTraceState ?? _additionalW3CTraceState;

        internal PathwayContext? PathwayContext { get; private set; }

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
                case Keys.TraceId:
                case HttpHeaderNames.TraceId:
                    value = TraceId.ToString(invariant);
                    return true;

                case Keys.ParentId:
                case HttpHeaderNames.ParentId:
                    value = SpanId.ToString(invariant);
                    return true;

                case Keys.SamplingPriority:
                case HttpHeaderNames.SamplingPriority:
                    // return the value from TraceContext if available
                    var samplingPriority = TraceContext?.SamplingPriority ?? SamplingPriority;
                    value = samplingPriority?.ToString(invariant);
                    return true;

                case Keys.Origin:
                case HttpHeaderNames.Origin:
                    value = Origin;
                    return true;

                case Keys.RawTraceId:
                    value = RawTraceId;
                    return true;

                case Keys.RawSpanId:
                    value = RawSpanId;
                    return true;

                case Keys.PropagatedTags:
                case HttpHeaderNames.PropagatedTags:
                    // return the value from TraceContext if available
                    value = TraceContext?.Tags.ToPropagationHeader() ?? PropagatedTags;
                    return true;

                case Keys.AdditionalW3CTraceState:
                    // return the value from TraceContext if available
                    value = TraceContext?.AdditionalW3CTraceState ?? AdditionalW3CTraceState;
                    return true;

                default:
                    value = null;
                    return false;
            }
        }

        /// <summary>
        /// Sets a DataStreams checkpoint
        /// </summary>
        /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
        /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
        internal void SetCheckpoint(DataStreamsManager manager, string[] edgeTags)
        {
            PathwayContext = manager.SetCheckpoint(PathwayContext, edgeTags);
        }

        /// <summary>
        /// Merges two DataStreams <see cref="PathwayContext"/>
        /// Should be called when a pathway context is extracted from an incoming span
        /// Used to merge contexts in a "fan in" scenario.
        /// </summary>
        internal void MergePathwayContext(PathwayContext? pathwayContext)
        {
            if (pathwayContext is null)
            {
                return;
            }

            if (PathwayContext is null)
            {
                PathwayContext = pathwayContext;
                return;
            }

            // This is purposely not thread safe
            // The code randomly chooses between the two PathwayContexts.
            // If there is a race, then that's okay
            // Randomly select between keeping the current context (0) or replacing (1)
            if (ThreadSafeRandom.Shared.Next(2) == 1)
            {
                PathwayContext = pathwayContext;
            }
        }

        internal static class Keys
        {
            private const string Prefix = "__DistributedKey-";

            public const string TraceId = $"{Prefix}TraceId";
            public const string ParentId = $"{Prefix}ParentId";
            public const string SamplingPriority = $"{Prefix}SamplingPriority";
            public const string Origin = $"{Prefix}Origin";
            public const string RawTraceId = $"{Prefix}RawTraceId";
            public const string RawSpanId = $"{Prefix}RawSpanId";
            public const string PropagatedTags = $"{Prefix}PropagatedTags";
            public const string AdditionalW3CTraceState = $"{Prefix}AdditionalW3CTraceState";
        }
    }
}
