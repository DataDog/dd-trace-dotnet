// <copyright file="IActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity.Handlers
{
    internal interface IActivityHandler
    {
        bool ShouldListenTo(string sourceName, string version);

        void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity;

        void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity;
    }
}
