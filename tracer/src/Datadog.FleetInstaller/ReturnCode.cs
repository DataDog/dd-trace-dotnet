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
    ErrorDuringPrerequisiteVerification = 1, // Not explicitly called, but equivalent
    ErrorRemovingNativeLoaderFiles = 2, // Must not change, special-case handled by fleet installer as it means "don't remove the files"
    ErrorDuringGacInstallation,
    ErrorDuringGacUninstallation,
    ErrorSettingAppPoolVariables,
    ErrorRemovingAppPoolVariables,
    ErrorRemovingCrashTrackerKey,
    ErrorReadingIisConfiguration,
    ErrorSettingGlobalEnvironmentVariables,
    ErrorRemovingGlobalEnvironmentVariables,
}
