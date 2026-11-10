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
        /// <param name="returnStruct">the WAF result to decode</param>
        /// <param name="returnCode">the WAF return code</param>
        /// <param name="totals">the totals of the runs of the same scope of the request, which already include this
        /// run's wall clock; the duration reported by the WAF for this run is added to them</param>
        /// <param name="isRasp">whether the run is a RASP run</param>
        /// <param name="truncated">whether the input was truncated</param>
        public Result(ref DdwafObjectStruct returnStruct, WafReturnCode returnCode, ref WafRunTotals totals, bool isRasp = false, bool truncated = false)
        {
            ReturnCode = returnCode;

            var returnValues = returnStruct.DecodeMap();
            returnValues.TryGetValue("timeout", out var timeoutObj);
            returnValues.TryGetValue("keep", out var keepObj);
            returnValues.TryGetValue("duration", out var durationObj);
            returnValues.TryGetValue("events", out var eventsObj);
            returnValues.TryGetValue("actions", out var actionsObj);
            returnValues.TryGetValue("attributes", out var attributesObj);

            if (durationObj is ulong durationNanos)
            {
                totals.AddDuration(durationNanos);
            }

            Actions = (Dictionary<string, object?>?)actionsObj;

            // Since libddwaf 2.x a run that only produces attributes or actions also returns DDWAF_MATCH,
            // so the return code alone is no longer enough: requiring events keeps this to actual rule
            // matches and stops API Security runs from being reported as security results.
            var events = eventsObj as IReadOnlyCollection<object>;
            ShouldReportSecurityResult = returnCode >= WafReturnCode.Match && events is { Count: > 0 };

            if (keepObj is bool keepValue)
            {
                HasKeep = true;
                Keep = keepValue;
            }
            else
            {
                HasKeep = false;
                Keep = ShouldReportSecurityResult;
            }

            if (attributesObj is Dictionary<string, object?> attributesValue)
            {
                BuildDerivatives(attributesValue);
            }

            if (ShouldReportSecurityResult)
            {
                Data = events;
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
                AggregatedTotalRuntimeRasp = totals.DurationMicroseconds;
                AggregatedTotalRuntimeWithBindingsRasp = totals.DurationWithBindingsMicroseconds;
            }
            else
            {
                AggregatedTotalRuntime = totals.DurationMicroseconds;
                AggregatedTotalRuntimeWithBindings = totals.DurationWithBindingsMicroseconds;
            }

            if (timeoutObj is bool timeoutValue and true)
            {
                Timeout = timeoutValue;
            }

            Truncated = truncated;
        }

        public WafReturnCode ReturnCode { get; }

        public IReadOnlyCollection<object>? Data { get; }

        public Dictionary<string, object?>? Actions { get; }

        public Dictionary<string, object?>? ExtractSchemaDerivatives { get; private set; }

        public Dictionary<string, object?>? FingerprintDerivatives { get; private set; }

        public Dictionary<string, object?>? WafSpanAttributes { get; private set; }

        public bool Keep { get; }

        public bool HasKeep { get; }

        /// <summary>
        /// Gets the total runtime of the non-RASP runs of the request so far, in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntime { get; }

        /// <summary>
        /// Gets the total runtime of the non-RASP runs of the request so far, including the bindings, in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindings { get; }

        /// <summary>
        /// Gets the total runtime of the RASP runs of the request so far, in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntimeRasp { get; }

        /// <summary>
        /// Gets the total runtime of the RASP runs of the request so far, including the bindings, in microseconds
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

        public bool Truncated { get; }

        private void BuildDerivatives(Dictionary<string, object?> derivatives)
        {
            foreach (var derivative in derivatives)
            {
                if ((derivative.Key == Tags.AppSecFpEndpoint) || (derivative.Key == Tags.AppSecFpHeader) || (derivative.Key == Tags.AppSecFpHttpNetwork) || (derivative.Key == Tags.AppSecFpSession))
                {
                    (FingerprintDerivatives ??= new Dictionary<string, object?>()).Add(derivative.Key, derivative.Value);
                }
                else if (derivative.Key.StartsWith(WafConstants.AppSecSchemaPrefix, StringComparison.Ordinal))
                {
                    (ExtractSchemaDerivatives ??= new Dictionary<string, object?>()).Add(derivative.Key, derivative.Value);
                }
                else
                {
                    (WafSpanAttributes ??= new Dictionary<string, object?>()).Add(derivative.Key, derivative.Value);
                }
            }
        }
    }
}
