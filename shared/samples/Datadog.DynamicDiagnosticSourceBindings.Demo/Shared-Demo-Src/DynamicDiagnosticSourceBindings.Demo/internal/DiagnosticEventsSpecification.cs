using System;
using System.Collections.Generic;
using System.Diagnostics;

using Datadog.Util;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal static class DiagnosticEventsSpecification
    {
        public const string DirectSourceName = "DynamicDiagnosticSourceBindings.Demo.DirectDiagnosticEventsGenerator";
        public const string StubbedSourceName = "DynamicDiagnosticSourceBindings.Demo.StubbedDiagnosticEventsGenerator";

        public const string DirectSourceEventName = "Cool Stuff Happened";
        public const string StubbedSourceEventName = "Great Stuff Happened";

        public class EventPayload
        {
            public int Iteration { get; }
            public string SourceName { get; }

            public EventPayload(int iteration, string sourceName)
            {
                Iteration = iteration;
                SourceName = sourceName;
            }

            public override string ToString()
            {
                return $"{nameof(EventPayload)} {{{nameof(Iteration)}={Iteration}, {nameof(SourceName)}=\"{SourceName}\"}}";
            }
        }
    }
}
