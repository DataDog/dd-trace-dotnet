// <copyright file="RcmSubscriptionManager.ManagedSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed partial class RcmSubscriptionManager
{
    private sealed class ManagedSession(RcmSubscriptionManager registry)
    {
        private static readonly HashSet<string> EmptyAssignment = new(StringComparer.Ordinal);

        private readonly RcmSubscriptionManager _registry = registry;
        private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();
        private readonly GetRcmRequest _request = new(new RcmClient(Guid.NewGuid().ToString(), new RcmClientState()));
        private long _rootVersion = 1;
        private string? _backendClientState;
        private long _targetsVersion;
        private string? _lastPollError;
        private long _appliedConfigsVersion;
        private long _requestAppliedConfigsVersion = -1;

        internal async Task SendRequest(
            RcmClientTracer rcmTracer,
            Func<GetRcmRequest, Task<GetRcmResponse?>> callback,
            Func<bool>? acceptResponse = null,
            Action<Action>? commitResponse = null)
        {
            await _registry._managedPollMutex.WaitAsync().ConfigureAwait(false);

            try
            {
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
                _registry._managedPollMutex.Release();
            }
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
            requestClient.Products = _registry.ProductKeys;
            requestClient.Capabilities = _registry.GetCachedCapabilities();

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

                    if (!_registry.ProductKeys.Contains(remoteConfigurationPath.Product))
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
            foreach (var appliedConfiguration in _appliedConfigurations.Values)
            {
                AddRemoval(appliedConfiguration.Path);
            }

            var deliveryDeclined = false;
            var results = await _registry.ApplyChanges(
                              configByProducts,
                              removedConfigsByProduct,
                              tryBeginDelivery: TryBeginDelivery,
                              tryCommit: TryCommitChanges,
                              assignedPaths: receivedPaths ?? EmptyAssignment).ConfigureAwait(false);
            if (!deliveryDeclined)
            {
                PublishIfCurrent(Commit, acceptResponse, commitResponse);
            }

            void AddRemoval(RemoteConfigurationPath path)
            {
                if (receivedPaths?.Contains(path.Path) == true)
                {
                    return;
                }

                removedConfigsByProduct ??= [];
                if (!removedConfigsByProduct.TryGetValue(path.Product, out var removedConfigs))
                {
                    removedConfigs = [];
                    removedConfigsByProduct[path.Product] = removedConfigs;
                }

                removedConfigs.Add(path);
            }

            bool TryBeginDelivery(
                Dictionary<string, List<RemoteConfiguration>>? rawUpserts,
                Dictionary<string, List<RemoteConfigurationPath>>? rawRemovals)
            {
                if (deliveryDeclined || acceptResponse?.Invoke() is false)
                {
                    deliveryDeclined = true;
                    return false;
                }

                // A raw callback can install changes before it returns or reports an error.
                InvalidateReceipts(rawUpserts, rawRemovals);
                return true;
            }

            bool TryCommitChanges(Dictionary<string, List<RemoteConfiguration>> upserts, Dictionary<string, List<RemoteConfigurationPath>>? removals, Action publish)
            {
                var committed = false;
                PublishIfCurrent(Apply, acceptResponse, commitResponse);
                deliveryDeclined |= !committed;
                return committed;

                void Apply()
                {
                    publish();
                    InvalidateReceipts(upserts, removals);
                    committed = true;
                }
            }

            void InvalidateReceipts(
                Dictionary<string, List<RemoteConfiguration>>? upserts,
                Dictionary<string, List<RemoteConfigurationPath>>? removals)
            {
                if (removals is not null)
                {
                    foreach (var paths in removals.Values)
                    {
                        foreach (var path in paths)
                        {
                            ForgetReceipt(path.Path);
                        }
                    }
                }

                if (upserts is not null)
                {
                    foreach (var configs in upserts.Values)
                    {
                        foreach (var config in configs)
                        {
                            ForgetReceipt(config.Path.Path);
                        }
                    }
                }
            }

            void ForgetReceipt(string path)
            {
                if (_appliedConfigurations.Remove(path))
                {
                    _appliedConfigsVersion++;
                }
            }

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
                            ForgetReceipt(value.Path);
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
    }
}
