// <copyright file="TracerFlareController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareController
{
    private const int RevertGlobalDebugMinutes = 20;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerFlareController>();

    private readonly IDiscoveryService _discoveryService;
    private readonly IRcmSubscriptionManager _subscriptionManager;
    private ISubscription? _subscription;
    private Timer? _resetTimer = null;

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
        if (_resetTimer is not null)
        {
            // If we have a timer, we should reset debugging now
            // otherwise we'll be permanently in debug mode
            ResetDebugging();
            _resetTimer.Dispose();
        }

        _discoveryService.RemoveSubscription(HandleConfigUpdate);

        if (_subscription is { } subscription)
        {
            _subscriptionManager.Unsubscribe(subscription);
        }

        return Task.CompletedTask;
    }

    private static void ResetDebugging() => GlobalSettings.SetDebugEnabledInternal(false);

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

    private ApplyDetails[] HandleTracerFlareInitiated(List<RemoteConfiguration> config)
    {
        try
        {
            // This product means "prepare for sending a tracer flare."
            // We may consider doing more than just enabling debug mode in the future
            GlobalSettings.SetDebugEnabledInternal(true);

            // The timer is a fallback, in case we never receive a "send flare" product
            var timer = new Timer(
                _ => ResetDebugging(),
                state: null,
                dueTime: TimeSpan.FromMinutes(RevertGlobalDebugMinutes),
                period: Timeout.InfiniteTimeSpan);

            var previous = Interlocked.Exchange(ref _resetTimer, timer);
            previous?.Dispose();

            Log.Debug("Enabling debug mode due to tracer flare initialization");

            return AcknowledgeAll(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare initialization");

            return ErrorAll(config, ex);
        }
    }

    private ApplyDetails[] HandleTracerFlareRequested(List<RemoteConfiguration> config)
    {
        try
        {
            // This product means "send the flare to the endpoint."
            // We may consider doing more than just enabling debug mode in the future
            Log.Debug("Received tracer flare request");

            if (CanSendTracerFlare != true)
            {
                Log.Debug("Ignoring tracer flare request - tracer flare endpoint is not available");
                return AcknowledgeAll(config);
            }

            if (Log.FileLogDirectory is not { } fileLogDirectory)
            {
                Log.Debug("Ignoring tracer flare request - file logging is disabled");
                return AcknowledgeAll(config);
            }

            return AcknowledgeAll(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare request");

            return ErrorAll(config, ex);
        }
    }

    private void ShouldSendDebugLogs(List<RemoteConfiguration> configs)
    {
        // // We can't
        // // We could have multiple configs here with the same config_ids, so need to account for all of them
        // Dictionary<string, List<RemoteConfiguration>>? results
        //
        //
        // // try with each of the configs until we find one that works
        // foreach (var config in configs)
        // {
        //     // TODO: I have no idea what I'm doing here. This almost certainly isn't correct
        //     // TODO: Look for the "task_type": "tracer_flare" key, reject config if it doesn't have that
        //     // TODO: Look for the "args" { "case_id": "12345"} key (I think?)
        //     var jobject = TryDeserialize(config);
        //     if (jobject is null
        //         || !jobject.TryGetValue("task_type", StringComparison.Ordinal, out var taskTypeToken)
        //         || taskTypeToken.Type != JTokenType.String
        //         || !string.Equals(taskTypeToken.Value<string>(), "tracer_flare", StringComparison.Ordinal))
        //     {
        //         // not the right sort of task
        //         continue;
        //     }
        //
        //     if (!jobject.TryGetValue("args", StringComparison.Ordinal, out var args)
        //      || args is not JObject argsObject
        //      || argsObject.TryGetValue("case_id", out var caseIdToken)
        //      || caseIdToken?.Type != JTokenType.String
        //      || caseIdToken.Value<string>() is not { Length: > 0 } caseId)
        //     {
        //         // missing case_id
        //         continue;
        //     }
        //
        //     if (DebugLogReader.TryToCreateSentinelFile(fileLogDirectory, fileLogDirectory))
        //     {
        //         return true;
        //     }
        // }
    }

    private ApplyDetails[] AcknowledgeAll(List<RemoteConfiguration> config)
    {
        var result = new ApplyDetails[config.Count];
        for (var i = 0; i < config.Count; i++)
        {
            result[i] = ApplyDetails.FromOk(config[i].Path.Path);
        }

        return result;
    }

    private ApplyDetails[] ErrorAll(List<RemoteConfiguration> config, Exception ex)
    {
        var result = new ApplyDetails[config.Count];
        for (var i = 0; i < config.Count; i++)
        {
            result[i] = ApplyDetails.FromError(config[i].Path.Path, ex.ToString());
        }

        return result;
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

    private JObject? TryDeserialize(RemoteConfiguration config)
    {
        try
        {
            using var stream = new MemoryStream(config.Contents);
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            return JObject.Load(jsonReader);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing remote configuration response");
            return null;
        }
    }

    private class TracerFlareRequest
    {
        [JsonProperty("tracer_type")]
        public string? TracerType { get; set; }

        [JsonProperty("args")]
        public Dictionary<string, object>? Args { get; set; }
    }
}
