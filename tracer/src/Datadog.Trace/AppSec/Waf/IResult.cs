// <copyright file="IResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IResult
    {
        WafReturnCode ReturnCode { get; }

        /// <summary>
        /// Gets a value indicating whether to block the request or not
        /// </summary>
        public Dictionary<string, object?>? BlockInfo { get; }

        public Dictionary<string, object?>? SendStackInfo { get; }

        IReadOnlyCollection<object>? Data { get; }

        Dictionary<string, object?>? Actions { get; }

        ulong AggregatedTotalRuntime { get; }

        ulong AggregatedTotalRuntimeWithBindings { get; }

        bool Timeout { get; }

        Dictionary<string, object?> Derivatives { get; }

        bool ShouldReportSchema { get; }

        bool ShouldReportSecurityResult { get; }
    }
}
