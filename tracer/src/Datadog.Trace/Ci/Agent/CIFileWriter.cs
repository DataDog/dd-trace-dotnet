// <copyright file="CIFileWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Agent
{
    internal class CIFileWriter : CIWriter
    {
        private static JsonConverter[] _converters = new JsonConverter[] { new SpanJsonConverter(), new TraceJsonConverter() };

        public CIFileWriter(ImmutableTracerSettings settings, ISampler sampler)
            : base(settings, sampler)
        {
            Log.Information("CIFileWriter Initialized.");
        }

        public override Task<bool> Ping()
        {
            return Task.FromResult(true);
        }

        protected override Task SendEvents(IEnumerable<IEvent> events)
        {
            var str = $"c:\\temp\\file-{Guid.NewGuid().ToString("n")}.json";
            var json = JsonConvert.SerializeObject(events, Formatting.Indented, _converters);
            File.WriteAllText(str, json);
            return Task.CompletedTask;
        }
    }
}
