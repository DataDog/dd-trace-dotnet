// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Datadog.Trace.Ci;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
    /// </summary>
    public partial class SpanContext : ISpanContext, IReadOnlyDictionary<string, string>
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
            Keys.LastParentId,

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
        public static readonly ISpanContext None = new ReadOnlySpanContext(traceId: Trace.TraceId.Zero, spanId: 0, serviceName: null);

        private string _rawTraceId;
        private string _rawSpanId;
        private string _origin;
        private string _additionalW3CTraceState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// from a propagated context. <see cref="ParentInternal"/> will be null
        /// since this is a root context locally.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        [PublicApi]
        public SpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority = null, string serviceName = null)
            : this((TraceId)(traceId ?? 0), serviceName)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContext_Ctor);
            // public ctor must keep accepting legacy types:
            // - traceId: ulong? => TraceId
            // - samplingPriority: SamplingPriority? => int?
            SpanId = spanId;
            SamplingPriority = (int?)samplingPriority;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// from a propagated context. <see cref="ParentInternal"/> will be null
        /// since this is a root context locally.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        /// <param name="origin">The propagated origin of the trace.</param>
        /// <param name="isRemote">Whether this <see cref="SpanContext"/> was from a distributed context.</param>
        internal SpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, bool isRemote = false)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
            IsRemote = isRemote;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> class
        /// from a propagated context. <see cref="ParentInternal"/> will be null
        /// since this is a root context locally.
        /// </summary>
        /// <param name="traceId">The propagated trace id.</param>
        /// <param name="spanId">The propagated span id.</param>
        /// <param name="samplingPriority">The propagated sampling priority.</param>
        /// <param name="serviceName">The service name to propagate to child spans.</param>
        /// <param name="origin">The propagated origin of the trace.</param>
        /// <param name="rawTraceId">The raw propagated trace id</param>
        /// <param name="rawSpanId">The raw propagated span id</param>
        /// <param name="isRemote">Whether this <see cref="SpanContext"/> was from a distributed context.</param>
        internal SpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, string rawTraceId, string rawSpanId, bool isRemote = false)
            : this(traceId, serviceName)
        {
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
            _rawTraceId = rawTraceId;
            _rawSpanId = rawSpanId;
            IsRemote = isRemote;
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
        /// <param name="isRemote">Whether this <see cref="SpanContext"/> was from a distributed context.</param>
        internal SpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null, bool isRemote = false)
            : this(GetTraceId(parent, traceId), serviceName)
        {
            // if 128-bit trace ids are enabled, also use full uint64 for span id,
            // otherwise keep using the legacy so-called uint63s.
            var useAllBits = traceContext?.Tracer?.Settings?.TraceId128BitGenerationEnabled ?? true;

            SpanId = spanId > 0 ? spanId : RandomIdGenerator.Shared.NextSpanId(useAllBits);
            ParentInternal = parent;
            TraceContext = traceContext;

            if (parent is SpanContext spanContext)
            {
                _rawTraceId = spanContext._rawTraceId ?? rawTraceId;
                PathwayContext = spanContext.PathwayContext;
            }
            else
            {
                _rawTraceId = rawTraceId;
            }

            _rawSpanId = rawSpanId;
            IsRemote = isRemote;
        }

        private SpanContext(TraceId traceId, string serviceName)
        {
            TraceId128 = traceId == Trace.TraceId.Zero
                          ? RandomIdGenerator.Shared.NextTraceId(useAllBits: false)
                          : traceId;

            ServiceNameInternal = serviceName;

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
        [GeneratePublicApi(PublicApiUsage.SpanContext_Parent_Get)]
        internal ISpanContext ParentInternal { get; }

        /// <summary>
        /// Gets the 128-bit trace id.
        /// </summary>
        internal TraceId TraceId128 { get; }

        /// <summary>
        /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
        /// </summary>
        [PublicApi]
        public ulong TraceId => TraceId128.Lower;

        /// <summary>
        /// Gets the span id of the parent span.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.SpanContext_ParentId_Get)]
        internal ulong? ParentIdInternal => ParentInternal?.SpanId;

        /// <summary>
        /// Gets the span id.
        /// </summary>
        public ulong SpanId { get; internal set; }

        /// <summary>
        /// Gets or sets the service name to propagate to child spans.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.SpanContext_ServiceName_Get, PublicApiUsage.SpanContext_ServiceName_Set)]
        internal string ServiceNameInternal { get; set; }

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
        /// Gets the trace id as a hexadecimal string of length 32,
        /// padded with zeros to the left if needed.
        /// </summary>
        internal string RawTraceId => _rawTraceId ??= HexString.ToHexString(TraceId128);

        /// <summary>
        /// Gets or sets the span id as a hexadecimal string of length 16,
        /// padded with zeros to the left if needed.
        /// </summary>
        internal string RawSpanId
        {
            get => _rawSpanId ??= HexString.ToHexString(SpanId);
            set => _rawSpanId = value;
        }

        /// <summary>
        /// Gets or sets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
        /// This value will _not_ include the "dd" key, which is parsed out into other individual values
        /// (e.g. sampling priority, origin, propagates tags, etc).
        /// </summary>
        internal string AdditionalW3CTraceState
        {
            get => TraceContext?.AdditionalW3CTraceState ?? _additionalW3CTraceState;
            set
            {
                _additionalW3CTraceState = value;

                if (TraceContext is not null)
                {
                    TraceContext.AdditionalW3CTraceState = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the last span ID of the most recently seen Datadog span that will be propagated downstream
        /// to allow for the re-parenting of spans in cases where spans in distributed traces have missing spans.
        /// </summary>
        internal string LastParentId { get; set; }

        internal PathwayContext? PathwayContext { get; private set; }

        /// <summary>
        ///  Gets a value indicating whether this <see cref="SpanContext"/> was propagated from a remote parent.
        /// </summary>
        internal bool IsRemote { get; }

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
                    // returns the 64-bit span id in decimal encoding
                    value = SpanId.ToString(invariant);
                    return true;

                case Keys.SamplingPriority:
                case HttpHeaderNames.SamplingPriority:
                    var samplingPriority = GetOrMakeSamplingDecision();
                    value = samplingPriority?.ToString(invariant);
                    return true;

                case Keys.Origin:
                case HttpHeaderNames.Origin:
                    value = Origin;
                    return true;

                case Keys.RawTraceId:
                    // returns the full 128-bit trace id in hexadecimal encoding
                    value = RawTraceId;
                    return true;

                case Keys.RawSpanId:
                    // returns the 64-bit span id in hexadecimal encoding
                    value = RawSpanId;
                    return true;

                case Keys.PropagatedTags:
                case HttpHeaderNames.PropagatedTags:
                    value = PrepareTagsHeaderForPropagation();
                    return true;

                case Keys.AdditionalW3CTraceState:
                    // return the value from TraceContext if available
                    value = TraceContext?.AdditionalW3CTraceState ?? AdditionalW3CTraceState;
                    return true;

                case Keys.LastParentId:
                    value = LastParentId;
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
                       // if there is no context or it has a zero trace id,
                       // use the specified fallback value
                       null or { TraceId: 0 } => fallback,

                       // use the 128-bit trace id from SpanContext if possible
                       SpanContext sc => sc.TraceId128,

                       // otherwise use the 64-bit trace id from ISpanContext
                       _ => (TraceId)context.TraceId
                   };
        }

        /// <summary>
        /// If <see cref="TraceContext"/> is not null, returns <see cref="Trace.TraceContext.GetOrMakeSamplingDecision"/>.
        /// Otherwise, returns <see cref="SamplingPriority"/>.
        /// </summary>
        internal int? GetOrMakeSamplingDecision() =>
            TraceContext?.GetOrMakeSamplingDecision() ?? // this SpanContext belongs to a local trace
            SamplingPriority; // this a propagated context (also some tests rely on this)

        [return: MaybeNull]
        internal TraceTagCollection PrepareTagsForPropagation()
        {
            TraceTagCollection propagatedTags;

            // use the value from TraceContext if available
            if (TraceContext != null)
            {
                propagatedTags = TraceContext.Tags;
            }
            else
            {
                if (TraceId128.Upper > 0 && PropagatedTags == null)
                {
                    // we need to add the "_dd.p.tid" propagated tag, so create a new collection if we don't have one
                    PropagatedTags = new TraceTagCollection();
                }

                propagatedTags = PropagatedTags;
            }

            // add, replace, or remove the "_dd.p.tid" tag
            propagatedTags?.FixTraceIdTag(TraceId128);
            return propagatedTags;
        }

        [return: MaybeNull]
        internal string PrepareTagsHeaderForPropagation()
        {
            // try to get max length from tracer settings, but do NOT access Tracer.Instance
            var headerMaxLength = TraceContext?.Tracer?.Settings?.OutgoingTagPropagationHeaderMaxLength;

            var propagatedTags = PrepareTagsForPropagation();
            return propagatedTags?.ToPropagationHeader(headerMaxLength);
        }

        /// <summary>
        /// Sets a DataStreams checkpoint
        /// </summary>
        /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
        /// <param name="checkpointKind">The type of the checkpoint</param>
        /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
        /// <param name="payloadSizeBytes">Payload size in bytes</param>
        /// <param name="timeInQueueMs">Edge start time extracted from the message metadata. Used only if this is start of the pathway</param>
        /// <param name="parent">The parent context, if known</param>
        internal void SetCheckpoint(DataStreamsManager manager, CheckpointKind checkpointKind, string[] edgeTags, long payloadSizeBytes, long timeInQueueMs, PathwayContext? parent)
        {
            if (manager != null)
            {
                PathwayContext = manager.SetCheckpoint(parent, checkpointKind, edgeTags, payloadSizeBytes, timeInQueueMs);
            }
        }

        /// <summary>
        /// There shouldn't be any need to manually set the pathway context to a known value,
        /// except in the case where messages are consumed in batch, and then processed individually to produce more messages,
        /// in which case we need to recover the consume checkpoint so that the produce checkpoint is properly linked to it.
        /// Kafka is the only integration offering that feature for now.
        /// </summary>
        internal void ManuallySetPathwayContextToPairMessages(PathwayContext? pathwayContext)
        {
            PathwayContext = pathwayContext;
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
            public const string LastParentId = $"{Prefix}LastParentId";
        }
    }
}
