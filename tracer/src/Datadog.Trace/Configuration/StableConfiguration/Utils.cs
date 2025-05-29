// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace;
using Datadog.Trace.Configuration.StableConfiguration;
using Datadog.Trace.LibDatadog.LibraryConfig;

internal class Utils
{
    public static IEnumerable<FleetConfiguration> GetConfigurations()
    {
        VecLibraryConfig? configs = null;
        if (FrameworkDescription.Instance.IsWindows())
        {
            configs = Windows.GetConfigurations();
        }
        else
        {
            configs = NonWindows.GetConfigurations();
        }

        if (configs.HasValue)
        {
            unsafe
            {
                if (configs.HasValue && configs.Value.Length > 0)
                {
                    var libraryConfigs = configs.Value;
                    LibraryConfig* libraryConfigsPtr = (LibraryConfig*)libraryConfigs.Ptr;
                    var length = (int)configs.Value.Length;
                    var list = new List<FleetConfiguration>(length);
                    for (var i = 0; i < length; i++)
                    {
                        var config = libraryConfigsPtr[i];
                        var configName = config.Name;
                        var configValue = Encoding.UTF8.GetString((byte*)config.Value.Ptr, (int)config.Value.Length);
                        list.Add(new FleetConfiguration(configName, configValue, config.Source));
                    }

                    return list;
                }
            }
        }

        return Array.Empty<FleetConfiguration>();
    }

    private static class Windows
    {
        [DllImport("Datadog.Tracer.Native.dll")]
        public static extern VecLibraryConfig GetConfigurations();
    }

    // assume .NET Core if not running on Windows
    // These methods are rewritten by the native tracer to use the correct paths
    private static class NonWindows
    {
        [DllImport("Datadog.Tracer.Native")]
        public static extern VecLibraryConfig GetConfigurations();
    }
}
