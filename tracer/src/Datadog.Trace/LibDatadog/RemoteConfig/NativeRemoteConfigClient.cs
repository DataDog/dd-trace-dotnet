// <copyright file="NativeRemoteConfigClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.LibDatadog.RemoteConfig;

/// <summary>
/// The libdatadog remote configuration client. It talks to the agent itself, and hands out the
/// configuration contents without copying them, see <see cref="RemoteConfigurationContentHandle"/>.
/// </summary>
internal sealed class NativeRemoteConfigClient : IRemoteConfigClient
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NativeRemoteConfigClient>();

    private readonly RemoteConfigClientHandle _handle;

    // The fetches take one token, and Cancel cancels a clone of it, as a token must not be used by two threads at once
    private readonly CancellationTokenHandle _fetchCancellation;
    private readonly CancellationTokenHandle _cancellation;

    private NativeRemoteConfigClient(RemoteConfigClientHandle handle)
    {
        _handle = handle;
        _fetchCancellation = CancellationTokenHandle.Create();
        _cancellation = _fetchCancellation.Clone();
    }

    public static NativeRemoteConfigClient Create(string clientId, RcmClientTracer target, RemoteConfigEndpoint endpoint)
    {
        using var strings = new NativeStrings();
        var config = new RcClientConfig
        {
            Language = strings.Add(target.Language),
            TracerVersion = strings.Add(target.TracerVersion),
            RuntimeId = strings.Add(target.RuntimeId),
            ClientId = strings.Add(clientId),
            Target = ToTarget(strings, target),
        };
        var nativeEndpoint = CreateEndpoint(endpoint);
        try
        {
            NativeInterop.RemoteConfig.ClientNew(ref config, nativeEndpoint, out var client).ThrowIfError();
            return new NativeRemoteConfigClient(new RemoteConfigClientHandle(client));
        }
        finally
        {
            NativeInterop.Common.EndpointDrop(nativeEndpoint);
        }
    }

    public void SetProducts(ICollection<string> products, BigInteger capabilities)
    {
        if (!(capabilities >> 64).IsZero)
        {
            Log.Warning("Remote configuration capabilities above bit 63 are not supported, so they are not advertised");
        }

        using var strings = new NativeStrings();
        var result = NativeInterop.RemoteConfig.ClientSetProducts(_handle, strings.AddAll(products), (ulong)(capabilities & ulong.MaxValue));
        try
        {
            result.ThrowIfError();
        }
        catch (LibDatadogException ex)
        {
            // The supported products and capabilities are subscribed to nevertheless
            Log.Warning(ex, "Failed to subscribe to some remote configuration products or capabilities");
        }
    }

    public void SetExtraServices(string[]? services)
    {
        using var strings = new NativeStrings();
        NativeInterop.RemoteConfig.ClientSetExtraServices(_handle, strings.AddAll(services)).ThrowIfError();
    }

    public void Rebind(RcmClientTracer target, RemoteConfigEndpoint? endpoint)
    {
        using var strings = new NativeStrings();
        var nativeTarget = ToTarget(strings, target);
        var nativeEndpoint = endpoint is null ? IntPtr.Zero : CreateEndpoint(endpoint);
        try
        {
            NativeInterop.RemoteConfig.ClientRebind(_handle, ref nativeTarget, nativeEndpoint).ThrowIfError();
        }
        finally
        {
            if (nativeEndpoint != IntPtr.Zero)
            {
                NativeInterop.Common.EndpointDrop(nativeEndpoint);
            }
        }
    }

    public void SetTags(RcmClientTracer target)
    {
        using var strings = new NativeStrings();
        NativeInterop.RemoteConfig.ClientSetTags(_handle, strings.AddAll(target.Tags)).ThrowIfError();
    }

    public unsafe RemoteConfigurationChanges Fetch()
    {
        NativeInterop.RemoteConfig.ClientFetch(_handle, _fetchCancellation, out var changes).ThrowIfError();

        var updated = new List<RemoteConfiguration>();
        var removed = new List<RemoteConfigurationPath>();
        var count = (int)changes.Len;
        var firstUnclaimed = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var change = ((RcChange*)changes.Ptr)[i];
                firstUnclaimed = i + 1;
                RemoteConfigurationContent? contents = null;
                if (change.Kind != RcChange.Remove)
                {
                    if (change.ContentsLen == 0)
                    {
                        NativeInterop.RemoteConfig.ContentDrop(change.ContentsOwner);
                        contents = Array.Empty<byte>();
                    }
                    else
                    {
                        contents = new RemoteConfigurationContent(new RemoteConfigurationContentHandle(change.ContentsPtr, change.ContentsLen, change.ContentsOwner));
                    }
                }

                var path = RemoteConfigurationPath.FromPath(Encoding.UTF8.GetString((byte*)change.PathPtr, (int)change.PathLen));
                if (contents is null)
                {
                    removed.Add(path);
                }
                else
                {
                    updated.Add(new RemoteConfiguration(path, contents, (long)change.Version));
                }
            }
        }
        finally
        {
            // Only if the loop failed midway: nothing else releases these contents
            for (var i = firstUnclaimed; i < count; i++)
            {
                NativeInterop.RemoteConfig.ContentDrop(((RcChange*)changes.Ptr)[i].ContentsOwner);
            }

            NativeInterop.RemoteConfig.ChangesDrop(changes);
        }

        return new RemoteConfigurationChanges(updated, removed);
    }

    public void SetApplyState(string path, ulong applyState, string? error)
    {
        using var strings = new NativeStrings();
        NativeInterop.RemoteConfig.ClientSetApplyState(_handle, strings.Add(path), (uint)applyState, strings.Add(error)).ThrowIfError();
    }

    public void Cancel() => _cancellation.Cancel();

    public void Dispose()
    {
        _handle.Dispose();
        _fetchCancellation.Dispose();
        _cancellation.Dispose();
    }

    /// <summary>
    /// Creates a libdatadog endpoint, which must be released with <see cref="NativeInterop.Common.EndpointDrop"/>.
    /// </summary>
    private static IntPtr CreateEndpoint(RemoteConfigEndpoint endpoint)
    {
        using var url = new CharSlice(endpoint.Url);
        var nativeEndpoint = NativeInterop.Common.EndpointFromUrl(url);
        if (nativeEndpoint == IntPtr.Zero)
        {
            throw new LibDatadogException($"Invalid agent url for remote configuration: {endpoint.Url}");
        }

        NativeInterop.Common.EndpointSetTimeout(nativeEndpoint, endpoint.TimeoutMs);
        return nativeEndpoint;
    }

    // RcmSubscriptionManager.IsSameTargetExceptTags compares these fields but the tags: keep both lists in sync
    private static RcTarget ToTarget(NativeStrings strings, RcmClientTracer target)
        => new()
        {
            Service = strings.Add(target.Service),
            Env = strings.Add(target.Env),
            AppVersion = strings.Add(target.AppVersion),
            Tags = strings.AddAll(target.Tags),
            ProcessTags = strings.AddAll(target.ProcessTags),
        };
}
