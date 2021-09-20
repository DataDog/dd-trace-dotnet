// <copyright file="InstrumentationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        private static string assemblyFullName = typeof(InstrumentationDefinitions).Assembly.FullName;

        internal static Payload GetAllDefinitions()
        {
            var definitionsArray = GetDefinitionsArray();

            return new Payload
            {
                DefinitionsId = "FFAFA5168C4F4718B40CA8788875C2DA",
                Definitions = definitionsArray,
            };
        }

        internal struct Payload
        {
            public string DefinitionsId { get; set; }

            public NativeCallTargetDefinition[] Definitions { get; set; }
        }
    }
}
