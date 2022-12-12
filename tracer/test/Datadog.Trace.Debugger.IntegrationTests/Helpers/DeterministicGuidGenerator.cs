// <copyright file="DeterministicGuidGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.IntegrationTests.Helpers
{
    /// <summary>
    /// We want to have deterministic Guid(s) in our testings so we'll be able to assert and approve the same output.
    /// This way, we can correlate Snapshots and Probe Statuses to their corresponding Probe request.
    /// </summary>
    internal class DeterministicGuidGenerator
    {
        private readonly Random _random;

        public DeterministicGuidGenerator(int seed = 1)
        {
            _random = new Random(seed);
        }

        public Guid New()
        {
            var guid = new byte[16];
            _random.NextBytes(guid);

            return new Guid(guid);
        }
    }
}
