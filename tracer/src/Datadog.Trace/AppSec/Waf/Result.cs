// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Result : IResult
    {
        public Result(DdwafResultStruct returnStruct, WafReturnCode returnCode, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings)
        {
            ReturnCode = returnCode;
            Actions = returnStruct.Actions.DecodeMap();
            ShouldReportSecurityResult = returnCode >= WafReturnCode.Match;
            Derivatives = returnStruct.Derivatives.DecodeMap();
            ShouldReportSchema = Derivatives is { Count: > 0 };
            if (ShouldReportSecurityResult)
            {
                Data = returnStruct.Events.DecodeObjectArray();
            }

             if (Actions is { Count: > 0 })
            {
                var blockActionFound = Actions.TryGetValue(BlockingAction.BlockRequestType, out var value);
                if (blockActionFound)
                {
                    BlockInfo = value as Dictionary<string, object?>;
                    ShouldBlock = true;
                }
                var redirectActionFound = Actions.TryGetValue(BlockingAction.RedirectRequestType, out value);
                if (redirectActionFound )
                {
                    RedirectInfo = value as Dictionary<string, object?>;
                    ShouldBlock = true;
                }
            }

            AggregatedTotalRuntime = aggregatedTotalRuntime;
            AggregatedTotalRuntimeWithBindings = aggregatedTotalRuntimeWithBindings;
            Timeout = returnStruct.Timeout;
        }

        public WafReturnCode ReturnCode { get; }

        public bool ShouldReportSchema { get; }

        public IReadOnlyCollection<object>? Data { get; }

        public Dictionary<string, object?>? Actions { get; }

        public Dictionary<string, object?> Derivatives { get; }

        /// <summary>
        /// Gets the total runtime in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntime { get; }

        /// <summary>
        /// Gets the total runtime in microseconds with parameter passing to the waf
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindings { get; }

        public bool ShouldBlock => BlockInfo is not null || RedirectInfo is not null;

        public Dictionary<string, object?>? BlockInfo { get; }

        public Dictionary<string, object?>? RedirectInfo { get; }

        public bool ShouldReportSecurityResult { get; }

        public bool Timeout { get; }
    }
}
