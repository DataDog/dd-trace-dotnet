// <copyright file="WindowsDirectoryAccess.NonWindows.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner;

internal static class WindowsDirectoryAccess
{
    internal static void CreatePrivateDirectory(string path)
    {
        throw new PlatformNotSupportedException();
    }

    internal static void ValidateDirectoryAccess(
        string path,
        bool requireCurrentUserOwner,
        bool requireTrustedOwner,
        bool allowBroadWrite)
    {
        throw new PlatformNotSupportedException();
    }
}
