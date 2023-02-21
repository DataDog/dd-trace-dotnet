// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Ci;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Tagging;
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

        private string _origin;

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
            : this(traceId ?? 0, serviceName)
        {
            // public ctor must keep accepting legacy types:
            // - traceId: ulong? => TraceId
            // - samplingPriority: SamplingPriority? => int?
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
        internal SpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
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
        /// <param name="rawTraceId">The raw propagated trace id</param>
        /// <param name="rawSpanId">The raw propagated span id</param>
        internal SpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, string rawTraceId, string rawSpanId)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
            RawTraceId = rawTraceId;
            RawSpanId = rawSpanId;
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
        /// <param name="rawTraceId">Raw trace id value</param>
        /// <param name="rawSpanId">Raw span id value</param>
        internal SpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null)
            : this(GetTraceId(parent, traceId), serviceName)
        {
            // if 128-bit trace ids are enabled, also use full uint64 for span id,
            // otherwise keep using the legacy so-called uint63s.
            var useAllBits = traceContext?.Tracer?.Settings?.TraceId128BitGenerationEnabled ?? false;

            SpanId = spanId > 0 ? spanId : RandomIdGenerator.Shared.NextSpanId(useAllBits);
            Parent = parent;
            TraceContext = traceContext;

            if (parent is SpanContext spanContext)
            {
                RawTraceId = spanContext.RawTraceId ?? rawTraceId;
                PathwayContext = spanContext.PathwayContext;
            }
            else
            {
                RawTraceId = rawTraceId;
            }

            RawSpanId = rawSpanId;
        }

        private SpanContext(TraceId traceId, string serviceName)
        {
            // In this ctor we don't know if 128-bits are enabled or not, so use the default value "false".
            // To get around this, make sure the trace id is set
            // before getting here so this ctor doesn't need to generate it.
            TraceId128 = traceId == 0
                          ? RandomIdGenerator.Shared.NextTraceId(useAllBits: false)
                          : traceId;

            ServiceName = serviceName;

            // Because we have a ctor as part of the public api without accepting the origin tag,
            // we need to ensure new SpanContext created by this .ctor has the CI Visibility origin
            // tag if the CI Visibility mode is running to ensure the correct propagation
            // to children spans and distributed trace.
            if (CIVisibility.IsRunning)
            {
                Origin = Ci.Tags.TestTags.CIAppTestOriginName;
            }
        }

        /// <summary>
        /// Gets the parent context.
        /// </summary>
        public ISpanContext Parent { get; }

        /// <summary>
        /// Gets the lower 64-bits of the 128-bit trace id.
        /// </summary>
        ulong ISpanContext.TraceId => TraceId128.Lower;

        /// <summary>
        /// Gets the 128-bit trace id.
        /// </summary>
        internal TraceId TraceId128 { get; }

        /// <summary>
        /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
        /// </summary>
        public ulong TraceId => TraceId128.Lower;

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
        /// Gets or sets the origin of the trace.
        /// For local contexts, this property delegates to TraceContext.Origin.
        /// This is a temporary work around because we use SpanContext
        /// for all local spans and also for propagation.
        /// </summary>
        internal string Origin
        {
            get => TraceContext?.Origin ?? _origin;
            set
            {
                _origin = value;

                if (TraceContext is not null)
                {
                    TraceContext.Origin = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the propagated trace tags collection.
        /// </summary>
        internal TraceTagCollection PropagatedTags { get; set; }

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

        /// <summary>
        /// Gets the raw traceId (to support > 64bits)
        /// </summary>
        internal string RawTraceId { get; }

        /// <summary>
        /// Gets the raw spanId
        /// </summary>
        internal string RawSpanId { get; }

        /// <summary>
        /// Gets or sets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
        /// This value will _not_ include the "dd" key, which is parsed out into other individual values
        /// (e.g. sampling priority, origin, propagates tags, etc).
        /// </summary>
        internal string AdditionalW3CTraceState { get; set; }

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
                    // use the lower 64-bits for backwards compat, truncate using TraceId128.Lower
                    value = TraceId128.Lower.ToString(invariant);
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
                    value = TraceContext?.Tags.ToPropagationHeader() ?? PropagatedTags?.ToPropagationHeader();
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

        private static TraceId GetTraceId(ISpanContext context, TraceId fallback)
        {
            return context switch
                   {
                       // use the 128-bit TraceId if possible
                       SpanContext sc => sc.TraceId,
                       // otherwise use the 64-bit ulong
                       not null => context.TraceId,
                       // if no context, use the specified fallback value
                       null => fallback
                   };
        }

        /// <summary>
        /// Sets a DataStreams checkpoint
        /// </summary>
        /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
        /// <param name="checkpointKind">The type of the checkpoint</param>
        /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
        internal void SetCheckpoint(DataStreamsManager manager, CheckpointKind checkpointKind, string[] edgeTags)
        {
            PathwayContext = manager.SetCheckpoint(PathwayContext, checkpointKind, edgeTags);
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
