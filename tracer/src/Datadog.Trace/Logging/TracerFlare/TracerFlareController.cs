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
    private readonly TracerFlareApi _flareApi;
    private ISubscription? _subscription;
    private Timer? _resetTimer = null;

    public TracerFlareController(
        IDiscoveryService discoveryService,
        IRcmSubscriptionManager subscriptionManager,
        TracerFlareApi flareApi)
    {
        _subscriptionManager = subscriptionManager;
        _flareApi = flareApi;
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

    private IEnumerable<ApplyDetails> RcmProductReceived(
        Dictionary<string, List<RemoteConfiguration>> configByProduct,
        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
    {
        if (configByProduct.TryGetValue(RcmProducts.TracerFlareInitiated, out var initiatedConfig)
         && initiatedConfig.Count > 0)
        {
            return HandleTracerFlareInitiated(initiatedConfig);
        }

        if (removedConfigByProduct?.TryGetValue(RcmProducts.TracerFlareInitiated, out var removedConfig) == true
         && removedConfig.Count > 0)
        {
            return HandleTracerFlareResolved(removedConfig);
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

    private ApplyDetails[] HandleTracerFlareResolved(List<RemoteConfigurationPath> config)
    {
        try
        {
            // This product means "tracer flare is over, revert log levels"
            ResetDebugging();
            var timer = Interlocked.Exchange(ref _resetTimer, null);
            timer?.Dispose();

            Log.Debug("Disabled debug mode due to tracer flare complete");

            // TODO: I don't know if we need to "accept" removed config?
            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromOk(config[i].Path);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare complete");

            // TODO: I don't know if we need to "accept" removed config?
            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < config.Count; i++)
            {
                result[i] = ApplyDetails.FromError(config[i].Path, ex.ToString());
            }

            return result;
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

            // TODO: do we care about the case where we have multiple configs with the same case_id?
            // Assuming not, and we'll just get two submissions in that case
            var result = new ApplyDetails[config.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var remoteConfig = config[i];
                result[i] = TrySendDebugLogs(remoteConfig, fileLogDirectory);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare request");

            return ErrorAll(config, ex);
        }
    }

    // internal for testing
    internal ApplyDetails TrySendDebugLogs(RemoteConfiguration config, string fileLogDirectory)
    {
        // TODO: I have no idea what I'm doing here. This almost certainly isn't correct
        // TODO: Look for the "task_type": "tracer_flare" key, reject config if it doesn't have that
        // TODO: Look for the "args" { "case_id": "12345"} key (I think?)
        try
        {
            var jObject = TryDeserialize(config);
            if (jObject is null
             || !jObject.TryGetValue("task_type", StringComparison.Ordinal, out var taskTypeToken)
             || taskTypeToken.Type != JTokenType.String
             || !string.Equals(taskTypeToken.Value<string>(), "tracer_flare", StringComparison.Ordinal))
            {
                // not the right sort of task, just acknowledge it
                return ApplyDetails.FromOk(config.Path.Path);
            }

            if (!jObject.TryGetValue("args", StringComparison.Ordinal, out var args)
             || args is not JObject argsObject
             || argsObject.TryGetValue("case_id", out var caseIdToken)
             || caseIdToken?.Type != JTokenType.String
             || caseIdToken.Value<string>() is not { Length: > 0 } caseId)
            {
                // missing case_id - should we just accept it?
                return ApplyDetails.FromError(config.Path.Path, "Missing case_id");
            }

            if (!DebugLogReader.TryToCreateSentinelFile(fileLogDirectory, fileLogDirectory))
            {
                // already accepted by a different tracer
                return ApplyDetails.FromOk(config.Path.Path);
            }

            // ok, do the thing
            var task = _flareApi.SendTracerFlare(stream => DebugLogReader.WriteDebugLogArchiveToStream(stream, fileLogDirectory), caseId);

            // uh oh, sync over async :grimace:
            // and this all happens inside a lock IIRC, so this could be problematic...
            var result = task.GetAwaiter().GetResult();
            return result
                       ? ApplyDetails.FromOk(config.Path.Path)
                       : ApplyDetails.FromError(config.Path.Path, "There was an error uploading the debug log archive");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare generation for config {ConfigPath}", config.Path.Path);
            return ApplyDetails.FromError(config.Path.Path, ex.ToString());
        }
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
}
