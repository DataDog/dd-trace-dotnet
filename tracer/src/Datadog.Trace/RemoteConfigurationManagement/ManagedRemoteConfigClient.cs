// <copyright file="ManagedRemoteConfigClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

/// <summary>
/// The managed remote configuration client, the fallback of libdatadog's <see cref="LibDatadog.RemoteConfig.NativeRemoteConfigClient"/> for one release:
/// used when libdatadog's client is not available or can't be created, or is disabled with <see cref="Configuration.ConfigurationKeys.Rcm.RemoteConfigurationNativeClientEnabled"/>.
/// It does not verify the hashes of the files. Its transport keeps the discovery service of the remote configuration manager which created it,
/// also after the tracer manager was replaced (TracerManager.ReplaceGlobalManager), which is fine while the endpoint path discovery reports doesn't change.
/// Removed with it: its key and fallback, RemoteConfigEndpoint.ExporterSettings, Transport/*, RemoteConfigurationCache, Base64DecodingStream and their
/// tests. Protocol/* and Json/*, except RcmClientTracer, move to the test helpers, Json/* decoding with Convert.FromBase64String.
/// </summary>
internal sealed class ManagedRemoteConfigClient : IRemoteConfigClient
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ManagedRemoteConfigClient>();

    /// <summary>
    /// Key is the path
    /// </summary>
    private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();

    private readonly Func<RemoteConfigEndpoint, IRemoteConfigurationApi> _apiFactory;

    // Persistent request object, mutated in-place each poll
    private readonly GetRcmRequest _request;

    private IRemoteConfigurationApi _api;
    private RcmClientTracer _target;
    private string[]? _extraServices;
    private ICollection<string> _productKeys = [];
    private byte[] _capabilities = [];
    private bool _isCancelled;

    private long _rootVersion = 1;
    private string? _backendClientState;
    private long _targetsVersion;
    private string? _lastPollError;
    private long _appliedConfigsVersion;

    // Version tracking for lists that are expensive to rebuild
    private long _requestAppliedConfigsVersion = -1;

    internal ManagedRemoteConfigClient(string clientId, RcmClientTracer target, RemoteConfigEndpoint endpoint, Func<RemoteConfigEndpoint, IRemoteConfigurationApi> apiFactory)
    {
        _request = new GetRcmRequest(new RcmClient(clientId, new RcmClientState()));
        _apiFactory = apiFactory;
        _api = apiFactory(endpoint);
        _target = target;
    }

    public static ManagedRemoteConfigClient Create(string clientId, RcmClientTracer target, RemoteConfigEndpoint endpoint, IDiscoveryService discoveryService)
        => new(clientId, target, endpoint, e => RemoteConfigurationApiFactory.Create(e.ExporterSettings ?? throw new ArgumentException("The endpoint has no exporter settings", nameof(endpoint)), discoveryService));

    public static byte[] GetCapabilities(BigInteger capabilities)
    {
        // capabilitiesArray needs to be big endian
#if NETCOREAPP
        // isUnsigned: true avoids the extra 0x00 byte in some values, no need to remove it.
        // isBigEndian: true returns the bytes in big-endian order, no need to reverse them.
        return capabilities.ToByteArray(isUnsigned: true, isBigEndian: true);
#else
        var bytes = capabilities.ToByteArray();

        if (bytes.Length > 1 && bytes[bytes.Length - 1] == 0)
        {
            // HACK: BigInteger.ToByteArray() adds a 0x00 byte at the end of the array to
            // distinguish positive numbers where the highest bit it set from negative numbers.
            // The code below drops this last byte if present.
            var unsignedBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, unsignedBytes, unsignedBytes.Length);
            bytes = unsignedBytes;
        }

        // we need a big-endian array
        Array.Reverse(bytes);
        return bytes;
#endif
    }

    public void SetProducts(ICollection<string> products, BigInteger capabilities)
    {
        _productKeys = products;
        _capabilities = GetCapabilities(capabilities);
    }

    public void SetExtraServices(string[]? services) => _extraServices = services;

    public void Rebind(RcmClientTracer target, RemoteConfigEndpoint? endpoint)
    {
        // The client state is kept across a rebind: only the next requests change
        _target = target;
        if (endpoint is not null)
        {
            _api = _apiFactory(endpoint);
        }
    }

    public RemoteConfigurationChanges Fetch()
    {
        ThrowIfCancelled();
        var response = TrySendRequest();

        // An ongoing request is not aborted, but its response is ignored
        ThrowIfCancelled();
        if (response is null)
        {
            return new([], []);
        }

        try
        {
            UpdateRootVersionFromResponseRoots(response.Roots);

            if (response.Targets?.Signed != null)
            {
                return ProcessResponse(response);
            }
        }
        catch (Exception ex)
        {
            // this is a processing issue, not a communication issue
            // so we should report the error to the agent next time
            Log.Error(ex, "Error processing RCM request");
            _lastPollError = ex.Message;
        }

        return new([], []);
    }

    public void SetApplyState(string path, ulong applyState, string? error)
    {
        if (!_appliedConfigurations.TryGetValue(path, out var appliedConfig))
        {
            // Not an applied configuration, ignored like by libdatadog's client
            Log.Debug<string>("Ignoring the apply state of {Path}, which is not an applied remote configuration", path);
            return;
        }

        switch (applyState)
        {
            case ApplyStates.UNACKNOWLEDGED:
                // Do nothing
                break;
            case ApplyStates.ACKNOWLEDGED:
                appliedConfig.Applied();
                _appliedConfigsVersion++;
                break;
            case ApplyStates.ERROR:
                appliedConfig.ErrorOccured(error);
                _appliedConfigsVersion++;
                break;
            default:
                Log.Warning("Unexpected ApplyState: {ApplyState}", applyState);
                break;
        }
    }

    public void Cancel() => Volatile.Write(ref _isCancelled, true);

    public void Dispose() => Cancel();

    private void ThrowIfCancelled()
    {
        if (Volatile.Read(ref _isCancelled))
        {
            throw new OperationCanceledException("The remote configuration client was cancelled");
        }
    }

    private GetRcmRequest BuildRequest(string? lastPollError)
    {
        // Update all fields in-place instead of recreating everything
        var requestClient = _request.Client;
        var requestState = requestClient.State;
        requestState.RootVersion = _rootVersion;
        requestState.TargetsVersion = _targetsVersion;
        requestState.HasError = lastPollError is not null;
        requestState.Error = lastPollError;
        requestState.BackendClientState = _backendClientState;

        // The protocol sends the extra services on the target
        _target.ExtraServices = _extraServices;
        requestClient.ClientTracer = _target;
        requestClient.Products = _productKeys;
        requestClient.Capabilities = _capabilities;

        // Rebuild config-derived lists only when applied configs have changed
        if (_requestAppliedConfigsVersion != _appliedConfigsVersion)
        {
            _requestAppliedConfigsVersion = _appliedConfigsVersion;
            var cachedTargetFiles = new List<RcmCachedTargetFile>(_appliedConfigurations.Values.Count);
            var configStates = new List<RcmConfigState>(_appliedConfigurations.Values.Count);

            foreach (var cache in _appliedConfigurations.Values)
            {
                cachedTargetFiles.Add(new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList()));
                configStates.Add(new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product, cache.ApplyState, cache.Error));
            }

            _request.CachedTargetFiles = cachedTargetFiles;
            requestState.ConfigStates = configStates;
        }

        return _request;
    }

    private GetRcmResponse? TrySendRequest()
    {
        try
        {
            var request = BuildRequest(_lastPollError);
            _lastPollError = null;

            // Fetch runs on the dedicated polling thread, which has no synchronization context
            return _api.GetConfigs(request).GetAwaiter().GetResult();
        }
        catch (RemoteConfigurationDeserializationException e)
        {
            Log.Error(e, "Error sending request to RCM endpoint: serialization error");
            // serialization errors should be reported to the agent
            // but we want the _inner_ message
            _lastPollError = $"{e.Message}: {e.InnerException?.Message}";
            return null;
        }
        catch (Exception e)
        {
            // Other errors when sending requests to the agent should not be reported
            // Which means anything that is not a deserialization error
            Log.Warning(e, "Error sending request to RCM endpoint");
            return null;
        }
    }

    private RemoteConfigurationChanges ProcessResponse(GetRcmResponse response)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var description = response.TargetFiles?.Count > 0
                                  ? "with the following paths: " + string.Join(",", response.TargetFiles.Select(t => t.Path))
                                  : "that is empty.";
            Log.Debug("Received Remote Configuration response {ResponseDescription}.", description);
        }

        var signed = response.Targets?.Signed?.Targets;

        var updated = new List<RemoteConfiguration>();
        List<string>? receivedPaths = null;
        if (response.ClientConfigs?.Count > 0)
        {
            receivedPaths = new List<string>(capacity: response.ClientConfigs.Count);

            // handle new configurations
            foreach (var clientConfig in response.ClientConfigs)
            {
                var remoteConfigurationPath = RemoteConfigurationPath.FromPath(clientConfig);
                receivedPaths.Add(remoteConfigurationPath.Path);

                if (signed is null || !signed.TryGetValue(remoteConfigurationPath.Path, out var signedTarget))
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in targets");
                    return null!; // keep compiler happy
                }

                if (!_productKeys.Contains(remoteConfigurationPath.Product))
                {
                    Log.Warning("Received config {RemoteConfigurationPath} for a product that was not requested", remoteConfigurationPath);
                    continue;
                }

                if (_appliedConfigurations.TryGetValue(remoteConfigurationPath.Path, out var appliedConfig) &&
                    IsConfigAlreadyApplied(appliedConfig, signedTarget))
                {
                    continue;
                }

                var targetFile = response.TargetFiles?.FirstOrDefault(file => file.Path == remoteConfigurationPath.Path);
                if (targetFile is null)
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in target files");
                }

                var remoteConfigurationCache = new RemoteConfigurationCache(remoteConfigurationPath, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom?.V ?? 0);
                _appliedConfigurations[remoteConfigurationCache.Path.Path] = remoteConfigurationCache;
                _appliedConfigsVersion++;

                updated.Add(new RemoteConfiguration(remoteConfigurationPath, targetFile.Raw, signedTarget.Custom?.V ?? 0));
            }
        }

        var removed = new List<RemoteConfigurationPath>();

        // handle removed configurations
        foreach (var appliedConfiguration in _appliedConfigurations)
        {
            if (receivedPaths is not null && receivedPaths.Contains(appliedConfiguration.Key))
            {
                continue;
            }

            removed.Add(appliedConfiguration.Value.Path);
        }

        // update applied configurations after removal
        foreach (var value in removed)
        {
            _appliedConfigurations.Remove(value.Path);
            _appliedConfigsVersion++;
        }

        _targetsVersion = response.Targets?.Signed?.Version ?? 0;
        _backendClientState = response.Targets?.Signed?.Custom?.OpaqueBackendState;

        return new RemoteConfigurationChanges(updated, removed);

        static bool IsConfigAlreadyApplied(RemoteConfigurationCache appliedConfig, Target signedTarget)
        {
            var newHashes = signedTarget.Hashes;
            if (appliedConfig.Hashes.Count != newHashes?.Count)
            {
                return false;
            }

            foreach (var kvp in appliedConfig.Hashes)
            {
                if (!newHashes.TryGetValue(kvp.Key, out var newHash)
                 || !newHash.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private void UpdateRootVersionFromResponseRoots(List<string>? roots)
    {
        if (roots is not { Count: > 0 })
        {
            return;
        }

        // The response contains a list of Root objects with different versions
        // We don't currently do anything with these root objects, hence why we just leave
        // these un-deserialized initially. However, we need to send back the _last_
        // version from the final root object in the array, so we deserialize that one only,
        // and extract the version

        var lastRoot = roots[roots.Count - 1];
        using var stream = new Base64DecodingStream(lastRoot);
        using var streamReader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(streamReader)
        {
            ArrayPool = JsonArrayPool.Shared,
        };
        var tufRoot = new JsonSerializer().Deserialize<MinimalTufRoot>(jsonReader);

        if (tufRoot?.Signed is not null)
        {
            _rootVersion = tufRoot.Signed.Version;
        }
    }
}
