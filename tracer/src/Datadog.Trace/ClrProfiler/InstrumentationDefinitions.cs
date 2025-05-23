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

        internal static TraceMethodPayload GetTraceMethodDefinitions()
        {
            return new TraceMethodPayload
            {
                // Fixed Id for definitions payload (to avoid loading same integrations from multiple AppDomains)
                DefinitionsId = "CDEF904668434E7693E99DBD91341808",
                AssemblyName = assemblyFullName,
                TypeName = typeof(Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations.TraceAnnotationsIntegration).FullName
            };
        }

        internal static void Dispose()
        {
        }

        internal struct Payload
        {
            public Payload(string id, NativeCallTargetDefinition[] defs)
            {
                DefinitionsId = id;
                Definitions = defs;
            }

            public string DefinitionsId { get; set; }

            public NativeCallTargetDefinition[] Definitions { get; set; }
        }

        internal struct TraceMethodPayload
        {
            public string DefinitionsId { get; set; }

            public string AssemblyName { get; set; }

            public string TypeName { get; set; }
        }
    }
}
