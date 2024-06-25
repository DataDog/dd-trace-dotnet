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
        public Result(DdwafResultStruct returnStruct, WafReturnCode returnCode, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings, bool isRasp = false)
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
                if (Actions.TryGetValue(BlockingAction.BlockRequestType, out var value))
                {
                    BlockInfo = value as Dictionary<string, object?>;
                    ShouldBlock = true;
                }

                if (Actions.TryGetValue(BlockingAction.RedirectRequestType, out value))
                {
                    RedirectInfo = value as Dictionary<string, object?>;
                    ShouldBlock = true;
                }

                if (Actions.TryGetValue(BlockingAction.GenerateStackType, out value))
                {
                    SendStackInfo = value as Dictionary<string, object?>;
                }
            }

            if (isRasp)
            {
                AggregatedTotalRuntimeRasp = aggregatedTotalRuntime;
                AggregatedTotalRuntimeWithBindingsRasp = aggregatedTotalRuntimeWithBindings;
            }
            else
            {
                AggregatedTotalRuntime = aggregatedTotalRuntime;
                AggregatedTotalRuntimeWithBindings = aggregatedTotalRuntimeWithBindings;
            }

            Timeout = returnStruct.Timeout > 0;
        }

        public WafReturnCode ReturnCode { get; }

        public bool ShouldReportSchema { get; }

        public IReadOnlyCollection<object>? Data { get; }

        public Dictionary<string, object?>? Actions { get; }

        public Dictionary<string, object?> Derivatives { get; }

        /// <summary>
        /// Gets the total runtime in nanoseconds
        /// </summary>
        public ulong AggregatedTotalRuntime { get; }

        /// <summary>
        /// Gets the total runtime in nanoseconds with parameter passing to the waf
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindings { get; }

        /// <summary>
        /// Gets the total runtime in nanoseconds for RASP calls
        /// </summary>
        public ulong AggregatedTotalRuntimeRasp { get; }

        /// <summary>
        /// Gets the total runtime in nanoseconds with parameter passing to the waf for RASP calls
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindingsRasp { get; }

        /// <summary>
        /// Gets the number of times that a rule type is evaluated in RASP
        /// </summary>
        public uint RaspRuleEvaluations { get; }

        public bool ShouldBlock { get; }

        public Dictionary<string, object?>? BlockInfo { get; }

        public Dictionary<string, object?>? RedirectInfo { get; }

        public Dictionary<string, object?>? SendStackInfo { get; }

        public bool ShouldReportSecurityResult { get; }

        public bool Timeout { get; }
    }
}
