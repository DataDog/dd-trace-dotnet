// <copyright file="RejectingAdaptiveSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class RejectingAdaptiveSampler : IAdaptiveSampler
    {
        internal static readonly RejectingAdaptiveSampler Instance = new();

        public bool Sample()
        {
            return false;
        }

        public bool Keep()
        {
            return false;
        }

        public bool Drop()
        {
            return false;
        }

        public double NextDouble()
        {
            return 1.0;
        }

        public void Dispose()
        {
        }
    }
}
