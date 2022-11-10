// <copyright file="NullOkResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Waf
{
    internal class NullOkResult : IResult
    {
        public NullOkResult()
        {
            ReturnCode = ReturnCode.Ok;
            Block = false;
            ShouldBeReported = false;
            Data = string.Empty;
        }

        public ReturnCode ReturnCode { get; }

        public bool Block { get; }

        public bool ShouldBeReported { get; }

        public string Data { get; }

        public IList<string> Actions { get; }

        public ulong AggregatedTotalRuntime { get; }

        public ulong AggregatedTotalRuntimeWithBindings { get; }

        public void Dispose()
        {
        }
    }
}
