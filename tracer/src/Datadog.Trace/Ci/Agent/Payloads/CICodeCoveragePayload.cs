// <copyright file="CICodeCoveragePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CICodeCoveragePayload : MultipartPayload
    {
        private readonly IFormatterResolver _formatterResolver;
        private readonly Stopwatch _serializationWatch;

        public CICodeCoveragePayload(CIVisibilitySettings settings, int maxItemsPerPayload = DefaultMaxItemsPerPayload, int maxBytesPerPayload = DefaultMaxBytesPerPayload, IFormatterResolver formatterResolver = null)
            : base(settings, maxItemsPerPayload, maxBytesPerPayload, formatterResolver)
        {
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _serializationWatch = new Stopwatch();

            // We call reset here to add the dummy event
            Reset();
        }

        public override string EventPlatformSubdomain => "citestcov-intake";

        public override string EventPlatformPath => "api/v2/citestcov";

        public override MetricTags.CIVisibilityEndpoints TelemetryEndpoint => MetricTags.CIVisibilityEndpoints.CodeCoverage;

        public override MetricTags.CIVisibilityEndpointAndCompression TelemetryEndpointAndCompression
            => UseGZip
                   ? MetricTags.CIVisibilityEndpointAndCompression.CodeCoverageRequestCompressed
                   : MetricTags.CIVisibilityEndpointAndCompression.CodeCoverageUncompressed;

        public override bool CanProcessEvent(IEvent @event)
        {
            return @event is TestCoverage;
        }

        protected override MultipartFormItem CreateMultipartFormItem(EventsBuffer<IEvent> eventsBuffer)
        {
            var index = Count;
            var eventInBytes = MessagePackSerializer.Serialize(new CoveragePayload(eventsBuffer), _formatterResolver);
            CIVisibility.Log.Debug<int, int>("CICodeCoveragePayload: Serialized {Count} test code coverage as a single multipart item with {Size} bytes.", eventsBuffer.Count, eventInBytes.Length);
            return new MultipartFormItem($"coverage{index}", MimeTypes.MsgPack, $"filecoverage{index}.msgpack", new ArraySegment<byte>(eventInBytes));
        }

        public override bool TryProcessEvent(IEvent @event)
        {
            _serializationWatch.Restart();
            var success = base.TryProcessEvent(@event);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointEventsSerializationMs(TelemetryEndpoint, _serializationWatch.Elapsed.TotalMilliseconds);
            return success;
        }

        public override void Reset()
        {
            base.Reset();

            // This is a current limitation in the backend side
            // an "event" item must be included in each payload.
            // So here we are adding a dummy one.
            AddMultipartFormItem(
                new MultipartFormItem(
                    "event",
                    MimeTypes.Json,
                    "fileevent.json",
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"dummy\": true}"))));
        }

        internal readonly struct CoveragePayload
        {
            public readonly EventsBuffer<IEvent> TestCoverageData;

            public CoveragePayload(EventsBuffer<IEvent> testCoverageData)
            {
                TestCoverageData = testCoverageData;
            }
        }
    }
}
