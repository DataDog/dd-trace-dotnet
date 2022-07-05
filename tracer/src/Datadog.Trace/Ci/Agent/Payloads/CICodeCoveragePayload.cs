// <copyright file="CICodeCoveragePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Coverage.Models;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CICodeCoveragePayload : CIVisibilityMultipartPayload
    {
        public CICodeCoveragePayload(IFormatterResolver formatterResolver = null)
            : base(10, formatterResolver)
        {
            var agentlessUrl = CIVisibility.Settings.AgentlessUrl;
            if (!string.IsNullOrWhiteSpace(agentlessUrl))
            {
                var builder = new UriBuilder(agentlessUrl);
                builder.Path = "api/v2/citestcov";
                Url = builder.Uri;
            }
            else
            {
                var builder = new UriBuilder("https://datadog.host.com/api/v2/citestcov");
                builder.Host = "event-platform-intake." + CIVisibility.Settings.Site;
                Url = builder.Uri;
            }
        }

        public override Uri Url { get; }

        public override bool CanProcessEvent(IEvent @event)
        {
            return @event is CoveragePayload;
        }

        protected override MultipartFormItem CreateMultipartFormItem(ArraySegment<byte> eventInBytes)
        {
            var index = Count + 1;
            return new MultipartFormItem($"coverage{index}", MimeTypes.MsgPack, $"filecoverage{index}.msgpack", eventInBytes);
        }

        protected override void OnBeforeToArray()
        {
            AddMultipartFormItem(
                new MultipartFormItem(
                    "event",
                    MimeTypes.Json,
                    "fileevent.json",
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"dummy\": true}"))));
        }
    }
}
