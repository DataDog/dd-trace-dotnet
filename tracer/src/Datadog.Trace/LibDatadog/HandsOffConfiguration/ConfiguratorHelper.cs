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
    internal static ConfigurationResult GetConfiguration(string? handsOffLocalConfigPath, string? handsOffFleetConfigPath, bool? isLibdatadogAvailable = null)
    {
        if (isLibdatadogAvailable is false)
        {
            return new ConfigurationResult(null, "Skipping hands-off configuration because LibDatadog is not available", Result.LibDatadogUnavailable);
        }

        if (isLibdatadogAvailable is null)
        {
            var isLibdatadogAvailableEval = LibDatadogAvailabilityHelper.IsLibDatadogAvailable;
            if (!isLibdatadogAvailableEval.IsAvailable)
            {
                return new ConfigurationResult(null, "Skipping hands-off configuration: as LibDatadog is not available", Result.LibDatadogUnavailable, isLibdatadogAvailableEval.Exception);
            }
        }

        CharSlice? languageCs = null;
        CString? localPath = null;
        CString? fleetPath = null;
        var configHandle = IntPtr.Zero;
        LibraryConfigResult? configurationResult = null;
        try
        {
            languageCs = new CharSlice(TracerConstants.Language);

            // We have to force disable debug logs because otherwise they write directly to the console
            // which could be breaking.
            configHandle = NativeInterop.LibraryConfig.ConfiguratorNew(debugLogs: 0, languageCs.Value);

            if (handsOffLocalConfigPath is not null)
            {
                localPath = new CString(handsOffLocalConfigPath);
                NativeInterop.LibraryConfig.ConfiguratorWithLocalPath(configHandle, localPath.Value);
            }

            if (handsOffFleetConfigPath is not null)
            {
                fleetPath = new CString(handsOffFleetConfigPath);
                NativeInterop.LibraryConfig.ConfiguratorWithFleetPath(configHandle, fleetPath.Value);
            }

            configurationResult = NativeInterop.LibraryConfig.ConfiguratorGet(configHandle);
            var result = configurationResult.Value.Result;

            if (configurationResult.Value.Tag == ResultTag.Err)
            {
                var resultError = result.Error;
                var error = resultError.Message.ToUtf8String();
                return new ConfigurationResult(null, error, Result.LibDatadogCallError);
            }

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
                        configEntriesRemote[name] = value;
                    }
                    else if (libraryConfig->Source == LibraryConfigSource.LocalStableConfig)
                    {
                        configEntriesLocal[name] = value;
                    }
                }
            }

            return new ConfigurationResult(new ConfigurationSuccessResult(configEntriesLocal, configEntriesRemote), null, Result.Success);
        }
        catch (Exception ex)
        {
            return new ConfigurationResult(null, "Failed to get hands-off configuration.", Result.LibDatadogCallError, ex);
        }
        finally
        {
            languageCs?.Dispose();
            localPath?.Dispose();
            fleetPath?.Dispose();

            if (configurationResult.HasValue)
            {
                NativeInterop.LibraryConfig.LibraryConfigDrop(configurationResult.Value);
            }

            if (configHandle != IntPtr.Zero)
            {
                NativeInterop.LibraryConfig.ConfiguratorDrop(configHandle);
            }
        }
    }
}
