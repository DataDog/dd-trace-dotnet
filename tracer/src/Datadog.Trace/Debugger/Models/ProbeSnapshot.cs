// <copyright file="ProbeSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger
{
    internal record struct ProbeSnapshot
    {
        public ProbeMethodCapture Captures { get; set; }

        public ProbeInfo Probe { get; set; }

        public ThreadInfo Thread { get; set; }

        public StackInfo[] Stack { get; set; }

        public string Id { get; set; }

        public long Timestamp { get; set; }

        public string Duration { get; set; }

        public string Language { get; set; }
    }

    internal record struct ProbeInfo
    {
        public string Id { get; set; }

        public ProbeLocation Location { get; set; }
    }

    internal record struct ThreadInfo
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    internal record struct StackInfo
    {
        public string Function { get; set; }

        public string FileName { get; set; }

        public int LineNumber { get; set; }
    }

    internal record struct ProbeLocation
    {
        public string Method { get; set; }

        public string Type { get; set; }

        public string File { get; set; }

        public string[] Lines { get; set; }
    }

    internal record struct ProbeMethodCapture
    {
        public CapturedContext Entry { get; set; }

        public CapturedContext Return { get; set; }

        public CapturedLines Lines { get; set; }
    }

    internal record struct Throwable
    {
        public string Message { get; set; }

        public string Type { get; set; }

        public StackInfo[] Stacktrace { get; set; }
    }

    internal record CapturedValue : IComparable<CapturedValue>
    {
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        public string Name { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

        [JsonIgnore]
        public CapturedValue[] Fields { get; set; }

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
                        return new CapturedValue { Name = c.Name, Value = jsonObject.ToString() };
                    }

                    var co = jsonObject.ToObject<CapturedValue>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }
        }

        public int CompareTo(CapturedValue other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }

    internal record CapturedLines
    {
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        public CapturedContext Captured { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Captured = _additionalData.FirstOrDefault().Value.ToObject<CapturedContext>();
        }
    }

    internal record CapturedContext
    {
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        [JsonIgnore]
        public CapturedValue[] Fields { get; set; }

        [JsonIgnore]
        public CapturedValue[] Arguments { get; set; }

        [JsonIgnore]
        public CapturedValue[] Locals { get; set; }

        public Throwable Throwable { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_additionalData.TryGetValue("arguments", out var arguments))
            {
                Arguments = arguments.Children<JProperty>().Select(c =>
                {
                    var co = c.Children().First().ToObject<CapturedValue>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }

            if (_additionalData.TryGetValue("locals", out var locals))
            {
                Locals = locals.Children<JProperty>().Select(c =>
                {
                    var co = c.Children().First().ToObject<CapturedValue>();
                    co.Name = c.Name;
                    return co;
                }).ToArray();
            }
        }
    }
}
