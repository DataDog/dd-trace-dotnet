// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed partial class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _managedPollMutex = new(1, 1);

    // Ideally this would be an ImmutableArray but that's not available in net461
    private IReadOnlyList<ISubscription> _subscriptions = [];
    private Dictionary<object, Dictionary<string, RemoteConfigurationPath>>? _deliveredPaths;

    private BigInteger _capabilities;
    private byte[]? _cachedCapabilitiesBytes;

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
            ForgetUnregisteredOwner(oldSubscription);

            RefreshProductKeys();
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions = new List<ISubscription>(_subscriptions.Where(s => s != subscription));
            ForgetUnregisteredOwner(subscription);
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
        DeliveryAdmission tryBeginDelivery,
        ChangeCommit tryCommit,
        ISubscription? watched = null,
        IReadOnlyList<ISubscription>? subscriptions = null,
        HashSet<string>? assignedPaths = null,
        HashSet<string>? replayProducts = null)
    {
        List<ApplyDetails>? results = null;

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

        foreach (var subscription in subscriptions ?? Volatile.Read(ref _subscriptions))
        {
            if (ReferenceEquals(subscription, watched))
            {
                continue;
            }

            var productKeys = subscription.ProductKeys;
            var install = (subscription as Subscription)?.InstallChanges;
            var filteredConfigs = FilterProductChanges(productKeys, configByProducts);
            var filteredRemovals = subscriptions is null && install is null ? removedConfigsByProduct : FilterProductChanges(GetRemovalProducts(subscription), removedConfigsByProduct);

            if (assignedPaths is not null)
            {
                filteredRemovals = ReconcileDeliveredPaths(subscription, filteredRemovals, assignedPaths, replayProducts, explicitlyRemoved);
            }

            var haveChanges = filteredConfigs?.Count > 0 || filteredRemovals?.Count > 0;
            if (!haveChanges)
            {
                continue;
            }

            if (!tryBeginDelivery(install is null ? filteredConfigs : null, install is null ? filteredRemovals : null))
            {
                break;
            }

            try
            {
                ApplyDetails[] applied;
                if (install is not null)
                {
                    var upserts = filteredConfigs ?? [];
                    var committed = install(
                        upserts,
                        filteredRemovals,
                        publish => tryCommit(upserts, filteredRemovals, () =>
                        {
                            publish();
                            RememberDeliveredPaths(subscription, filteredConfigs);
                            ForgetWithdrawnPaths(subscription, filteredRemovals);
                        }));
                    if (committed is null)
                    {
                        continue;
                    }

                    applied = committed;
                }
                else
                {
                    RememberDeliveredPaths(subscription, filteredConfigs);
                    applied = await subscription.Invoke(filteredConfigs ?? [], filteredRemovals).ConfigureAwait(false);
                    ForgetWithdrawnPaths(subscription, filteredRemovals);
                }

                (results ??= new()).AddRange(applied);
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
#endif

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

                List<RemoteConfigurationPath>? alreadyRemoved = null;
                removals?.TryGetValue(product, out alreadyRemoved);
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

    public ManagedRcmPoll CreateManagedPoller() => new ManagedSession(this).SendRequest;

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

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();

#if !NET5_0_OR_GREATER
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();

        bool IEqualityComparer<object>.Equals(object? left, object? right) => ReferenceEquals(left, right);

        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
#endif
}
