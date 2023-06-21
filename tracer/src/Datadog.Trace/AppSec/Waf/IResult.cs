// <copyright file="IResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IResult
    {
        ReturnCode ReturnCode { get; }

        /// <summary>
        /// Gets a value indicating whether to block the request or not
        /// </summary>
        public bool ShouldBlock { get; }

        public bool ShouldBeReported { get; }

        string Data { get; }

        List<string> Actions { get; }

        ulong AggregatedTotalRuntime { get; }

        ulong AggregatedTotalRuntimeWithBindings { get; }

        bool Timeout { get; }
    }
}
