// <copyright file="ConfiguratorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal struct ConfiguratorHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfiguratorHelper>();

    internal static ConfigurationResult? GetConfiguration()
    {
        var configHandle = NativeInterop.LibraryConfig.ConfiguratorNew(1, new CharSlice("dotnet"));
        var configurationResult = NativeInterop.LibraryConfig.ConfiguratorGet(configHandle);
        var result = configurationResult.Result;
        ConfigurationResult? configurationResultReturned = null;
        if (configurationResult.Tag == ResultTag.Err)
        {
            Log.Error("Failed to store tracer metadata with message: {Error}", result.Error.Message.ToUtf8String());
            NativeInterop.Common.DropError(ref result.Error);
        }
        else
        {
            ref var configurationResultRef = ref configurationResult.Result;
            var libraryConfigs = result.Ok;
            var configsLength = (int)libraryConfigs.Length;
            var configEntriesLocal = new Dictionary<string, ConfigurationEntry>();
            var configEntriesRemote = new Dictionary<string, ConfigurationEntry>();
            var structSize = Marshal.SizeOf<LibraryConfig>();
            for (var i = 0; i < configsLength; i++)
            {
                unsafe
                {
                    var ptr = new IntPtr(libraryConfigs.Ptr + (structSize * i));
                    var libraryConfig = (LibraryConfig*)ptr;
                    var confEntry = new ConfigurationEntry(libraryConfig->Name.ToUtf8String(), libraryConfig->Value.ToUtf8String());
                    if (libraryConfig->Source == LibraryConfigSource.FleetStableConfig)
                    {
                        configEntriesRemote.Add(confEntry.Key, confEntry);
                    }
                    else if (libraryConfig->Source == LibraryConfigSource.LocalStableConfig)
                    {
                        configEntriesLocal.Add(confEntry.Key, confEntry);
                    }
                }
            }

            NativeInterop.LibraryConfig.LibraryConfigDrop(configurationResultRef.Ok);
            configurationResultReturned = new ConfigurationResult(configEntriesLocal, configEntriesRemote);
        }

        if (configHandle != IntPtr.Zero)
        {
            NativeInterop.LibraryConfig.ConfiguratorDrop(configHandle);
        }

        return configurationResultReturned;
    }
}
