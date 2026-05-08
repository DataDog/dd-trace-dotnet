// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
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
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _sendRequestMutex = new(1, 1);

    /// <summary>
    /// Key is the path
    /// </summary>
    private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();

    private readonly string _id = Guid.NewGuid().ToString();

    // Persistent request object, mutated in-place each poll
    private readonly GetRcmRequest _request;

    // Ideally this would be an ImmutableArray but that's not available in net461
    private IReadOnlyList<ISubscription> _subscriptions = [];

    private long _rootVersion = 1;
    private string? _backendClientState;
    private long _targetsVersion;
    private BigInteger _capabilities;
    private string? _lastPollError;
    private long _appliedConfigsVersion;

    // Cached capabilities byte[] — only recomputed when _capabilities changes
    private BigInteger _cachedCapabilities;
    private byte[]? _cachedCapabilitiesBytes;

    // Version tracking for lists that are expensive to rebuild
    private long _requestAppliedConfigsVersion = -1;

    public RcmSubscriptionManager()
    {
        _request = new GetRcmRequest(new RcmClient(_id, new RcmClientState()));
    }

    public bool HasAnySubscription => _subscriptions.Count > 0;

    // this list shouldn't be recalculated everytime we access it as it is used by RemoteConfigurationManager to build an rcm request every x seconds
    public ICollection<string> ProductKeys { get; private set; } = [];

    public void SubscribeToChanges(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.Contains(subscription))
            {
                _subscriptions = [.. _subscriptions, subscription];
            }

            RefreshProductKeys();
        }
    }

    public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
    {
        lock (_syncRoot)
        {
            var newSubscriptions = new List<ISubscription>(_subscriptions.Count);

            bool found = false;

            foreach (var subscription in _subscriptions)
            {
                var subscriptionToAdd = subscription == oldSubscription ? newSubscription : subscription;

                if (subscriptionToAdd == newSubscription)
                {
                    found = true;
                }

                newSubscriptions.Add(subscriptionToAdd);
            }

            if (!found)
            {
                newSubscriptions.Add(newSubscription);
            }

            _subscriptions = newSubscriptions;

            RefreshProductKeys();
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions = new List<ISubscription>(_subscriptions.Where(s => s != subscription));
            RefreshProductKeys();
        }
    }

    /// <summary>
    /// Called by RCM
    /// </summary>
    private async Task<List<ApplyDetails>?> Update(Dictionary<string, List<RemoteConfiguration>>? configByProducts, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
    {
        List<ApplyDetails>? results = null;

        var subscriptions = Volatile.Read(ref _subscriptions);

        foreach (var subscription in subscriptions)
        {
            Dictionary<string, List<RemoteConfiguration>>? filteredConfigs = null;
            if (configByProducts is not null)
            {
                foreach (var productKey in subscription.ProductKeys)
                {
                    if (configByProducts.TryGetValue(productKey, out var configs))
                    {
                        filteredConfigs ??= new(subscription.ProductKeys.Count);
                        filteredConfigs[productKey] = configs;
                    }
                }
            }

            var haveChanges = filteredConfigs?.Count > 0 || removedConfigsByProduct?.Count > 0;
            if (!haveChanges)
            {
                continue;
            }

            try
            {
                results ??= new();
                results.AddRange(await subscription.Invoke(filteredConfigs ?? [], removedConfigsByProduct).ConfigureAwait(false));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to apply remote configurations for products {ProductKeys}", string.Join(", ", subscription.ProductKeys));
            }
        }

        return results;
    }

    public void SetCapability(BigInteger index, bool available)
    {
        if (available)
        {
            _capabilities |= index;
        }
        else
        {
            _capabilities &= ~index;
        }
    }

    public byte[] GetCapabilities()
    {
        // capabilitiesArray needs to be big endian
#if NETCOREAPP
        // isUnsigned: true avoids the extra 0x00 byte in some values, no need to remove it.
        // isBigEndian: true returns the bytes in big-endian order, no need to reverse them.
        return _capabilities.ToByteArray(isUnsigned: true, isBigEndian: true);
#else
        var bytes = _capabilities.ToByteArray();

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

    public async Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback)
    {
        await _sendRequestMutex.WaitAsync().ConfigureAwait(false);

        try
        {
            var response = await TrySendRequest(rcmTracer, callback).ConfigureAwait(false);
            if (response is null)
            {
                return;
            }

            UpdateRootVersionFromResponseRoots(response.Roots);

            if (response.Targets?.Signed != null)
            {
                await ProcessResponse(response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // this is a processing issue, not a communication issue
            // so we should report the error to the agent next time
            Log.Error(ex, "Error processing RCM request");
            _lastPollError = ex.Message;
        }
        finally
        {
            _sendRequestMutex.Release();
        }
    }

    private GetRcmRequest BuildRequest(RcmClientTracer rcmTracer, string? lastPollError)
    {
        // Update all fields in-place instead of recreating everything
        var requestClient = _request.Client;
        var requestState = requestClient.State;
        requestState.RootVersion = _rootVersion;
        requestState.TargetsVersion = _targetsVersion;
        requestState.HasError = lastPollError is not null;
        requestState.Error = lastPollError;
        requestState.BackendClientState = _backendClientState;

        requestClient.ClientTracer = rcmTracer;
        requestClient.Products = ProductKeys;
        requestClient.Capabilities = GetCachedCapabilities();

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

        byte[] GetCachedCapabilities()
        {
            if (_cachedCapabilitiesBytes is not null && _capabilities == _cachedCapabilities)
            {
                return _cachedCapabilitiesBytes;
            }

            _cachedCapabilities = _capabilities;
            _cachedCapabilitiesBytes = GetCapabilities();
            return _cachedCapabilitiesBytes;
        }
    }

    private async Task<GetRcmResponse?> TrySendRequest(RcmClientTracer rcmClientTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> func)
    {
        try
        {
            var request = BuildRequest(rcmClientTracer, _lastPollError);
            _lastPollError = null;

            return await func(request).ConfigureAwait(false);
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

    private async Task ProcessResponse(GetRcmResponse response)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var description = response.TargetFiles?.Count > 0
                                  ? "with the following paths: " + string.Join(",", response.TargetFiles.Select(t => t.Path))
                                  : "that is empty.";
            Log.Debug("Received Remote Configuration response {ResponseDescription}.", description);
        }

        var signed = response.Targets?.Signed?.Targets;

        Dictionary<string, List<RemoteConfiguration>>? configByProducts = null;
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
                    return; // keep compiler happy
                }

                if (!ProductKeys.Contains(remoteConfigurationPath.Product))
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

                var remoteConfiguration = new RemoteConfiguration(remoteConfigurationPath, targetFile.Raw, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom?.V ?? 0);
                configByProducts ??= [];
                if (!configByProducts.TryGetValue(remoteConfigurationPath.Product, out var configByProduct))
                {
                    configByProduct ??= [];
                    configByProducts[remoteConfigurationPath.Product] = configByProduct;
                }

                configByProduct.Add(remoteConfiguration);
            }
        }

        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct = null;

        // handle removed configurations
        foreach (var appliedConfiguration in _appliedConfigurations)
        {
            if (receivedPaths is not null && receivedPaths.Contains(appliedConfiguration.Key))
            {
                continue;
            }

            removedConfigsByProduct ??= [];
            if (!removedConfigsByProduct.TryGetValue(appliedConfiguration.Value.Path.Product, out var removedConfig))
            {
                removedConfig ??= [];
                removedConfigsByProduct[appliedConfiguration.Value.Path.Product] = removedConfig;
            }

            removedConfig.Add(appliedConfiguration.Value.Path);
        }

        // update applied configurations after removal
        if (removedConfigsByProduct is not null)
        {
            foreach (var removedConfig in removedConfigsByProduct.Values)
            {
                foreach (var value in removedConfig)
                {
                    _appliedConfigurations.Remove(value.Path);
                    _appliedConfigsVersion++;
                }
            }
        }

        var results = await Update(configByProducts, removedConfigsByProduct).ConfigureAwait(false);

        if (results is not null)
        {
            foreach (var result in results)
            {
                switch (result.ApplyState)
                {
                    case ApplyStates.UNACKNOWLEDGED:
                        // Do nothing
                        break;
                    case ApplyStates.ACKNOWLEDGED:
                        _appliedConfigurations[result.Filename].Applied();
                        _appliedConfigsVersion++;
                        break;
                    case ApplyStates.ERROR:
                        _appliedConfigurations[result.Filename].ErrorOccured(result.Error);
                        _appliedConfigsVersion++;
                        break;
                    default:
                        Log.Warning("Unexpected ApplyState: {ApplyState}", result.ApplyState);
                        break;
                }
            }
        }

        _targetsVersion = response.Targets?.Signed?.Version ?? 0;
        _backendClientState = response.Targets?.Signed?.Custom?.OpaqueBackendState;

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

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
