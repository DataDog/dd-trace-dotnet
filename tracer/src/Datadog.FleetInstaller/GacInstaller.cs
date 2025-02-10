// <copyright file="GacInstaller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.Permissions;
using PInvoke;

namespace Datadog.FleetInstaller;

internal static class GacInstaller
{
    private static readonly string MsCorLibDirectory
        = Path.GetDirectoryName(Assembly.GetAssembly(typeof(object))!.Location.Replace('/', '\\'))!;

    /// <summary>
    /// Publish the tracer files to the GAC.
    /// </summary>
    public static bool TryGacInstall(ILogger log, TracerValues tracerValues)
    {
        // We use the versioned x64 native loader path as the file that should exist to avoid uninstalling from the GAC
        // while the file is still in use
        // TODO: verify this holds up in 100% of cases.

        var pairs = tracerValues
            .FilesToAddToGac
            .Select(gacFile => (gacFile, tracerValues.NativeLoaderX64Path));

        return TryGacInstall(log, pairs);
    }

    /// <summary>
    /// Remove the tracer files from the GAC.
    /// </summary>
    public static bool TryGacUninstall(ILogger log, TracerValues tracerValues)
    {
        // We use the versioned x86 native loader path as the file that should exist to avoid uninstalling from the GAC
        // while the file is still in use.
        // We additionally pre-emptively explicitly block removing from the GAC while either the x64 or x86 files are on
        // disk - if they're on disk, they could be referenced, and removing the files from the GAC is dangerous. If the
        // files are removed, we know they're not in use, so we're safe

        foreach (var nativeLoader in new[] { tracerValues.NativeLoaderX86Path, tracerValues.NativeLoaderX64Path })
        {
            if (File.Exists(nativeLoader))
            {
                log.WriteError($"Error uninstalling files from the GAC - .NET Tracer file '{nativeLoader}' still exists on disk. " +
                               $"It is not safe to remove files from the GAC.");
                return false;
            }
        }

        var pairs = tracerValues
            .FilesToAddToGac
            .Select(gacFile => (gacFile, tracerValues.NativeLoaderX64Path));

        return TryGacUninstall(log, pairs);
    }

    /// <summary>
    /// Publish an assembly to the GAC.
    /// Based on System.EnterpriseServices.Internal.Publish.GacInstall(), but
    /// ensures we add the ref counting.
    /// </summary>
    /// <param name="log">A logger instance</param>
    /// <param name="filesToGac">A collection of pairs of files to install in the GAC.
    /// <c>AssemblyToGacPath</c> is the absolute path to the assembly to add to the GAC.
    /// <c>AssociatedFilepath</c> is the path to an application that uses the GAC assembly. The item should not be removed from the GAC if this application exists</param>
    private static unsafe bool TryGacInstall(ILogger log, IEnumerable<(string AssemblyToGacPath, string AssociatedFilepath)> filesToGac)
    {
        try
        {
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
            var fusionDllPath = Path.Combine(MsCorLibDirectory, "fusion.dll");
            if (LoadLibrary(fusionDllPath) == IntPtr.Zero)
            {
                log.WriteError($"Error loading fusion.dll from '{fusionDllPath}': path not found");
                return false;
            }

            var retValue = Fusion.CreateAssemblyCache(out var ppAsmCache, 0);
            if (retValue != HResult.Code.S_OK)
            {
                log.WriteError($"Error installing assemblies to GAC. Error code {retValue} returned from CreateAssemblyCache.");
                return false;
            }

            foreach (var (assemblyToGacPath, associatedFilepath) in filesToGac)
            {
                fixed (char* associatedFilepathPtr = associatedFilepath)
                {
                    var fusionInstallReference = new Fusion.FUSION_INSTALL_REFERENCE
                    {
                        cbSize = (uint)sizeof(Fusion.FUSION_INSTALL_REFERENCE),
                        dwFlags = Fusion.FusionInstallReferenceFlags.None,
                        guidScheme = Fusion.FUSION_INSTALL_REFERENCE.FUSION_REFCOUNT_FILEPATH_GUID,
                        szIdentifier = associatedFilepathPtr
                    };
                    retValue = ppAsmCache.InstallAssembly(0U, assemblyToGacPath, &fusionInstallReference);
                }

                if (retValue != HResult.Code.S_OK)
                {
                    log.WriteError($"Error installing assembly '{assemblyToGacPath}' to GAC. Error code {retValue} returned from CreateAssemblyCache.");
                    return false;
                }

                log.WriteInfo($"Successfully installed assembly '{assemblyToGacPath}' into the GAC.");
            }

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error installing assemblies to GAC");
            return false;
        }
    }

    /// <summary>
    /// Remove an assembly from the GAC.
    /// This should only be called if we _know_ that the assembly won't be needed anymore
    /// </summary>
    /// <param name="log">A logger instance</param>
    /// <param name="filesToRemove">A collection of pairs of files to uninstall from the GAC.
    /// <c>AssemblyToGacPath</c> is the absolute path to the assembly to add to the GAC.
    /// <c>AssociatedFilepath</c> is the path to an application that uses the GAC assembly. The item should not be removed from the GAC if this application exists</param>
    private static unsafe bool TryGacUninstall(ILogger log, IEnumerable<(string AssemblyToGacPath, string AssociatedFilepath)> filesToRemove)
    {
        try
        {
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();

            var retValue = Fusion.CreateAssemblyCache(out var ppAsmCache, 0);
            if (retValue != HResult.Code.S_OK)
            {
                log.WriteError($"Error uninstalling assemblies from GAC. Error code {retValue} returned from CreateAssemblyCache.");
                return false;
            }

            foreach (var (gacAssemblyPath, associatedFilepath) in filesToRemove)
            {
                var gacName = Path.GetFileNameWithoutExtension(gacAssemblyPath);
                if (string.IsNullOrEmpty(gacName))
                {
                    log.WriteError($"Error uninstalling '{gacAssemblyPath}' from GAC: could not determine GAC name.");
                }

                Fusion.UninstallDisposition disposition = 0;
                fixed (char* associatedFilepathPtr = associatedFilepath)
                {
                    var fusionInstallReference = new Fusion.FUSION_INSTALL_REFERENCE
                    {
                        cbSize = (uint)sizeof(Fusion.FUSION_INSTALL_REFERENCE),
                        dwFlags = Fusion.FusionInstallReferenceFlags.None,
                        guidScheme = Fusion.FUSION_INSTALL_REFERENCE.FUSION_REFCOUNT_FILEPATH_GUID,
                        szIdentifier = associatedFilepathPtr
                    };
                    retValue = ppAsmCache.UninstallAssembly(0U, gacName, &fusionInstallReference, &disposition);
                }

                if (disposition is Fusion.UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_ALREADY_UNINSTALLED
                    or Fusion.UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_REFERENCE_NOT_FOUND)
                {
                    log.WriteInfo($"Assembly '{gacAssemblyPath}' was already uninstalled from the GAC.");
                }
                else if (retValue == HResult.Code.S_OK)
                {
                    log.WriteInfo($"Successfully uninstalled assembly '{gacAssemblyPath}' from the GAC.");
                }
                else
                {
                    log.WriteError(
                        $"Error uninstalling assembly '{gacAssemblyPath}' from GAC. Error code {retValue} returned from UninstallAssembly, with uninstall value {disposition}.");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error uninstalling assemblies from the GAC");
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string filename);
}
