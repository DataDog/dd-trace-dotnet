// <copyright file="ReturnCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.FleetInstaller;

internal enum ReturnCode
{
    // The order of these values is important, as they are used to determine the exit code of the process
    // We should always add new error values to the end and not re-order them
    Success = 0,
    ErrorDuringPrerequisiteVerification, // Not explicitly called, but equivalent
    ErrorDuringGacInstallation,
    ErrorDuringGacUninstallation,
    ErrorSettingAppPoolVariables,
    ErrorRemovingAppPoolVariables,
    ErrorRemovingNativeLoaderFiles,
    ErrorRemovingCrashTrackerKey,
}
