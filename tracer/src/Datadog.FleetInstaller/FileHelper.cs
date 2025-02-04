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
    public static bool VerifyFiles(ILogger log, SymlinkedTracerValues symlinkedValues, VersionedTracerValues versionedValues)
    {
        // Verify that the critical files we expect to find in the file system _are_ there
        // Obviously there are race conditions around this we can't avoid, this is just a gut check

        // The versioned path is the path to the tracer home directory for the specific version being installed
        // The versioned native loaders should exist, as they are what are loaded by the tracer, even though
        // we don't point to them directly
        if (!DoTracerFilesExist(log, versionedValues, checkGacFiles: true)
            || !DoTracerFilesExist(log, symlinkedValues))
        {
            return false;
        }

        // check the symlink directory is as expected
        try
        {
            // var symLinkDir = new DirectoryInfo(symlinkedValues.TracerHomeDirectory);
            var attrs = File.GetAttributes(symlinkedValues.TracerHomeDirectory);
            if (!attrs.HasFlagFast(FileAttributes.Directory) || !attrs.HasFlagFast(FileAttributes.ReparsePoint))
            {
                log.WriteError($"The Tracer home directory symlink did not have the expected characteristics. Expected a directory symlink, but found '{attrs.ToString()}'");
                return false;
            }
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error checking symlink directory details: ");
            return false;
        }

        return true;

        static bool DoTracerFilesExist(ILogger log, TracerValues values, bool checkGacFiles = false)
        {
            if (!Directory.Exists(values.TracerHomeDirectory))
            {
                log.WriteError($"The Tracer home directory does not exist at the provided path '{values.TracerHomeDirectory}'");
                return false;
            }

            if (!File.Exists(values.NativeLoaderX64Path))
            {
                log.WriteError($"The .NET Tracer's x64 native loader file does not exist at the provided path '{values.NativeLoaderX64Path}'");
                return false;
            }

            if (!File.Exists(values.NativeLoaderX86Path))
            {
                log.WriteError($"The .NET Tracer's x86 native loader file does not exist at the provided path '{values.NativeLoaderX86Path}'");
                return false;
            }

            if (checkGacFiles)
            {
                foreach (var gacFile in values.FilesToAddToGac)
                {
                    if (!File.Exists(gacFile))
                    {
                        log.WriteError($"The .NET Tracer file to add to the GAC does not exist at the provided path '{gacFile}'");
                        return false;
                    }
                }
            }

            return true;
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

    private static bool HasFlagFast(this FileAttributes value, FileAttributes flag)
        => flag == 0 || (value & flag) == flag;
}
