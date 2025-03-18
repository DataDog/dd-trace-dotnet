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
using System.Text;
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
                var simpleName = Path.GetFileNameWithoutExtension(gacAssemblyPath);
                if (string.IsNullOrEmpty(simpleName))
                {
                    log.WriteError($"Error uninstalling '{gacAssemblyPath}' from GAC: could not determine GAC name.");
                }

                // We need the full version of the assembly we're uninstalling, but we only have the name
                // (the file might not exist on disk anymore, so we can't try to read it either). We can't use
                // the simple name, as if there are multiple versions of the Datadog.Trace (for example)
                // assembly installed, then the fusion API returns
                // IASSEMBLYCACHE_UNINSTALL_DISPOSITION_REFERENCE_NOT_FOUND if we try to use the simple name,
                // even if they have different filepath references.
                List<string>? assemblyNamesToTry = null;
                retValue = Fusion.CreateAssemblyNameObject(out var gacAssemblyName, simpleName, Fusion.CREATE_ASM_NAME_OBJ_FLAGS.NONE, IntPtr.Zero);
                if (retValue != HResult.Code.S_OK || gacAssemblyName is null)
                {
                    log.WriteWarning($"Error creating IAssemblyName object for {simpleName}. Error code {retValue} returned from CreateAssemblyEnum. Continuing with simple assembly name {simpleName}");
                }
                else
                {
                    retValue = Fusion.CreateAssemblyEnum(out var enumerator, IntPtr.Zero, gacAssemblyName, Fusion.ASM_CACHE_FLAGS.ASM_CACHE_GAC, IntPtr.Zero);
                    if (retValue != HResult.Code.S_OK)
                    {
                        log.WriteWarning($"Error enumerating assemblies from GAC. Error code {retValue} returned from CreateAssemblyEnum. Continuing with simple assembly name {simpleName}");
                    }
                    else
                    {
                        var bufferSize = 256;
                        var sb = new StringBuilder(bufferSize);

                        while (retValue == HResult.Code.S_OK)
                        {
                            retValue = enumerator.GetNextAssembly(IntPtr.Zero, out var assemblyName, 0);
                            if (retValue == HResult.Code.S_OK && assemblyName is not null)
                            {
                                sb.Clear();
                                retValue = assemblyName.GetDisplayName(sb, ref bufferSize, Fusion.ASM_DISPLAY_FLAGS.ASM_DISPLAYF_FULL);
                                if (retValue == HResult.Code.S_OK)
                                {
                                    var name = sb.ToString();
                                    log.WriteError($"Found assembly {name}");
                                    assemblyNamesToTry ??= new();
                                    assemblyNamesToTry.Add(name);
                                }
                            }
                        }
                    }
                }

                assemblyNamesToTry ??= [simpleName];

                // Loop through all the assembly names we found. Only one of them
                // should match the associatedFilepath, so this should be safe.
                var success = false;
                foreach (var gacName in assemblyNamesToTry)
                {
                    Fusion.UninstallDisposition disposition = 0;
                    fixed (char* associatedFilepathPtr = associatedFilepath)
                    {
                        var fusionInstallReference = new Fusion.FUSION_INSTALL_REFERENCE
                        {
                            cbSize = (uint)Marshal.SizeOf<Fusion.FUSION_INSTALL_REFERENCE>(),
                            dwFlags = Fusion.FusionInstallReferenceFlags.None,
                            guidScheme = Fusion.FUSION_INSTALL_REFERENCE.FUSION_REFCOUNT_FILEPATH_GUID,
                            szIdentifier = associatedFilepathPtr
                        };
                        retValue = ppAsmCache.UninstallAssembly(0U, gacName, &fusionInstallReference, &disposition);
                    }

                    if (disposition is Fusion.UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_ALREADY_UNINSTALLED
                        or Fusion.UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_REFERENCE_NOT_FOUND)
                    {
                        log.WriteInfo($"Assembly '{gacAssemblyPath}' with name '{gacName}' was already uninstalled from the GAC or does not match associated reference.");
                        success = true;
                    }
                    else if (disposition is Fusion.UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_HAS_INSTALL_REFERENCES)
                    {
                        log.WriteInfo($"Assembly '{gacAssemblyPath}' with name '{gacName}' has additional install references. It was not removed from the GAC, but will be removed when all references are removed.");
                        success = true;
                    }
                    else if (retValue == HResult.Code.S_OK)
                    {
                        log.WriteInfo($"Successfully uninstalled assembly '{gacAssemblyPath}' with name '{gacName}' from the GAC.");
                        success = true;
                    }
                    else
                    {
                        log.WriteError(
                            $"Error uninstalling assembly '{gacAssemblyPath}' with name '{gacName}' from GAC. Error code {retValue} returned from UninstallAssembly, with uninstall value {disposition}.");
                    }
                }

                if (!success)
                {
                    log.WriteError(
                        $"Error uninstalling assembly '{gacAssemblyPath}' from GAC - no successful or implicit uninstalls.");
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
