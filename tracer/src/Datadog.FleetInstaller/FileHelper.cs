// <copyright file="FileHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Datadog.FleetInstaller;

internal static class FileHelper
{
    public static bool TryVerifyFiles(ILogger log, TracerValues values, out string? error)
    {
        // Verify that the critical files we expect to find in the file system _are_ there
        // Obviously there are race conditions around this we can't avoid, this is just a gut check
        // The versioned path is the path to the tracer home directory for the specific version being installed
        // The versioned native loaders should exist, as they are what are loaded by the tracer, even though
        // we don't point to them directly
        error = null;
        if (!Directory.Exists(values.TracerHomeDirectory))
        {
            var message = $"The Tracer home directory does not exist at the provided path '{values.TracerHomeDirectory}'";
            error ??= message;
            log.WriteError(message);
        }

        if (!File.Exists(values.NativeLoaderX64Path))
        {
            var message = $"The .NET Tracer's x64 native loader file does not exist at the provided path '{values.NativeLoaderX64Path}'";
            error ??= message;
            log.WriteError(message);
        }

        if (!File.Exists(values.NativeLoaderX86Path))
        {
            var message = $"The .NET Tracer's x86 native loader file does not exist at the provided path '{values.NativeLoaderX86Path}'";
            error ??= message;
            log.WriteError(message);
        }

        // Make sure the sub files exist
        foreach (var gacFile in values.FilesToAddToGac)
        {
            if (!File.Exists(gacFile))
            {
                var message = $"The .NET Tracer file to add to the GAC does not exist at the provided path '{gacFile}'";
                error ??= message;
                log.WriteError(message);
            }
        }

        return error is null;
    }

    /// <summary>
    /// Delete the native loader files, as a precursor to uninstalling a version.
    /// If the files can be deleted, they're not in use, and it's safe to remove of the files
    /// </summary>
    public static bool TryDeleteNativeLoaders(ILogger log, TracerValues values)
    {
        // Delete the native loader
        try
        {
            log.WriteInfo($"Deleting native loader file at '{values.NativeLoaderX86Path}'");
            File.Delete(values.NativeLoaderX86Path);

            log.WriteInfo($"Deleting native loader file at '{values.NativeLoaderX64Path}'");
            File.Delete(values.NativeLoaderX64Path);
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Error deleting native loaders");
            return false;
        }
    }

    public static bool CreateLogDirectory(ILogger log, string logDirectory)
    {
        log.WriteInfo($"Ensuring log directory '{logDirectory}' exists");

        try
        {
            var dirInfo = Directory.CreateDirectory(logDirectory);
            return SetDirectoryPermissions(log, dirInfo);
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"There was an error creating the log directory '{logDirectory}'");
            return false;
        }

        static bool SetDirectoryPermissions(ILogger log, DirectoryInfo directoryInfo)
        {
            try
            {
                // Give write access to "Everyone"
                var dSecurity = directoryInfo.GetAccessControl();
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify | FileSystemRights.Synchronize,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));
                return true;
            }
            catch (Exception ex)
            {
                log.WriteError(ex, "Error setting directory security");
                return false;
            }
        }
    }
}
