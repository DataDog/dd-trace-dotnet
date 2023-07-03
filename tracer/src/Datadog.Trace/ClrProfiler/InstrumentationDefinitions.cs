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

        // --> Legacy methods in order to maintain backwards compatibility with previous versions of the Native Library

        internal static string GetPayloadId(InstrumentationCategory category, CallTargetKind kind)
        {
            return $"{category}-{kind}";
        }

        internal static Payload GetLegacyDefinitions(InstrumentationCategory category = InstrumentationCategory.Tracing, CallTargetKind kind = CallTargetKind.Default)
        {
            var defs = Instrumentations.Where(i => i.HasCategory(category) && i.Kind == (byte)kind).Select(i => (NativeCallTargetDefinition)i);
            return new Payload(GetPayloadId(category, kind), defs.ToArray());
        }

        internal static Payload GetAllDefinitions(InstrumentationCategory instrumentationFilter = InstrumentationCategory.Tracing)
        {
            return GetLegacyDefinitions(instrumentationFilter, CallTargetKind.Default);
        }

        internal static Payload GetDerivedDefinitions(InstrumentationCategory instrumentationFilter = InstrumentationCategory.Tracing)
        {
            return GetLegacyDefinitions(instrumentationFilter, CallTargetKind.Derived);
        }

        internal static Payload GetInterfaceDefinitions(InstrumentationCategory instrumentationFilter = InstrumentationCategory.Tracing)
        {
            return GetLegacyDefinitions(instrumentationFilter, CallTargetKind.Interface);
        }

        internal static NativeCallTargetDefinition[] GetAllDefinitionsNative()
        {
            return Instrumentations.Where(i => i.Kind == (byte)CallTargetKind.Default).Select(i => (NativeCallTargetDefinition)i).ToArray();
        }

        internal static NativeCallTargetDefinition[] GetAllDerivedDefinitionsNative()
        {
            return Instrumentations.Where(i => i.Kind == (byte)CallTargetKind.Derived).Select(i => (NativeCallTargetDefinition)i).ToArray();
        }

        internal static NativeCallTargetDefinition[] GetAllInterfaceDefinitionsNative()
        {
            return Instrumentations.Where(i => i.Kind == (byte)CallTargetKind.Interface).Select(i => (NativeCallTargetDefinition)i).ToArray();
        }

        // <-- End of Legacy methods

        internal static TraceMethodPayload GetTraceAttributeDefinitions()
        {
            return new TraceMethodPayload
            {
                // Fixed Id for definitions payload (to avoid loading same integrations from multiple AppDomains)
                DefinitionsId = "9C6EB897BD4946D0BB492E062FB0AE67",
                AssemblyName = assemblyFullName,
                TypeName = typeof(Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations.TraceAnnotationsIntegration).FullName
            };
        }

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
            Instrumentations = null!;
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
