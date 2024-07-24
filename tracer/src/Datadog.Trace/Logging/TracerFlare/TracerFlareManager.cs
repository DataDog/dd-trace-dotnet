// <copyright file="TracerFlareManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareManager : ITracerFlareManager
{
    internal const string TracerFlareInitializationLog = "Enabling debug mode due to tracer flare initialization";
    internal const string TracerFlareCompleteLog = "Disabled debug mode due to tracer flare complete";
    internal const string ReceivedTracerFlareRequestLog = "Received tracer flare request";
    private const int RevertGlobalDebugMinutes = 20;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerFlareManager>();

    private readonly IDiscoveryService _discoveryService;
    private readonly IRcmSubscriptionManager _subscriptionManager;
    private readonly ITelemetryController _telemetryController;
    private readonly TracerFlareApi _flareApi;

    private string? _debugEnabledConfigPath;
    private ISubscription? _subscription;
    private Timer? _resetTimer = null;

    private bool _wasDebugLogEnabled;

    public TracerFlareManager(
        IDiscoveryService discoveryService,
        IRcmSubscriptionManager subscriptionManager,
        ITelemetryController telemetryController,
        TracerFlareApi flareApi)
    {
        _subscriptionManager = subscriptionManager;
        _telemetryController = telemetryController;
        _flareApi = flareApi;
        _discoveryService = discoveryService;
    }

    public bool? CanSendTracerFlare { get; private set; } = null;

    public void Start()
    {
        if (Interlocked.Exchange(ref _subscription, new Subscription(RcmProductReceived, RcmProducts.TracerFlareInitiated, RcmProducts.TracerFlareRequested)) == null)
        {
            _discoveryService.SubscribeToChanges(HandleConfigUpdate);
            // Don't need to set any capabilities for tracer flare
            _subscriptionManager.SubscribeToChanges(_subscription!);
        }
    }

    public void Dispose()
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
    }

    private void ResetDebugging()
    {
        // Restore the log level to its old value
        if (!_wasDebugLogEnabled)
        {
            _debugEnabledConfigPath = null;
            GlobalSettings.SetDebugEnabledInternal(false);
        }
    }

    private async Task<ApplyDetails[]> RcmProductReceived(
        Dictionary<string, List<RemoteConfiguration>> configByProduct,
        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
    {
        // We only expect _one_ of these to happen at a time, but handle the case where they all come together
        ApplyDetails[]? results = null;
        if (configByProduct.TryGetValue(RcmProducts.TracerFlareInitiated, out var initiatedConfig)
         && initiatedConfig.Count > 0)
        {
            results = await HandleTracerFlareInitiated(initiatedConfig).ConfigureAwait(false);
        }

        if (configByProduct.TryGetValue(RcmProducts.TracerFlareRequested, out var requestedConfig)
         && requestedConfig.Count > 0)
        {
            var handled = await HandleTracerFlareRequested(requestedConfig).ConfigureAwait(false);
            results = results is null ? handled : [..results, ..handled];
        }

        if (removedConfigByProduct?.TryGetValue(RcmProducts.TracerFlareInitiated, out var removedConfig) == true
         && removedConfig.Count > 0)
        {
            // We don't need to acknowledge config deletions
            HandleTracerFlareResolved(removedConfig);
        }

        return results ?? [];
    }

    private async Task<ApplyDetails[]> HandleTracerFlareInitiated(List<RemoteConfiguration> config)
    {
        try
        {
            var debugRequested = false;
            foreach (var remoteConfig in config)
            {
                if (IsEnableDebugConfig(remoteConfig))
                {
                    debugRequested = true;
                    _debugEnabledConfigPath = remoteConfig.Path.Path;
                    break;
                }
            }

            if (debugRequested)
            {
                // This product means "prepare for sending a tracer flare."
                // We may consider doing more than just enabling debug mode in the future
                _wasDebugLogEnabled = GlobalSettings.Instance.DebugEnabledInternal;
                GlobalSettings.SetDebugEnabledInternal(true);

                // The timer is a fallback, in case we never receive a "send flare" product
                var timer = new Timer(
                    _ => ResetDebugging(),
                    state: null,
                    dueTime: TimeSpan.FromMinutes(RevertGlobalDebugMinutes),
                    period: Timeout.InfiniteTimeSpan);

                var previous = Interlocked.Exchange(ref _resetTimer, timer);
                previous?.Dispose();

                Log.Debug(TracerFlareInitializationLog);

                // dump the telemetry (assuming we have somewhere to dump it)
                if (Log.FileLogDirectory is { } logDir)
                {
                    // the filename here is chosen so that it will get cleaned up in the normal log rotation
                    ProcessHelpers.GetCurrentProcessInformation(out _, out _, out var pid);
                    var rid = Tracer.RuntimeId;
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var telemetryPath = Path.Combine(logDir, $"dotnet-tracer-telemetry-{pid}-{rid}-{timestamp}.log");
                    Log.Debug("Requesting telemetry dump to {FileName}", telemetryPath);
                    await _telemetryController.DumpTelemetry(telemetryPath).ConfigureAwait(false);
                }
            }

            return AcknowledgeAll(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare initialization");

            return ErrorAll(config, ex);
        }
    }

    private void HandleTracerFlareResolved(List<RemoteConfigurationPath> config)
    {
        try
        {
            var enableDebugDeleted = false;

            if (_debugEnabledConfigPath != null)
            {
                foreach (var removedConfig in config)
                {
                    if (_debugEnabledConfigPath == removedConfig.Path)
                    {
                        enableDebugDeleted = true;
                        break;
                    }
                }
            }

            if (enableDebugDeleted)
            {
                // This product means "tracer flare is over, revert log levels"
                ResetDebugging();
                var timer = Interlocked.Exchange(ref _resetTimer, null);
                timer?.Dispose();

                Log.Information(TracerFlareCompleteLog);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare complete");
        }
    }

    private async Task<ApplyDetails[]> HandleTracerFlareRequested(List<RemoteConfiguration> config)
    {
        try
        {
            // This product means "send the flare to the endpoint."
            // We may consider doing more than just enabling debug mode in the future
            Log.Debug(ReceivedTracerFlareRequestLog);

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
                result[i] = await TrySendDebugLogs(remoteConfig.Path.Path, remoteConfig.Contents, remoteConfig.Path.Id, fileLogDirectory).ConfigureAwait(false);
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
    internal async Task<ApplyDetails> TrySendDebugLogs(string configPath, byte[] configContents, string configId, string fileLogDirectory)
    {
        try
        {
            // Looking for config that looks like this:
            // { "task_type": "tracer_flare", "args" { "case_id": "12345"} }
            var jObject = TryDeserialize(configContents);
            if (jObject is null)
            {
                Log.Debug("Invalid configuration provided for tracer flare");
                return ApplyDetails.FromError(configPath, "Invalid configuration provided");
            }

            if (!jObject.TryGetValue("task_type", StringComparison.Ordinal, out var taskTypeToken)
             || taskTypeToken.Type != JTokenType.String
             || !string.Equals(taskTypeToken.Value<string>(), "tracer_flare", StringComparison.Ordinal))
            {
                // not the right sort of task, just acknowledge it
                Log.Debug($"{RcmProducts.TracerFlareRequested} was not tracer flare - ignoring");
                return ApplyDetails.FromOk(configPath);
            }

            if (!jObject.TryGetValue("args", StringComparison.Ordinal, out var args)
             || args is not JObject argsObject)
            {
                // missing args
                Log.Debug("Invalid configuration provided for tracer flare - missing args");
                return ApplyDetails.FromError(configPath, "Missing args");
            }

            if (!TryGetArg(argsObject, "case_id", configPath, out var caseId, out var caseError))
            {
                return caseError.Value;
            }

            if (!TryGetArg(argsObject, "hostname", configPath, out var hostname, out var hostError))
            {
                return hostError.Value;
            }

            if (!TryGetArg(argsObject, "user_handle", configPath, out var email, out var emailError))
            {
                return emailError.Value;
            }

            if (!DebugLogReader.TryToCreateSentinelFile(fileLogDirectory, configId))
            {
                // already accepted by a different tracer
                Log.Debug("Tracer flare already handled by other tracer instance - ignoring");
                return ApplyDetails.FromOk(configPath);
            }

            // ok, do the thing
            var result = await _flareApi.SendTracerFlare(
                                             stream => DebugLogReader.WriteDebugLogArchiveToStream(stream, fileLogDirectory),
                                             caseId: caseId,
                                             email: email,
                                             hostname: hostname)
                                        .ConfigureAwait(false);

            if (result.Key)
            {
                return ApplyDetails.FromOk(configPath);
            }

            var error = string.IsNullOrEmpty(result.Value)
                            ? "There was an error uploading the debug log archive"
                            : $"There was an error uploading the debug log archive. Agent response: {result.Value}";

            return ApplyDetails.FromError(configPath, error);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling tracer flare generation for config {ConfigPath}", configPath);
            return ApplyDetails.FromError(configPath, ex.ToString());
        }

        static bool TryGetArg(JObject argsObject, string name, string configPath, [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out ApplyDetails? error)
        {
            if (argsObject.TryGetValue(name, out var token)
             && token.Type == JTokenType.String
             && token.Value<string>() is { Length: > 0 } parsedValue)
            {
                value = parsedValue;
                error = null;
                return true;
            }

            // missing required token
            Log.Debug("Invalid configuration provided for tracer flare - missing {Token}", name);
            error = ApplyDetails.FromError(configPath, "Missing " + name);
            value = null;
            return false;
        }
    }

    private static bool IsEnableDebugConfig(RemoteConfiguration remoteConfig)
    {
        try
        {
            var remoteConfigPath = remoteConfig.Path;

            if (remoteConfigPath.Id.Equals("flare-log-level.debug", StringComparison.Ordinal)
                || remoteConfigPath.Id.Equals("flare-log-level.trace", StringComparison.Ordinal))
            {
                return true;
            }

            var json = JObject.Parse(EncodingHelpers.Utf8NoBom.GetString(remoteConfig.Contents));

            var logLevel = json["config"]?["log_level"]?.Value<string>();

            return logLevel is not null
                && (logLevel.Equals("debug", StringComparison.OrdinalIgnoreCase)
                    || logLevel.Equals("trace", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Invalid configuration provided for tracer flare");
        }

        return false;
    }

    private static ApplyDetails[] AcknowledgeAll(List<RemoteConfiguration> config)
    {
        var result = new ApplyDetails[config.Count];
        for (var i = 0; i < config.Count; i++)
        {
            result[i] = ApplyDetails.FromOk(config[i].Path.Path);
        }

        return result;
    }

    private static ApplyDetails[] ErrorAll(List<RemoteConfiguration> config, Exception ex)
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

    private JObject? TryDeserialize(byte[] contents)
    {
        try
        {
            using var stream = new MemoryStream(contents);
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
