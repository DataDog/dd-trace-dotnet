// <copyright file="ConfiguratorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal struct ConfiguratorHelper
{
    internal static ConfigurationResult GetConfiguration(bool debugEnabled, string? handsOffLocalConfigPath, string? handsOffFleetConfigPath, bool? isLibdatadogAvailable = null)
    {
        if (isLibdatadogAvailable is false or null)
        {
            var isLibdatadogAvailableEval = LibDatadogAvailaibilityHelper.IsLibDatadogAvailable;
            if (!isLibdatadogAvailableEval.IsAvailable)
            {
                return new ConfigurationResult(null, "Skipping hands-off configuration: as LibDatadog is not available", Result.LibDatadogUnavailable, isLibdatadogAvailableEval.Exception);
            }
        }

        try
        {
            var configHandle = NativeInterop.LibraryConfig.ConfiguratorNew(debugEnabled ? (byte)1 : (byte)0, new CharSlice(TracerConstants.Language));
            CString? localPath = null;
            CString? fleetPath = null;
            if (handsOffLocalConfigPath is not null)
            {
                localPath = new CString(handsOffLocalConfigPath);
                NativeInterop.LibraryConfig.ConfiguratorWithLocalPath(configHandle, localPath!.Value);
            }

            if (handsOffFleetConfigPath is not null)
            {
                fleetPath = new CString(handsOffFleetConfigPath);
                NativeInterop.LibraryConfig.ConfiguratorWithFleetPath(configHandle, fleetPath!.Value);
            }

            var configurationResult = NativeInterop.LibraryConfig.ConfiguratorGet(configHandle);
            var result = configurationResult.Result;
            ConfigurationResult configurationResultReturned;
            if (configurationResult.Tag == ResultTag.Err)
            {
                var error = result.Error.Message.ToUtf8String();
                NativeInterop.Common.DropError(ref result.Error);
                configurationResultReturned = new ConfigurationResult(null, error, Result.LibDatadogCallError);
            }
            else
            {
                ref var configurationResultRef = ref configurationResult.Result;
                var libraryConfigs = result.Ok;
                var configsLength = (int)libraryConfigs.Length;
                var configEntriesLocal = new Dictionary<string, string>();
                var configEntriesRemote = new Dictionary<string, string>();
                var structSize = Marshal.SizeOf<LibraryConfig>();
                for (var i = 0; i < configsLength; i++)
                {
                    unsafe
                    {
                        var ptr = new IntPtr(libraryConfigs.Ptr + (structSize * i));
                        var libraryConfig = (LibraryConfig*)ptr;
                        var name = libraryConfig->Name.ToUtf8String();
                        var value = libraryConfig->Value.ToUtf8String();
                        if (libraryConfig->Source == LibraryConfigSource.FleetStableConfig)
                        {
                            configEntriesRemote.Add(name, value);
                        }
                        else if (libraryConfig->Source == LibraryConfigSource.LocalStableConfig)
                        {
                            configEntriesLocal.Add(name, value);
                        }
                    }
                }

                NativeInterop.LibraryConfig.LibraryConfigDrop(configurationResultRef.Ok);
                localPath?.Dispose();
                fleetPath?.Dispose();
                configurationResultReturned = new ConfigurationResult(new ConfigurationSuccessResult(configEntriesLocal, configEntriesRemote), null, Result.Success);
            }

            if (configHandle != IntPtr.Zero)
            {
                NativeInterop.LibraryConfig.ConfiguratorDrop(configHandle);
            }

            return configurationResultReturned;
        }
        catch (Exception ex)
        {
            return new ConfigurationResult(null, "Failed to get hands-off configuration.", Result.LibDatadogCallError, ex);
        }
    }
}
