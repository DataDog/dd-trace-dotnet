// <copyright file="IActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity.Handlers
{
    internal interface IActivityHandler
    {
        bool ShouldListenTo(string sourceName, string? version);

        // All shapes (start handling only needs the base IActivity members)
        void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity;

        // ActivityListener path (DiagnosticSource 5.0+)
        void ActivityStopped<T>(string sourceName, T activity, in ActivityStatus status)
            where T : IActivity5;

        // DiagnosticListener path (DiagnosticSource [4.0.4, 5.0))
        void LegacyActivityStopped<T>(string sourceName, T activity)
            where T : IActivity;
    }
}
