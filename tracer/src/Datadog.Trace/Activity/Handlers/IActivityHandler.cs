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

        void ActivityStartedBasic<T>(string sourceName, T activity)
            where T : IActivity;

        void ActivityStoppedBasic<T>(string sourceName, T activity)
            where T : IActivity;

        void ActivityStartedW3C<T>(string sourceName, T activity)
            where T : IW3CActivity;

        void ActivityStoppedW3C<T>(string sourceName, T activity)
            where T : IW3CActivity;

        void ActivityStarted5<T>(string sourceName, T activity)
            where T : IActivity5;

        void ActivityStopped5<T>(string sourceName, T activity)
            where T : IActivity5;

        void ActivityStarted6<T>(string sourceName, T activity)
            where T : IActivity6;

        void ActivityStopped6<T>(string sourceName, T activity)
            where T : IActivity6;
    }
}
