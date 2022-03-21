// <copyright file="InstrumentationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        private static string assemblyFullName = typeof(InstrumentationDefinitions).Assembly.FullName;

        internal static Payload GetAllDefinitions(InstrumentationFilter instrumentationFilter = InstrumentationFilter.NoFilter)
        {
            return GetDefinitionsArray(instrumentationFilter);
        }

        internal static Payload GetDerivedDefinitions(InstrumentationFilter instrumentationFilter = InstrumentationFilter.NoFilter)
        {
            return GetDerivedDefinitionsArray(instrumentationFilter);
        }

        internal static NativeCallTargetDefinition[] GetAllDefinitionsNative()
        {
            return InstrumentationsNatives.ToArray();
        }

        internal static NativeCallTargetDefinition[] GetAllDerivedDefinitionsNative()
        {
            return DerivedInstrumentationsNatives.ToArray();
        }

        internal struct Payload
        {
            public string DefinitionsId { get; set; }

            public NativeCallTargetDefinition[] Definitions { get; set; }
        }
    }
}
