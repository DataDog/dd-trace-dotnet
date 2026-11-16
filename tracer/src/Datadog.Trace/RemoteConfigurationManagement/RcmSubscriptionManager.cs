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
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();

    private readonly IDatadogLogger _log;
    private readonly object _syncRoot = new();

    // Serializes the polls, and with them the dispatch to the subscriptions: the remote configuration managers
    // of a replaced and of the replacing tracer may poll concurrently
    private readonly object _pollLock = new();

    // Publishes the client and cancels it as one step, so that a shutdown during the client creation isn't lost
    private readonly object _cancellationLock = new();

    // The paths of the configurations the client delivered and hasn't removed since. Only accessed with _pollLock held.
    private readonly HashSet<string> _deliveredPaths = [];

    // Ideally this would be an ImmutableArray but that's not available in net461
    private IReadOnlyList<ISubscription> _subscriptions = [];

    private BigInteger _capabilities;

    // The client of the AppDomain, and what was last sent to it. Only accessed with _pollLock held, except that _client
    // and _isCancelled are also accessed by Cancel with only _cancellationLock held, and _isCancelled is read without lock.
    private IRemoteConfigClient? _client;
    private bool _isCancelled;
    private RcmClientTracer? _clientTracer;
    private RemoteConfigEndpoint? _clientEndpoint;
    private string[]? _clientExtraServices;
    private ICollection<string>? _pushedProductKeys;
    private BigInteger _pushedCapabilities;

    internal RcmSubscriptionManager(IDatadogLogger? log = null)
    {
        _log = log ?? DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();
    }

    public string ClientId { get; } = Guid.NewGuid().ToString();

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
                _log.Error(e, "Failed to apply remote configurations for products {ProductKeys}", string.Join(", ", subscription.ProductKeys));
            }
        }

        return results;
    }

    public void SetCapability(BigInteger index, bool available)
    {
        lock (_syncRoot)
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
    }

    public bool Poll(Func<string, RcmClientTracer, RemoteConfigEndpoint, IRemoteConfigClient> clientFactory, RcmClientTracer tracer, RemoteConfigEndpoint endpoint, string[]? extraServices)
    {
        lock (_pollLock)
        {
            if (Volatile.Read(ref _isCancelled))
            {
                return false;
            }

            var client = _client;
            if (client is null)
            {
                try
                {
                    client = clientFactory(ClientId, tracer, endpoint);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Remote Configuration is unavailable: failed to create the remote configuration client");
                    return false;
                }

                lock (_cancellationLock)
                {
                    _client = client;
                    if (_isCancelled)
                    {
                        client.Cancel();
                        return false;
                    }
                }
            }
            else
            {
                // A rebind starts a new client session, so only for an actual change, not for every settings update
                var isSameEndpoint = IsSameEndpoint(endpoint, _clientEndpoint);
                if (!isSameEndpoint || !IsSameTarget(tracer, _clientTracer))
                {
                    client.Rebind(tracer, isSameEndpoint ? null : endpoint);
                }
            }

            _clientTracer = tracer;
            _clientEndpoint = endpoint;

            if (!ReferenceEquals(extraServices, _clientExtraServices))
            {
                client.SetExtraServices(extraServices);
                _clientExtraServices = extraServices;
            }

            PushProductsIfChanged(client);
            FetchAndDispatch(client);
            return true;
        }
    }

    public void Cancel()
    {
        lock (_cancellationLock)
        {
            _isCancelled = true;
            _client?.Cancel();
        }
    }

    // The fields NativeRemoteConfigClient.ToTarget sends: keep both lists in sync
    private static bool IsSameTarget(RcmClientTracer tracer, RcmClientTracer? previous)
        => ReferenceEquals(tracer, previous)
        || (previous is not null
         && tracer.Service == previous.Service
         && tracer.Env == previous.Env
         && tracer.AppVersion == previous.AppVersion
         && tracer.Tags.SequenceEqual(previous.Tags)
         && (tracer.ProcessTags ?? []).SequenceEqual(previous.ProcessTags ?? []));

    private static bool IsSameEndpoint(RemoteConfigEndpoint endpoint, RemoteConfigEndpoint? previous)
        => ReferenceEquals(endpoint, previous)
        || (previous is not null && endpoint.Url == previous.Url && endpoint.TimeoutMs == previous.TimeoutMs);

    private void FetchAndDispatch(IRemoteConfigClient client)
    {
        RemoteConfigurationChanges changes;
        try
        {
            changes = client.Fetch();
        }
        catch (Exception e)
        {
            if (Volatile.Read(ref _isCancelled))
            {
                _log.Debug(e, "Remote configuration fetch cancelled on shutdown");
            }
            else
            {
                _log.Warning(e, "Error fetching remote configurations");
            }

            return;
        }

        if (changes.IsEmpty)
        {
            return;
        }

        if (_log.IsEnabled(LogEventLevel.Debug))
        {
            _log.Debug(
                "Received remote configurations. Updated: {Updated}. Removed: {Removed}.",
                string.Join(",", changes.Updated.Select(c => c.Path.Path)),
                string.Join(",", changes.Removed.Select(p => p.Path)));
        }

        Dictionary<string, List<RemoteConfiguration>>? configByProducts = null;
        foreach (var config in changes.Updated)
        {
            _deliveredPaths.Add(config.Path.Path);
            configByProducts ??= [];
            if (!configByProducts.TryGetValue(config.Path.Product, out var configs))
            {
                configByProducts[config.Path.Product] = configs = [];
            }

            configs.Add(config);
        }

        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct = null;
        foreach (var path in changes.Removed)
        {
            _deliveredPaths.Remove(path.Path);
            removedConfigsByProduct ??= [];
            if (!removedConfigsByProduct.TryGetValue(path.Product, out var paths))
            {
                removedConfigsByProduct[path.Product] = paths = [];
            }

            paths.Add(path);
        }

        // libdatadog's client removes every configuration when the agent answers 404 or the configurations expired.
        // Removing a single configuration is usually deleting it on purpose.
        if (changes.Removed.Count > 1 && _deliveredPaths.Count == 0)
        {
            _log.Warning<int>("All {Count} remote configurations were removed at once. If they weren't deleted on purpose, the agent stopped serving remote configuration, or its configurations expired.", changes.Removed.Count);
        }

        // Polling happens on a dedicated thread, so blocking on the (rarely asynchronous) subscribers is fine
        var results = Update(configByProducts, removedConfigsByProduct).GetAwaiter().GetResult();
        if (results is null)
        {
            return;
        }

        foreach (var result in results)
        {
            if (result.ApplyState == ApplyStates.UNACKNOWLEDGED)
            {
                continue;
            }

            client.SetApplyState(result.Filename, result.ApplyState, result.Error);
        }
    }

    private void PushProductsIfChanged(IRemoteConfigClient client)
    {
        ICollection<string> productKeys;
        BigInteger capabilities;
        lock (_syncRoot)
        {
            productKeys = ProductKeys;
            capabilities = _capabilities;
        }

        if (ReferenceEquals(productKeys, _pushedProductKeys) && capabilities == _pushedCapabilities)
        {
            return;
        }

        client.SetProducts(productKeys, capabilities);
        _pushedProductKeys = productKeys;
        _pushedCapabilities = capabilities;
    }

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
