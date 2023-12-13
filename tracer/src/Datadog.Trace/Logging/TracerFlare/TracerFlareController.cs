// <copyright file="TracerFlareController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareController
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerFlareController>();

    private readonly IDiscoveryService _discoveryService;
    private readonly IRcmSubscriptionManager _subscriptionManager;
    private ISubscription? _subscription;

    public TracerFlareController(
        IDiscoveryService discoveryService,
        IRcmSubscriptionManager subscriptionManager)
    {
        _subscriptionManager = subscriptionManager;
        _discoveryService = discoveryService;
    }

    public bool? CanSendTracerFlare { get; private set; } = null;

    public void Start()
    {
        if (Interlocked.Exchange(ref _subscription, new Subscription(RcmProductReceived, RcmProducts.TracerFlareInitiated, RcmProducts.TracerFlareRequested)) == null)
        {
            // TODO: do we need to set capabilities?
            _subscriptionManager.SubscribeToChanges(_subscription!);
        }
    }

    public Task DisposeAsync()
    {
        _discoveryService.RemoveSubscription(HandleConfigUpdate);

        if (_subscription is { } subscription)
        {
            _subscriptionManager.Unsubscribe(subscription);
        }

        return Task.CompletedTask;
    }

    private static ApplyDetails[] HandleTracerFlareInitiated(List<RemoteConfiguration> config)
    {
        try
        {
            // This product means "prepare for sending a tracer flare."
            // We may consider doing more than just enabling debug mode in the future
            // The timer is a fallback, in case we never receive a "send flare" product
            GlobalSettings.SetDebugEnabledInternal(true);
            Log.Debug("Enabling debug mode due to tracer flare initialization");

            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromOk(config[i].Path.Path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare initialization");

            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromError(config[i].Path.Path, ex.ToString());
            }

            return result;
        }

        throw new NotImplementedException();
    }

    private static ApplyDetails[] HandleTracerFlareRequested(List<RemoteConfiguration> config)
    {
        try
        {
            // This product means "send the flare to the endpoint."
            // We may consider doing more than just enabling debug mode in the future
            // The timer is a fallback, in case we never receive a "send flare" product
            GlobalSettings.SetDebugEnabledInternal(true);
            Log.Debug("Enabling debug mode due to tracer flare initialization");

            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromOk(config[i].Path.Path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare initialization");

            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromError(config[i].Path.Path, ex.ToString());
            }

            return result;
        }

        throw new NotImplementedException();
    }

    private IEnumerable<ApplyDetails> RcmProductReceived(Dictionary<string, List<RemoteConfiguration>> configByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
    {
        if (configByProduct.TryGetValue(RcmProducts.TracerFlareInitiated, out var initiatedConfig)
            && initiatedConfig.Count > 0)
        {
            return HandleTracerFlareInitiated(initiatedConfig);
        }

        if (configByProduct.TryGetValue(RcmProducts.TracerFlareRequested, out var requestedConfig)
            && requestedConfig.Count > 0)
        {
            return HandleTracerFlareRequested(requestedConfig);
        }

        return Enumerable.Empty<ApplyDetails>();
    }

    private void HandleConfigUpdate(AgentConfiguration config)
    {
        CanSendTracerFlare = !string.IsNullOrWhiteSpace(config.TracerFlareEndpoint);

        if (CanSendTracerFlare.Value)
        {
            Log.Debug("Tracer flare endpoint available");
        }
        else
        {
            Log.Debug("Tracer flare endpoint is not available");
        }
    }
}
