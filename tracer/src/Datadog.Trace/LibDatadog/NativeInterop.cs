// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;
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
}
