// <copyright file="ErrorSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class ErrorSampler : ITraceChunkSampler
    {
        public bool Sample(in SpanCollection trace)
        {
            foreach (var span in trace)
            {
                if (span.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
