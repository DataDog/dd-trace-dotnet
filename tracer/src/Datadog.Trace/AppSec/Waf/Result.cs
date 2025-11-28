// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal sealed class Result : IResult
    {
        public Result(ref DdwafObjectStruct returnStruct, WafReturnCode returnCode, ref ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings, bool isRasp = false)
        {
            ReturnCode = returnCode;

            var returnValues = returnStruct.DecodeMap();
            returnValues.TryGetValue("timeout", out var timeoutObj);
            returnValues.TryGetValue("keep", out var keepObj);
            returnValues.TryGetValue("duration", out var durationObj);
            returnValues.TryGetValue("events", out var eventsObj);
            returnValues.TryGetValue("actions", out var actionsObj);
            returnValues.TryGetValue("attributes", out var attributesObj);

            if (durationObj is ulong durationNanos and > 0)
            {
                aggregatedTotalRuntime += durationNanos / 1000; // Convert from nanoseconds to microseconds
            }

            Actions = (Dictionary<string, object?>?)actionsObj;
            ShouldReportSecurityResult = returnCode >= WafReturnCode.Match;
            if (attributesObj is Dictionary<string, object?> attributesValue)
            {
                BuildDerivatives(attributesValue);
            }

            if (ShouldReportSecurityResult && eventsObj is IReadOnlyCollection<object> eventsValue)
            {
                Data = eventsValue;
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

            if (timeoutObj is bool timeoutValue and true)
            {
                Timeout = timeoutValue;
            }
        }

        public WafReturnCode ReturnCode { get; }

        public IReadOnlyCollection<object>? Data { get; }

        public Dictionary<string, object?>? Actions { get; }

        public Dictionary<string, object?>? ExtractSchemaDerivatives { get; private set; }

        public Dictionary<string, object?>? FingerprintDerivatives { get; private set; }

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

        private void BuildDerivatives(Dictionary<string, object?> derivatives)
        {
            foreach (var derivative in derivatives)
            {
                if ((derivative.Key == Tags.AppSecFpEndpoint) || (derivative.Key == Tags.AppSecFpHeader) || (derivative.Key == Tags.AppSecFpHttpNetwork) || (derivative.Key == Tags.AppSecFpSession))
                {
                    if (FingerprintDerivatives is null)
                    {
                        FingerprintDerivatives = new Dictionary<string, object?>();
                    }

                    FingerprintDerivatives.Add(derivative.Key, derivative.Value);
                }
                else
                {
                    if (ExtractSchemaDerivatives is null)
                    {
                        ExtractSchemaDerivatives = new Dictionary<string, object?>();
                    }

                    ExtractSchemaDerivatives.Add(derivative.Key, derivative.Value);
                }
            }
        }
    }
}
