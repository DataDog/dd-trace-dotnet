// <copyright file="NopAdaptiveSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.RateLimiting
{
    internal class NopAdaptiveSampler : IAdaptiveSampler
    {
        internal static readonly NopAdaptiveSampler Instance = new();

        public bool Sample()
        {
            return true;
        }

        public bool Keep()
        {
            return true;
        }

        public bool Drop()
        {
            return true;
        }

        public double NextDouble()
        {
            return 1.0;
        }
    }
}
