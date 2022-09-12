// <copyright file="ErrorSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class ErrorSampler : ITraceSampler
    {
        public bool Sample(ArraySegment<Span> trace)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace.Array[i + trace.Offset].Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
