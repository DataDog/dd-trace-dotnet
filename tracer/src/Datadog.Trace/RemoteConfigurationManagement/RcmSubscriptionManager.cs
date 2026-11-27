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
#if SHARED_REMOTE_CONFIG
    private static readonly HashSet<string> EmptyAssignment = new(StringComparer.Ordinal);
#endif

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _sendRequestMutex = new(1, 1);

    /// <summary>
    /// Key is the path
    /// </summary>
    private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();

    // Persistent request object, mutated in-place each poll
    private GetRcmRequest _request;

    // Ideally this would be an ImmutableArray but that's not available in net461
    private IReadOnlyList<ISubscription> _subscriptions = [];
#if SHARED_REMOTE_CONFIG
    private Dictionary<object, Dictionary<string, RemoteConfigurationPath>>? _deliveredPaths;
    private int _managedSessionRetired;
#endif

    private long _rootVersion = 1;
    private string? _backendClientState;
    private long _targetsVersion;
    private BigInteger _capabilities;
    private string? _lastPollError;
    private long _appliedConfigsVersion;

    private byte[]? _cachedCapabilitiesBytes;

    // Version tracking for lists that are expensive to rebuild
    private long _requestAppliedConfigsVersion = -1;

    public RcmSubscriptionManager()
    {
        _request = CreateManagedRequest();
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
#if SHARED_REMOTE_CONFIG
            ForgetUnregisteredOwner(oldSubscription);
#endif

            RefreshProductKeys();
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions = new List<ISubscription>(_subscriptions.Where(s => s != subscription));
#if SHARED_REMOTE_CONFIG
            ForgetUnregisteredOwner(subscription);
#endif
            RefreshProductKeys();
        }
    }

#if SHARED_REMOTE_CONFIG
    internal static HashSet<string> GetProductsWithChangedOwners(IReadOnlyList<ISubscription> previous, IReadOnlyList<ISubscription> current)
    {
        var products = new HashSet<string>(StringComparer.Ordinal);
        AddProducts(previous);
        AddProducts(current);
        products.RemoveWhere(product => HaveSameDeliveryOwners(
            previous,
            current,
            subscription => subscription.ProductKeys.Contains(product) || GetRemovalProducts(subscription).Contains(product)));
        return products;

        void AddProducts(IReadOnlyList<ISubscription> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                products.UnionWith(subscription.ProductKeys);
                products.UnionWith(GetRemovalProducts(subscription));
            }
        }
    }
#endif

    /// <summary>
    /// Called by RCM
    /// </summary>
    internal async Task<List<ApplyDetails>?> ApplyChanges(
        Dictionary<string, List<RemoteConfiguration>>? configByProducts,
        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct,
        ISubscription? watched = null,
        IReadOnlyList<ISubscription>? subscriptions = null,
        Func<bool>? tryBeginDelivery = null,
        HashSet<string>? assignedPaths = null,
        HashSet<string>? replayProducts = null)
    {
        List<ApplyDetails>? results = null;

#if SHARED_REMOTE_CONFIG
        HashSet<string>? explicitlyRemoved = null;
        if (assignedPaths is not null && removedConfigsByProduct is { Count: > 0 })
        {
            explicitlyRemoved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var removedConfigs in removedConfigsByProduct.Values)
            {
                foreach (var path in removedConfigs)
                {
                    explicitlyRemoved.Add(path.Path);
                }
            }
        }
#endif

        foreach (var subscription in subscriptions ?? Volatile.Read(ref _subscriptions))
        {
            if (ReferenceEquals(subscription, watched))
            {
                continue;
            }

            var productKeys = subscription.ProductKeys;
            var filteredConfigs = FilterProductChanges(productKeys, configByProducts);
            var filteredRemovals = subscriptions is null ? removedConfigsByProduct : FilterProductChanges(GetRemovalProducts(subscription), removedConfigsByProduct);

#if SHARED_REMOTE_CONFIG
            if (assignedPaths is not null)
            {
                filteredRemovals = ReconcileDeliveredPaths(subscription, filteredRemovals, assignedPaths, replayProducts, explicitlyRemoved);
            }
#endif

            var haveChanges = filteredConfigs?.Count > 0 || filteredRemovals?.Count > 0;
            if (!haveChanges)
            {
                continue;
            }

            if (tryBeginDelivery?.Invoke() is false)
            {
                break;
            }

            try
            {
#if SHARED_REMOTE_CONFIG
                RememberDeliveredPaths(subscription, filteredConfigs);
#endif
                results ??= new();
                results.AddRange(await subscription.Invoke(filteredConfigs ?? [], filteredRemovals).ConfigureAwait(false));
#if SHARED_REMOTE_CONFIG
                ForgetWithdrawnPaths(subscription, filteredRemovals);
#endif
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to apply remote configurations for products {ProductKeys}", string.Join(", ", subscription.ProductKeys));
            }
        }

        return results;
    }

    private static IReadOnlyCollection<string> GetRemovalProducts(ISubscription subscription) =>
        (subscription as Subscription)?.RemovalProductKeys ?? subscription.ProductKeys;

    private static Dictionary<string, List<TChange>>? FilterProductChanges<TChange>(IReadOnlyCollection<string> products, Dictionary<string, List<TChange>>? changes)
    {
        if (changes is null || changes.Count == 0)
        {
            return null;
        }

        Dictionary<string, List<TChange>>? filtered = null;
        foreach (var product in products)
        {
            if (changes.TryGetValue(product, out var files))
            {
                filtered ??= new(products.Count);
                filtered[product] = files;
            }
        }

        return filtered;
    }

#if SHARED_REMOTE_CONFIG
    internal void RetireManagedSession() => Volatile.Write(ref _managedSessionRetired, 1);

    internal bool HasDeliveredPaths()
    {
        lock (_syncRoot)
        {
            return _deliveredPaths is { Count: > 0 };
        }
    }

    internal (IReadOnlyList<ISubscription> Subscriptions, ICollection<string> Products, byte[] Capabilities) CapturePollInterest()
    {
        lock (_syncRoot)
        {
            return (_subscriptions, ProductKeys, GetCachedCapabilities());
        }
    }

    internal bool AreSubscriptionsCurrent(
        IReadOnlyList<ISubscription> subscriptions,
        Dictionary<string, List<RemoteConfiguration>> upserts,
        Dictionary<string, List<RemoteConfigurationPath>> removals,
        bool completeAssignment)
    {
        var current = Volatile.Read(ref _subscriptions);
        return ReferenceEquals(subscriptions, current) ||
               (!completeAssignment && HaveSameDeliveryOwners(subscriptions, current, ReceivesChanges));

        bool ReceivesChanges(ISubscription subscription) =>
            ContainsProduct(subscription.ProductKeys, upserts) || ContainsProduct(GetRemovalProducts(subscription), removals);

        static bool ContainsProduct<TChange>(IReadOnlyCollection<string> products, Dictionary<string, List<TChange>> changes)
        {
            if (changes.Count == 0)
            {
                return false;
            }

            foreach (var product in products)
            {
                if (changes.ContainsKey(product))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool HaveSameDeliveryOwners(
        IReadOnlyList<ISubscription> previous,
        IReadOnlyList<ISubscription> current,
        Func<ISubscription, bool> receivesChanges)
    {
        using var owners = current.Where(receivesChanges).GetEnumerator();
        foreach (var subscription in previous)
        {
            if (receivesChanges(subscription) && (!owners.MoveNext() || !ReferenceEquals(subscription, owners.Current)))
            {
                return false;
            }
        }

        return !owners.MoveNext();
    }

    private static object GetDeliveryOwner(ISubscription subscription) =>
        (subscription as Subscription)?.DeliveryOwner ?? subscription;

    private void ForgetUnregisteredOwner(ISubscription subscription)
    {
        var owner = GetDeliveryOwner(subscription);
        if (_deliveredPaths is { } owners && owners.ContainsKey(owner) &&
            !_subscriptions.Any(current => ReferenceEquals(GetDeliveryOwner(current), owner)))
        {
            owners.Remove(owner);
        }
    }

    private void RememberDeliveredPaths(ISubscription subscription, Dictionary<string, List<RemoteConfiguration>>? upserts)
    {
        if (upserts is not { Count: > 0 })
        {
            return;
        }

        lock (_syncRoot)
        {
            var owner = GetDeliveryOwner(subscription);
            if (!_subscriptions.Any(current => ReferenceEquals(GetDeliveryOwner(current), owner)))
            {
                return;
            }

            _deliveredPaths ??= new(ReferenceEqualityComparer.Instance);
            if (!_deliveredPaths.TryGetValue(owner, out var paths))
            {
                paths = new(StringComparer.Ordinal);
                _deliveredPaths.Add(owner, paths);
            }

            // A subscriber can install part of an assignment before returning an error.
            foreach (var files in upserts.Values)
            {
                foreach (var file in files)
                {
                    paths[file.Path.Path] = file.Path;
                }
            }
        }
    }

    private void ForgetWithdrawnPaths(ISubscription subscription, Dictionary<string, List<RemoteConfigurationPath>>? removals)
    {
        if (removals is not { Count: > 0 })
        {
            return;
        }

        lock (_syncRoot)
        {
            var owner = GetDeliveryOwner(subscription);
            if (_deliveredPaths is not { } owners || !owners.TryGetValue(owner, out var paths))
            {
                return;
            }

            foreach (var files in removals.Values)
            {
                foreach (var path in files)
                {
                    paths.Remove(path.Path);
                }
            }

            if (paths.Count == 0)
            {
                owners.Remove(owner);
            }
        }
    }

    private Dictionary<string, List<RemoteConfigurationPath>>? ReconcileDeliveredPaths(
        ISubscription subscription,
        Dictionary<string, List<RemoteConfigurationPath>>? removals,
        HashSet<string> assignedPaths,
        HashSet<string>? replayProducts,
        HashSet<string>? explicitlyRemoved)
    {
        lock (_syncRoot)
        {
            if (_deliveredPaths is not { } owners || !owners.TryGetValue(GetDeliveryOwner(subscription), out var paths))
            {
                return removals;
            }

            Dictionary<string, List<RemoteConfigurationPath>>? merged = null;
            foreach (var product in GetRemovalProducts(subscription))
            {
                if (replayProducts is not null && !replayProducts.Contains(product))
                {
                    continue;
                }

                var alreadyRemoved = removals?.GetValueOrDefault(product);
                List<RemoteConfigurationPath>? combined = null;
                foreach (var path in paths.Values)
                {
                    if (path.Product == product && !assignedPaths.Contains(path.Path) &&
                        explicitlyRemoved?.Contains(path.Path) is not true)
                    {
                        combined ??= alreadyRemoved is null ? [] : [.. alreadyRemoved];
                        combined.Add(path);
                    }
                }

                if (combined is not null)
                {
                    merged ??= removals is null ? [] : new(removals);
                    merged[product] = combined;
                }
            }

            return merged ?? removals;
        }
    }
#endif

    internal Subscription? GetSnapshotSubscription(string product, IReadOnlyList<ISubscription>? subscriptions = null)
    {
        Subscription? owner = null;
        foreach (var item in subscriptions ?? Volatile.Read(ref _subscriptions))
        {
            if (!item.ProductKeys.Contains(product))
            {
                continue;
            }

            if (owner is not null || item.ProductKeys.Count != 1 || item is not Subscription { InstallSnapshot: not null } subscription)
            {
                return null;
            }

            owner = subscription;
        }

        return owner;
    }

    public void SetCapability(BigInteger index, bool available)
    {
        lock (_syncRoot)
        {
            var updated = available ? _capabilities | index : _capabilities & ~index;
            if (updated != _capabilities)
            {
                _capabilities = updated;
                Volatile.Write(ref _cachedCapabilitiesBytes, null);
            }
        }
    }

    public byte[] GetCapabilities()
    {
        lock (_syncRoot)
        {
            return EncodeCapabilities(_capabilities);
        }
    }

    public async Task SendRequest(
        RcmClientTracer rcmTracer,
        Func<GetRcmRequest, Task<GetRcmResponse?>> callback,
        Func<bool>? acceptResponse = null,
        Action<Action>? commitResponse = null)
    {
        await _sendRequestMutex.WaitAsync().ConfigureAwait(false);

        try
        {
#if SHARED_REMOTE_CONFIG
            if (Interlocked.Exchange(ref _managedSessionRetired, 0) != 0)
            {
                _request = CreateManagedRequest();
                _appliedConfigurations.Clear();
                _appliedConfigsVersion = 0;
                _requestAppliedConfigsVersion = -1;
                _rootVersion = 1;
                _targetsVersion = 0;
                _backendClientState = null;
                _lastPollError = null;
            }
#endif

            var response = await TrySendRequest(rcmTracer, callback, acceptResponse, commitResponse).ConfigureAwait(false);
            if (response is null || acceptResponse?.Invoke() is false)
            {
                return;
            }

            var rootVersion = ReadRootVersion(response.Roots);
            if (response.Targets?.Signed != null)
            {
                await ProcessResponse(response, rootVersion, acceptResponse, commitResponse).ConfigureAwait(false);
            }
            else if (rootVersion is { } version)
            {
                PublishIfCurrent(() => _rootVersion = version, acceptResponse, commitResponse);
            }
        }
        catch (Exception ex)
        {
            // this is a processing issue, not a communication issue
            // so we should report the error to the agent next time
            Log.Error(ex, "Error processing RCM request");
            PublishIfCurrent(() => _lastPollError = ex.Message, acceptResponse, commitResponse);
        }
        finally
        {
            _sendRequestMutex.Release();
        }
    }

    internal byte[] GetCachedCapabilities()
    {
        if (Volatile.Read(ref _cachedCapabilitiesBytes) is { } cached)
        {
            return cached;
        }

        lock (_syncRoot)
        {
            if (_cachedCapabilitiesBytes is { } published)
            {
                return published;
            }

            var encoded = EncodeCapabilities(_capabilities);
            Volatile.Write(ref _cachedCapabilitiesBytes, encoded);
            return encoded;
        }
    }

    private static byte[] EncodeCapabilities(BigInteger capabilities)
    {
#if NETCOREAPP
        return capabilities.ToByteArray(isUnsigned: true, isBigEndian: true);
#else
        var bytes = capabilities.ToByteArray();

        if (bytes.Length > 1 && bytes[bytes.Length - 1] == 0)
        {
            // ToByteArray includes a sign byte. Capabilities use an unsigned encoding.
            var unsignedBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, unsignedBytes, unsignedBytes.Length);
            bytes = unsignedBytes;
        }

        Array.Reverse(bytes);
        return bytes;
#endif
    }

    private static long? ReadRootVersion(List<string>? roots)
    {
        if (roots is not { Count: > 0 })
        {
            return null;
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

        return tufRoot?.Signed?.Version;
    }

    private static void PublishIfCurrent(Action update, Func<bool>? acceptResponse, Action<Action>? commitResponse)
    {
        if (commitResponse is not null)
        {
            commitResponse(update);
        }
        else if (acceptResponse?.Invoke() is not false)
        {
            update();
        }
    }

    private static GetRcmRequest CreateManagedRequest() => new(new RcmClient(Guid.NewGuid().ToString(), new RcmClientState()));

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
    }

    private async Task<GetRcmResponse?> TrySendRequest(
        RcmClientTracer rcmClientTracer,
        Func<GetRcmRequest, Task<GetRcmResponse?>> send,
        Func<bool>? acceptResponse,
        Action<Action>? commitResponse)
    {
        try
        {
            var request = BuildRequest(rcmClientTracer, _lastPollError);
            if (acceptResponse?.Invoke() is false)
            {
                return null;
            }

            _lastPollError = null;
            return await send(request).ConfigureAwait(false);
        }
        catch (RemoteConfigurationDeserializationException e)
        {
            Log.Error(e, "Error sending request to RCM endpoint: serialization error");
            // serialization errors should be reported to the agent
            // but we want the _inner_ message
            PublishIfCurrent(() => _lastPollError = $"{e.Message}: {e.InnerException?.Message}", acceptResponse, commitResponse);
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

    private async Task ProcessResponse(GetRcmResponse response, long? rootVersion, Func<bool>? acceptResponse, Action<Action>? commitResponse)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var description = response.TargetFiles?.Count > 0
                                  ? "with the following paths: " + string.Join(',', response.TargetFiles.Select(t => t.Path))
                                  : "that is empty.";
            Log.Debug("Received Remote Configuration response {ResponseDescription}.", description);
        }

        var signed = response.Targets?.Signed?.Targets;

        Dictionary<string, List<RemoteConfiguration>>? configByProducts = null;
        HashSet<string>? receivedPaths = null;
        List<RemoteConfigurationCache>? updates = null;
        if (response.ClientConfigs?.Count > 0)
        {
            receivedPaths = new HashSet<string>(StringComparer.Ordinal);

            // handle new configurations
            foreach (var clientConfig in response.ClientConfigs)
            {
                var remoteConfigurationPath = RemoteConfigurationPath.FromPath(clientConfig);
                if (!receivedPaths.Add(remoteConfigurationPath.Path))
                {
                    continue;
                }

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

                var targetFile = response.TargetFiles?.Find(file => file.Path == remoteConfigurationPath.Path);
                if (targetFile is null)
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in target files");
                }

                var remoteConfigurationCache = new RemoteConfigurationCache(remoteConfigurationPath, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom?.V ?? 0);
                (updates ??= []).Add(remoteConfigurationCache);

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

#if SHARED_REMOTE_CONFIG
        var assignedPaths = receivedPaths ?? EmptyAssignment;
#else
        HashSet<string>? assignedPaths = null;
#endif
        var results = await ApplyChanges(
                          configByProducts,
                          removedConfigsByProduct,
                          tryBeginDelivery: acceptResponse,
                          assignedPaths: assignedPaths).ConfigureAwait(false);
        PublishIfCurrent(Commit, acceptResponse, commitResponse);

        void Commit()
        {
            if (rootVersion is { } version)
            {
                _rootVersion = version;
            }

            if (updates is not null)
            {
                foreach (var update in updates)
                {
                    _appliedConfigurations[update.Path.Path] = update;
                    _appliedConfigsVersion++;
                }
            }

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

            if (results is not null)
            {
                foreach (var result in results)
                {
                    switch (result.ApplyState)
                    {
                        case ApplyStates.UNACKNOWLEDGED:
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
        }

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

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
