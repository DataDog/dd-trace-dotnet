// <copyright file="DisableActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity.Handlers
{
    internal class DisableActivityHandler : IActivityHandler
    {
        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            // do nothing
            throw new InvalidOperationException("Should not happen");
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // do nothing
            throw new InvalidOperationException("Should not happen");
        }

        public bool ShouldListenTo(string sourceName, string? version)
        {
            var toDisable = Tracer.Instance.Settings.DisabledOpenTelemetryIntegrations;
            if (toDisable is null || string.IsNullOrWhiteSpace(toDisable))
            {
                return false;
            }

            var toDisableSplit = toDisable.Split(';');
            foreach (var disabledSourceName in toDisableSplit)
            {
                if (sourceName == disabledSourceName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
