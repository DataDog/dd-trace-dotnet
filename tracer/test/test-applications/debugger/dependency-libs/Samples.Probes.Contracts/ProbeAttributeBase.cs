// <copyright file="ProbeAttributeBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Samples.Probes.Contracts
{
    public class ProbeAttributeBase : Attribute
    {
        public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks)
        {
            Skip = skip;
            Phase = phase;
            SkipOnFrameworks = skipOnFrameworks;
            Unlisted = unlisted;
            ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
        }

        public bool Skip { get; }

        public int Phase { get; }

        public string[] SkipOnFrameworks { get; }

        public bool Unlisted { get; }

        public int ExpectedNumberOfSnapshots { get; }
    }
}
