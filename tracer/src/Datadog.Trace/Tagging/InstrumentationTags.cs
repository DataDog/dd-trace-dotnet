// <copyright file="InstrumentationTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal abstract partial class InstrumentationTags : CommonTags
    {
        public abstract string SpanKind { get; }

        [Metric(Trace.Tags.Analytics)]
        public double? AnalyticsSampleRate { get; set; }

        public void SetAnalyticsSampleRate(IntegrationId integration, ImmutableTracerSettings settings, bool enabledWithGlobalSetting)
        {
            if (settings != null)
            {
#pragma warning disable 618 // App analytics is deprecated, but still used
                AnalyticsSampleRate = settings.GetIntegrationAnalyticsSampleRate(integration, enabledWithGlobalSetting);
#pragma warning restore 618
            }
        }
    }
}
