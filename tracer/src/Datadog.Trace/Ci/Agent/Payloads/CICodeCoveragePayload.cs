// <copyright file="CICodeCoveragePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Models;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CICodeCoveragePayload : MultipartPayload
    {
        public CICodeCoveragePayload(CIVisibilitySettings settings, int maxItemsPerPayload = DefaultMaxItemsPerPayload, int maxBytesPerPayload = DefaultMaxBytesPerPayload, IFormatterResolver formatterResolver = null)
            : base(settings, maxItemsPerPayload, maxBytesPerPayload, formatterResolver)
        {
            // We call reset here to add the dummy event
            Reset();
        }

        public override string EventPlatformSubdomain => "event-platform-intake";

        public override string EventPlatformPath => "api/v2/citestcov";

        public override bool HasEvents
        {
            get
            {
                // The List will always have at least 1 item due the backend limitation.
                // By comparing > 1 will avoid sending an empty payload each second.
                return Count > 1;
            }
        }

        public override bool CanProcessEvent(IEvent @event)
        {
            return @event is CoveragePayload;
        }

        protected override MultipartFormItem CreateMultipartFormItem(ArraySegment<byte> eventInBytes)
        {
            var index = Count + 1;
            return new MultipartFormItem($"coverage{index}", MimeTypes.MsgPack, $"filecoverage{index}.msgpack", eventInBytes);
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
    }
}
