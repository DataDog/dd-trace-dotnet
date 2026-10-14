// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;
using Datadog.Trace.LibDatadog.RemoteConfig;
using Datadog.Trace.LibDatadog.ServiceDiscovery;

namespace Datadog.Trace.LibDatadog;

internal static class NativeInterop
{
    private const string DllName = "LibDatadog";

    internal static class Common
    {
        [DllImport(DllName, EntryPoint = "ddog_Error_drop")]
        internal static extern void Drop(ErrorHandle error);

        [DllImport(DllName, EntryPoint = "ddog_Error_drop")]
        internal static extern void DropError(ref Error errorHandle);

        /// <summary>
        /// Returns null if the url is invalid.
        /// </summary>
        [DllImport(DllName, EntryPoint = "ddog_endpoint_from_url")]
        internal static extern IntPtr EndpointFromUrl(CharSlice url);

        [DllImport(DllName, EntryPoint = "ddog_endpoint_set_timeout")]
        internal static extern void EndpointSetTimeout(IntPtr endpoint, ulong millis);

        [DllImport(DllName, EntryPoint = "ddog_endpoint_drop")]
        internal static extern void EndpointDrop(IntPtr endpoint);

        [DllImport(DllName, EntryPoint = "ddog_CancellationToken_new")]
        internal static extern IntPtr CancellationTokenNew();

        [DllImport(DllName, EntryPoint = "ddog_CancellationToken_clone")]
        internal static extern IntPtr CancellationTokenClone(CancellationTokenHandle token);

        [DllImport(DllName, EntryPoint = "ddog_CancellationToken_cancel")]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool CancellationTokenCancel(CancellationTokenHandle token);

        [DllImport(DllName, EntryPoint = "ddog_CancellationToken_drop")]
        internal static extern void CancellationTokenDrop(IntPtr token);
    }

    internal static class LibraryConfig
    {
        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_new")]
        internal static extern IntPtr TracerMetadataNew();

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_free")]
        internal static extern void TracerMetadataFree(IntPtr metadata);

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_set")]
        internal static extern void TracerMetadataSet(IntPtr metadata, MetadataKind kind, CString value);

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_store")]
        internal static extern TracerMemfdHandleResult StoreTracerMetadata(IntPtr metadata);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_new")]
        internal static extern IntPtr ConfiguratorNew(byte debugLogs, CharSlice language);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_with_local_path")]
        internal static extern IntPtr ConfiguratorWithLocalPath(IntPtr configurator, CString localPath);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_with_fleet_path")]
        internal static extern IntPtr ConfiguratorWithFleetPath(IntPtr configurator, CString fleetPath);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_get")]
        internal static extern LibraryConfigResult ConfiguratorGet(IntPtr configurator);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_drop")]
        internal static extern void ConfiguratorDrop(IntPtr configurator);

        [DllImport(DllName, EntryPoint = "ddog_library_config_drop")]
        internal static extern void LibraryConfigDrop(LibraryConfigResult configs);
    }

    internal static class RemoteConfig
    {
        [DllImport(DllName, EntryPoint = "ddog_rc_client_new")]
        internal static extern VoidResult ClientNew(ref RcClientConfig config, IntPtr endpoint, out IntPtr client);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_drop")]
        internal static extern void ClientDrop(IntPtr client);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_rebind")]
        internal static extern VoidResult ClientRebind(RemoteConfigClientHandle client, ref RcTarget target, IntPtr endpoint);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_set_products")]
        internal static extern VoidResult ClientSetProducts(RemoteConfigClientHandle client, CharSlices products, ulong capabilities);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_set_extra_services")]
        internal static extern VoidResult ClientSetExtraServices(RemoteConfigClientHandle client, CharSlices services);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_fetch")]
        internal static extern VoidResult ClientFetch(RemoteConfigClientHandle client, CancellationTokenHandle cancel, out RcChanges changes);

        [DllImport(DllName, EntryPoint = "ddog_rc_client_set_apply_state")]
        internal static extern VoidResult ClientSetApplyState(RemoteConfigClientHandle client, CharSlice path, uint state, CharSlice error);

        [DllImport(DllName, EntryPoint = "ddog_rc_changes_drop")]
        internal static extern void ChangesDrop(RcChanges changes);

        [DllImport(DllName, EntryPoint = "ddog_rc_content_drop")]
        internal static extern void ContentDrop(IntPtr owner);
    }
}
