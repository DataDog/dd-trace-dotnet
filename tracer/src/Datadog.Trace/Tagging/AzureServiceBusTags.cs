// <copyright file="AzureServiceBusTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    // Re-implement InstrumentationTags so the built-in Activity can copy over SpanKind
    // without being limited by our tags infrastructure
    internal partial class AzureServiceBusTags : CommonTags
    {
        [Metric(Trace.Tags.Analytics)]
        public double? AnalyticsSampleRate { get; set; }

        [Tag(Trace.Tags.NetworkPeerName)]
        public string NetworkPeerName { get; set; }

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

    internal partial class AzureServiceBusV1Tags : AzureServiceBusTags
    {
        private string _peerServiceOverride = null;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? NetworkPeerName;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : Tags.NetworkPeerName;
            }
        }
    }
}
