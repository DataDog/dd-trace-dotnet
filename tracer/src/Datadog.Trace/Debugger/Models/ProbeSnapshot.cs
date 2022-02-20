// <copyright file="ProbeSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger
{
    internal readonly record struct ProbeSnapshot
    {
        public ProbeMethodCapture Captures { get; init; }

        public ProbeInfo Probe { get; init; }

        public ThreadInfo Thread { get; init; }

        public StackInfo[] Stack { get; init; }

        public string Id { get; init; }

        public string Language { get; init; }

        public int Duration { get; init; }

        public long Timestamp { get; init; }

        public int TraceId { get; init; }

        public int SpanId { get; init; }

        public int? Version { get; init; }
    }

    internal readonly record struct ProbeInfo
    {
        public string Id { get; init; }

        public ProbeLocation Location { get; init; }
    }

    internal readonly record struct ThreadInfo
    {
        public int Id { get; init; }

        public string Name { get; init; }
    }

    internal readonly record struct StackInfo
    {
        public string Method { get; init; }

        public string FileName { get; init; }

        public int LineNumber { get; init; }
    }

    internal readonly record struct ProbeLocation
    {
        public string Method { get; init; }

        public string Type { get; init; }
    }

    internal readonly record struct ProbeMethodCapture
    {
        public ProbeMethodEntryAndReturn Entry { get; init; }

        public ProbeMethodEntryAndReturn Return { get; init; }
    }

    internal readonly record struct Throwable
    {
        public string Message { get; init; }

        public string Type { get; init; }

        public string[] Stacktrace { get; init; }
    }

    internal record CapturedObject
    {
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        public string Name { get; set; }

        public string Type { get; init; }

        public string Value { get; init; }

        [JsonIgnore]
        public CapturedObject[] Fields { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_additionalData.TryGetValue("fields", out var fields))
            {
                Fields = fields.Children<JProperty>().Select(c =>
                {
                    var jsonObject = c.Children().FirstOrDefault();
                    if (jsonObject == null)
                    {
                        return default;
                    }

                    if (jsonObject.Type == JTokenType.String)
                    {
                        return new CapturedObject { Name = c.Name, Value = jsonObject.ToString() };
                    }

                    var co = jsonObject.ToObject<CapturedObject>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }
        }
    }

    internal record ProbeMethodEntryAndReturn
    {
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        public CapturedObject This { get; set; }

        [JsonIgnore]
        public CapturedObject[] Arguments { get; set; }

        [JsonIgnore]
        public CapturedObject[] Locals { get; set; }

        public Throwable Throwable { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_additionalData.TryGetValue("arguments", out var arguments))
            {
                Arguments = arguments.Children<JProperty>().Select(c =>
                {
                    var co = c.Children().First().ToObject<CapturedObject>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }

            if (_additionalData.TryGetValue("locals", out var locals))
            {
                Locals = locals.Children<JProperty>().Select(c =>
                {
                    var co = c.Children().First().ToObject<CapturedObject>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }
        }
    }
}
