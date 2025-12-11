// <copyright file="Defaults.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.FleetInstaller;

internal static class Defaults
{
    public const string CrashTrackingRegistryKey = @"Software\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules";
    public const string InstrumentationInstallTypeKey = "DD_INSTRUMENTATION_INSTALL_TYPE";
    public const string InstrumentationInstallTypeValue = "windows_fleet_installer";

    public static string TracerLogDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Datadog .NET Tracer", "logs");
}
