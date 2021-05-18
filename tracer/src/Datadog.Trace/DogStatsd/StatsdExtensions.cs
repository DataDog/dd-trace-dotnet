// <copyright file="StatsdExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal static class StatsdExtensions
    {
        public static void Exception(this IDogStatsd statsd, Exception exception, string source, string message, string[] tags = null)
        {
            if (statsd != null)
            {
                string[] exceptionTags =
                {
                    $"source:{source}",
                    $"message:{message}",
                    $"exception-type:{exception.GetType().FullName}",
                    $"exception-message:{exception.Message}",
                };

                string[] allTags = exceptionTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

                statsd.Counter(TracerMetricNames.Health.Exceptions, value: 1, sampleRate: 1, allTags);
            }
        }

        public static void Warning(this IDogStatsd statsd, string source, string message, string[] tags = null)
        {
            if (statsd != null)
            {
                string[] warningTags =
                {
                    $"source:{source}",
                    $"message:{message}"
                };

                string[] allTags = warningTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

                statsd.Counter(TracerMetricNames.Health.Warnings, value: 1, sampleRate: 1, allTags);
            }
        }
    }
}
