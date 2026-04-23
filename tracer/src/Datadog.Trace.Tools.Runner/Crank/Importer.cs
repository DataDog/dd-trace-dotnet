// <copyright file="Importer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal class Importer
    {
        public const string RateLimit = "rate-limit";
        public const string PayloadSize = "payload-size";

        public static int Process(string jsonFilePath)
        {
            // Temporarily stubbed out for the non-recording-spans experiment.
            // The Span/Tracer APIs used here (StartSpan with serviceName, settable ResourceName)
            // were removed on this branch. Restore this body once the experiment lands or is reverted.
            AnsiConsole.WriteLine($"Crank import is disabled in this experimental build (path: {jsonFilePath}).");
            return 0;
        }
    }
}
