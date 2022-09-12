// <copyright file="MethodProbeTestDataAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Samples.Probes.Contracts
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class MethodProbeTestDataAttribute : ProbeAttributeBase
    {
        public MethodProbeTestDataAttribute(string returnTypeName = null, string[] parametersTypeName = null, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, bool useFullTypeName = true, params string[] skipOnFramework)
            : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework)
        {
            ReturnTypeName = returnTypeName;
            ParametersTypeName = parametersTypeName;
            UseFullTypeName = useFullTypeName;
        }

        public string ReturnTypeName { get; }

        public string[] ParametersTypeName { get; }

        public bool UseFullTypeName { get; }
    }
}
