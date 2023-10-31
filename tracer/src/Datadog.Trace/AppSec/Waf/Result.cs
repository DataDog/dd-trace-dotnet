// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Result : IResult
    {
        public Result(DdwafResultStruct returnStruct, WafReturnCode returnCode, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings)
        {
            ReturnCode = returnCode;
            Actions = returnStruct.Actions.DecodeStringArray();
            ShouldReportSecurityResult = returnCode >= WafReturnCode.Match;
            Derivatives = returnStruct.Derivatives.DecodeMap();
            ShouldReportSchema = Derivatives is { Count: > 0 };
            var events = returnStruct.Events.DecodeObjectArray();
            if (events.Count == 0 || !ShouldReportSecurityResult) { Data = string.Empty; }
            else
            {
                // Serialize all the events
                Data = JsonConvert.SerializeObject(events);
            }

            ShouldBlock = Actions.Contains("block");
            AggregatedTotalRuntime = aggregatedTotalRuntime;
            AggregatedTotalRuntimeWithBindings = aggregatedTotalRuntimeWithBindings;
            Timeout = returnStruct.Timeout;
        }

        public WafReturnCode ReturnCode { get; }

        public bool ShouldReportSchema { get; }

        public string Data { get; }

        public List<string> Actions { get; }

        public Dictionary<string, object?> Derivatives { get; }

        /// <summary>
        /// Gets the total runtime in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntime { get; }

        /// <summary>
        /// Gets the total runtime in microseconds with parameter passing to the waf
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindings { get; }

        public bool ShouldBlock { get; }

        public bool ShouldReportSecurityResult { get; }

        public bool Timeout { get; }
    }
}
