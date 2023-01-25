// <copyright file="BlockException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.AppSec
{
    internal class BlockException : CallTargetBubbleUpException
    {
        internal BlockException()
        {
        }

        internal BlockException(string message)
            : base(message)
        {
        }

        internal BlockException(string message, Exception inner)
            : base(message, inner)
        {
        }

        internal BlockException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

        public BlockException(string triggerData, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings, bool reported = false)
        {
            TriggerData = triggerData;
            AggregatedTotalRuntime = aggregatedTotalRuntime;
            AggregatedTotalRuntimeWithBindings = aggregatedTotalRuntimeWithBindings;
            Reported = reported;
        }

        public string TriggerData { get; }

        public ulong AggregatedTotalRuntime { get; }

        public ulong AggregatedTotalRuntimeWithBindings { get; }

        public bool Reported { get; }
    }
}
